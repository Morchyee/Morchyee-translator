# Validation record — 2026-10-02

Final product package: `release/DesktopTranslator-2.0.2-alpha.2-win-x64/` (~236.7 MiB), including `DesktopTranslator.exe`, `KikitanTranslator.Subtitles.exe`, bundled frontend and Silero VAD model. Earlier release directories were preserved.

| Check | Result |
|---|---|
| `dotnet build kikitan-translator.sln` | Passed; 0 errors. Existing nullable, generated resource, async-handler and legacy overlay warnings remain. |
| Dependency-free regression executable | **16/16 passed**, no external HTTP calls or audio hardware used. |
| `npm run build --prefix KikitanTranslator.Photino/UserInterface` | Passed: TypeScript checking and Vite production build. Browserslist age warning remains. |
| Self-contained `win-x64` publish via release script | Passed for both host and subtitle executables. No Velopack/GitHub upload operation. |
| Published subtitle `--self-test` | Passed: actual WinForms construction, bounded history, control identity, translation updates, manual scroll retention, bottom following and wrapping/resize checks. No user preferences written. |
| Release-content inspection | No source, application config, logs, node_modules, debug symbols or known credential patterns found in text assets. Required files present. |
| Repository text credential-pattern scan | No matching Groq/Google/OpenAI key patterns found. This is a pattern check, not a proof that arbitrary secrets cannot exist. |
| `git diff --check` | Passed. |

Regression coverage includes Unicode IPC roundtrip; late translation updates and pruned history; immediate source delivery; source/target snapshot direction; failed/malformed translation preservation; Stop cancellation and late-update suppression; idempotent pipeline Start; transcription-only behavior; official provider host/header/body mapping; nonretryable authorization errors; rate-limit retries; HTTP cancellation; missing credentials; real Windows DPAPI plaintext migration and roundtrip; public-state redaction; corrupt/unavailable credential-file preservation; bounded serial Groq speech processing; and bounded translation backlog preserving source.

The initial sandbox blocked esbuild child-process creation (EPERM) and NuGet access. Approved build execution completed both. Default parallel MSBuild was also unreliable under sandbox restrictions; serial builds and the requested full build completed successfully with approved execution where needed.

The native rendering check caught and led to repair of a WinForms constructor/CreateParams initialization bug. The Groq cancellation stress check caught and led to repair of queued requests being drained after Stop. Neither failure is present in the final passing checks.

## Not verified

- Real playback capture/recognition or real Groq, Google Cloud, DeepL or Gemini credentials/service calls.
- Real default-device disconnect/reconnect, sleep/resume, network reconnection, mixed-monitor DPI interactions, exclusive full-screen games or multi-hour soak.
- Daily-use Photino/WebView2 interaction on a clean Windows machine.
- Legacy VRChat/OpenVR/Tauri integration behavior beyond compiling the maintained .NET solution.

Next acceptance work is the hardware/service matrix in `MANUAL_TESTS.md`, followed by signing/distribution and an official long-term STT alternative to the inherited Bing service. Optional hotkeys, subtitle export and extra providers should follow that evidence.
