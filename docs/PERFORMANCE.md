# Performance and reliability audit

## Scope

2026-10-02: code inspection of the desktop pipeline, providers, settings bridge, device monitor, logging, native subtitles and packaging. External provider latency, recognition accuracy and long-session memory stability require real hardware/service acceptance; unit tests do not measure these.

## Translation freshness

Previously the serial translation worker retained 32 pending requests. With a 20-second HTTP budget per request, a stalled provider could theoretically leave many minutes of old speech queued. This is a worst-case bound from code, not a measured service delay.

Desktop system-audio translation now retains at most three pending requests plus one in flight. Overflow drops the oldest pending translation; every recognized source still appears immediately with its stable subtitle ID. Requests queued for more than ten seconds are skipped before making a network request. Skipped/dropped requests retain their original text. Legacy microphone/chat output retains its 32-entry FIFO behavior. Stop still cancels the worker and HTTP transport.

A deterministic blocked-transport regression emits 101 source entries: after unblocking, only the in-flight first entry and the newest three pending entries reach the provider. No source entries are discarded. This verifies backlog policy, not real API latency.

HTTP uses one reused client per translator instance, one 20-second total retry budget and at most three attempts. Authorization failures are not retried. Long Retry-After values now fail promptly instead of being clamped and retried prematurely; cooldowns over two seconds are not appropriate for realtime captions. Short retry delays retain the existing bounded policy. Long cooldowns are retained per provider client: subsequent captions fail promptly without another HTTP request until the cooldown expires. Weak client ownership avoids a static registry leak. A regression verifies two captions produce only one HTTP request during a one-minute cooldown. Replacing a provider client resets this local cooldown.

The Groq prompt now requests faithful, natural output preserving meaning and tone, without commentary or Markdown. No model, token cap or context mechanism was changed; output quality needs provider-backed native-speaker evaluation.

## Resource ownership and idle work

Code inspection: bounded subtitle history, serial translation worker, bounded delayed-output queue (32), cancellation and explicit disposal of translators/capture/IPC/tray, and cached locale fonts disposed with native windows are already present. No forced garbage collections were introduced. Desktop device recovery checks every two seconds; while stopped, it checks the child process without enumerating playback devices or creating the legacy microphone engine. Settings WebView is created on demand rather than at tray startup.

Logging uses 5 MB file rollover, seven-file retention per session and startup cleanup of older session logs. Settings persist atomically and credentials use Windows DPAPI. Configuration load currently writes a migrated config on startup; settings changes can trigger both automatic persistence and explicit save acknowledgement. These small, user-triggered duplicate writes remain to preserve retry/durable-save semantics.

## Release integrity

The release script compares SHA-256 of every generated frontend file with its packaged counterpart, explicitly awaits the published native self-test with a 30-second timeout, and hashes every packaged file. Source/config/log/PDB files and known credential patterns are rejected. Existing releases are never overwritten. No trimming is enabled: native/WinForms/Photino reliability takes priority over size.

## Acceptance still required

Long-running playback and provider outages; sleep/resume and device changes; mixed-monitor DPI; native accessibility/high contrast; API translation accuracy and end-to-end latency. No claims of measured improvements to CPU, private memory or real network latency are made from source inspection alone.


## Measured packaged application behavior

Local Windows run of `DesktopTranslator-0.2.0-alpha.5-win-x64` on 2026-10-02, translation stopped using `--start-stopped --settings`. Measurements use Windows Process working set/private bytes/handle counters and CPU-time differences, with the owned process tree identified by parent PID. No external speech/translation request was made. Samples are short observations, not a before/after benchmark or proof of leak-free operation.

| State | Process | Working set MiB | Private MiB | Handles | CPU seconds in interval |
| --- | --- | ---: | ---: | ---: | ---: |
| Settings open, 15.05 s | Host | 91.1 | 21.1 | 759 | 0.438 |
| Settings open, 15.05 s | Subtitles (hidden) | 69.3 | 16.7 | 482 | 0.000 |
| Settings open, 15.05 s | Six WebView2 processes combined | 448.0 | 205.2 | 3382 | 1.968 |
| Settings closed, 15.06 s | Host | 92.2 | 22.0 | 732 | 0.016 |
| Settings closed, 15.06 s | Subtitles (hidden) | 68.5 | 16.2 | 488 | 0.000 |

The open-settings sample followed navigation and includes browser settling activity. Working sets may include shared pages and are not unique physical-memory totals. Closing settings released all six owned WebView2 processes; only host and subtitle processes remained. Graceful application Exit then left none of the eight sampled process IDs alive. These observations support on-demand settings ownership and quiet stopped tray operation, but do not justify claims about active VAD/recognition CPU or multi-hour memory stability.

The reviewed package contained 45 files, 236.9 MiB before its manifest. The actual Photino window served bundled `index.html`, displayed English / 简体中文 / 日本語 in General, applied interface changes, and saved Japanese while keeping `source_language=en` and `target_language=zh`. English light-theme screenshots covered all five sections; Chinese dark and Japanese light General were also checked. Development-preview review from the localization phase covers the broader locale/theme/size matrix; native screen-reader and mixed-DPI acceptance remain manual.

Subtitle font ownership was also tightened: unchanged appearance settings retain existing caption fonts, rather than allocating equal replacements and disposing fonts WinForms may still reference. The native self-test checks font identity on unchanged preferences and exercises native font handles. Caption controls, layout hierarchy, stable IDs and scrolling architecture are unchanged.
