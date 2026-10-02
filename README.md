# Desktop Translator

A standalone Windows alpha application for real-time bilingual subtitles from system audio, derived from Kikitan Translator. Play a video, movie, game, stream or voice chat normally; the app captures the default Windows playback device and displays recognized source text above its translation.

## Quick start

1. Extract the complete `DesktopTranslator-<version>-win-x64` folder to a writable location.
2. Double-click **DesktopTranslator.exe**. A subtitle window and tray icon appear. No Node.js, .NET SDK or terminal is needed for daily use.
3. Use the tray menu → **Open settings**. Translation contains language direction and provider setup; Speech recognition configures speech service access. Paste a key and choose **Save key**; a confirmation appears after storage succeeds.
4. Use **Start translation**, **Stop translation**, **Show/Hide subtitles**, **Subtitle appearance…** and **Exit** from the tray menu. Closing the subtitle window hides it; Exit stops the app.

Requires Windows 10/11 x64, an active playback device, an internet connection for cloud providers, and Microsoft Edge WebView2 Runtime for the settings window (normally present on current Windows). The package includes .NET and Windows Desktop runtimes. Exclusive full-screen games can cover ordinary desktop windows; borderless/windowed mode is recommended.

The alpha retains the existing Bing speech service. Its availability depends on the service and is not guaranteed. Groq Whisper is the alternative using an official API key. No new service-token bypass or scraping integration is introduced.

## Interface languages

Settings → **General** → **Interface language** supports **English**, **简体中文**, and **日本語**. The choice applies immediately to desktop settings, tray actions, subtitle waiting messages, and the native appearance dialog. It is independent of speech/translation languages: a Japanese interface can translate English audio into Chinese subtitles.

Without a saved choice, the app follows the Windows UI language: Simplified Chinese environments use `zh-CN`, Japanese environments use `ja-JP`, and other environments use English. A manual choice is saved as `ui_language` and survives restarts and system-language changes. Existing credentials and subtitle preferences remain compatible. Product/provider names and technical diagnostics retain their original spelling.

## Translation providers and credentials

- **Groq:** create a GroqCloud API key. Speech recognition also uses this key when Groq Whisper is selected. Translation uses the existing configured model in `Constants.GROQ_MODEL`.
- **Google Cloud Translation:** enable Cloud Translation API in a Google Cloud project and configure an API key with suitable API restrictions and billing. The app uses the official Basic v2 endpoint and sends the key in a header.
- **DeepL:** use a **DeepL API** Free or Pro account key. The Free endpoint is selected for keys ending in `:fx`; a consumer DeepL subscription alone is insufficient.

Provider account access, quotas and language support vary. Missing keys, rejected requests, timeouts and limits are reported in the settings UI and tray. Recognized source subtitles are retained on translation failure. Choose **Transcription only** to avoid translation requests.

References: [Groq API](https://console.groq.com/docs/api-reference), [Google Cloud Translation](https://docs.cloud.google.com/translate/docs/reference/rest/v2/translate), [Google API-key authentication](https://docs.cloud.google.com/docs/authentication/api-keys-use), [DeepL translation](https://developers.deepl.com/api-reference/translate/request-translation).

Windows API keys are encrypted with **DPAPI CurrentUser** in `%APPDATA%\Kikitan Translator\config.json`; the old folder name is retained for upgrade compatibility. Existing plaintext keys migrate on a successful load. Saves replace the file atomically. If configuration parsing or credential protection fails, the original file remains untouched and editing is blocked with an error. Back up that file before repair; encrypted keys are tied to the Windows account/machine context and may need re-entry on another computer. Settings state sends only a saved-key indicator, never the key itself.

## Subtitles

The default window shows a bounded five-entry history, wraps long sentences and stays on top. Source recognition appears immediately; translation updates that entry in place even while another sentence is being recognized. Scrolling upward suspends following the latest entry. At the bottom, new content remains anchored to the bottom.

Settings → **Subtitles** → **Customize…** (also available as **Subtitle appearance…** in the tray) opens a native live-preview dialog. Save applies changes; Cancel leaves preferences unchanged. The dialog persists font size, whole-window opacity, history count (3–50), always-on-top, source/translation visibility, click-through and position locking. Click-through and position locking can always be disabled from the tray. Window bounds are restored and clamped to an available monitor. Opacity applies to text as well as background; independent background opacity is not implemented.

## Build and test

Development requires the .NET SDK selected by `global.json` and Node.js/npm. Rust/Tauri is not required for this Windows desktop product.

```powershell
npm ci --prefix KikitanTranslator.Photino/UserInterface
npm test --prefix KikitanTranslator.Photino/UserInterface
npm run build --prefix KikitanTranslator.Photino/UserInterface
dotnet build kikitan-translator.sln -m:1
dotnet run --project tests/DesktopTranslator.Tests --no-build --no-restore
```

The regression runner is a dependency-free executable: it exits nonzero on failure. It uses fake HTTP transport/recognizers, plus real Windows DPAPI in isolated temporary test files. It does **not** make external API calls or capture hardware audio. The subtitle executable also has a rendering/scroll regression check:

```powershell
& ./KikitanTranslator.Subtitles/bin/Debug/net9.0-windows/KikitanTranslator.Subtitles.exe --self-test | Out-Host
```

For interactive debug settings, run `npm run dev --prefix KikitanTranslator.Photino/UserInterface` in another terminal, then `dotnet run --project KikitanTranslator.Photino`. Debug uses the Vite server at port 1420; release serves bundled frontend assets.

The desktop settings use five focused sections and light/dark themes. See [UI design system](docs/UI_DESIGN.md) for visual rules, development-only preview instructions, accessibility checks, and platform compromises.

## Reproducible Windows release

```powershell
./scripts/build-release.ps1 -Version 2.0.2-alpha.1
```

This installs locked frontend dependencies, builds the solution and frontend, runs regressions, publishes self-contained Windows x64 executables, runs subtitle rendering checks, verifies package contents and writes SHA-256 hashes. Output: `release/DesktopTranslator-<version>-win-x64/`. Use `-SkipInstall` only when dependencies have already been restored. Existing version folders are preserved; choose another version or move the existing folder to rebuild. Failed staging folders are retained for diagnosis. No upload, GitHub operation or release creation occurs.

The package includes the subtitle executable, frontend assets and Silero ONNX model. It excludes application config, logs, source, debug symbols and node_modules. The verification script checks known credential patterns in text assets; this is not a guarantee against every possible arbitrary secret. Self-contained executables intentionally retain reliability over minimal size.

## Architecture and recovery

`SystemLoopback` (WASAPI playback capture) → mono 16 kHz frames → Silero VAD → Bing/Groq recognition → source subtitle → bounded asynchronous translation worker → translation update with the same utterance ID → per-parent Windows named pipe → WinForms subtitle window.

`KikitanTranslator.Base` contains captures, VAD, recognizers, provider abstraction, pipeline and configuration. `KikitanTranslator.Photino` is the tray-mode host, settings bridge and process supervisor. `KikitanTranslator.Subtitles` owns native rendering and the tray controls. IPC servers are restricted to the current Windows user. Pipeline starts are idempotent; settings changes explicitly restart the running pipeline. Stop cancels translations before waiting for callbacks. Default playback-device changes or unexpected capture stops trigger recovery. A crashed subtitle process is restarted. Normal Exit cancels capture/workers, closes IPC and terminates the owned subtitle child if it fails to close promptly.

Translation backlog is bounded (32 utterances). Under sustained overload, source captions continue and skipped translations are reported. Subtitle IPC is also bounded; a stalled/crashed subtitle process can lose messages. History is session-only and is not a transcript archive.

## Privacy and limitations

Audio frames stay in memory locally and are sent to the selected cloud speech provider. Recognized source text is sent to the selected translation provider. Playback capture includes all audio on the default device; it cannot currently select only one app. Provider billing, retention and privacy policies apply.

API keys are not returned to frontend state or logged by the desktop provider paths. Logs are stored under the app-data folder, at Information level with size/count limits. Legacy logging paths may still contain service error details; review diagnostic files before sharing them. DPAPI does not protect against a malicious program already running as your Windows user.

Hardware, paid-service behavior, sleep/resume, mixed-DPI multi-monitor interaction, exclusive-full-screen games and multi-hour stability need manual acceptance testing. See [manual checks](docs/MANUAL_TESTS.md) and [engineering audit](docs/ENGINEERING_AUDIT.md).

## Legacy compatibility

VRChat microphone/chatbox/OSC, OpenVR overlay and old Tauri sources remain for compatibility and reference. They are not part of the normal desktop startup workflow. `--legacy-mode` opens the old settings workflow; `--no-ui` retains the local headless bridge. The legacy Google web translator source is retained but is not selected by the desktop/manager/console runtime paths; those use official Google Cloud instead. Legacy features are less tested than desktop mode. Automatic upstream Kikitan updates are disabled in desktop mode to prevent replacing this fork with the upstream product.

## License

Derived from Kikitan Translator. Original attribution and license are preserved in [LICENSE.md](LICENSE.md).

For safe inspection of a portable release, `DesktopTranslator.exe --start-stopped --settings` opens the real settings window without starting audio capture, even if automatic start is saved. The flags do not change that saved preference. Translation favors recent pending speech during provider stalls and retains recognized source text when an older translation is skipped. See [performance and reliability findings](docs/PERFORMANCE.md) for queue policy, measurements, and acceptance limits.
