# Engineering audit and implementation — 2026-10-02

Baseline: working alpha at `7e27508`, plus three pre-existing user edits (Photino project, host startup, HTML title) and an existing release directory. No remote changes were made. The prior `CODEBASE_REVIEW.md` describes an older snapshot and is not current validation evidence.

## Prioritized findings and resulting work

| Priority | Area | Finding and disposition |
|---|---|---|
| P0 | Configuration/startup | Parse failures could leave a null configuration. Defaults now initialize reliably; corrupt/decryption-failed files are preserved and editing is blocked. |
| P0 | Pipeline/shutdown | Recognition performed synchronous network translation; Stop could wait for long requests. Separate bounded async translation worker; official providers accept cancellation. |
| P1 | Subtitles | Entry identity depended on current-block timing. Stable utterance IDs allow overlapping source/translation updates without duplicate blocks. |
| P1 | Rendering | Bottom following used ScrollControlIntoView and could jump to the top of a tall block. Anchor explicitly at bottom; preserve user scroll and compensate trimmed entry heights. Controls update in place and panels double-buffer. |
| P1 | Tray/startup | A second app could collide on a global pipe; repeated Start restarted capture. User/session mutexes prevent duplicates, per-parent subtitle pipes avoid collisions, Start is idempotent. |
| P1 | Lifecycle | Microphone polling ran forever, settings Quit used Environment.Exit, child cleanup relied only on parent polling. Owned cancellation, awaited monitor shutdown, graceful Quit, bounded child shutdown and tray disposal. |
| P1 | Recovery | Bing recreated clients along overlapping paths. Single client with built-in reconnect, serialized session setup/turn restart, no recursive Start. Default playback changes/capture failure trigger controlled recovery. |
| P1 | Child recovery | Subtitle process failure had no supervisor. The monitor relaunches it; settings-thread failure is caught so audio/tray operation can continue. |
| P1 | Settings | Desktop mode exposed legacy VRChat onboarding/UI. Dedicated desktop settings page, state and provider/key indicators; legacy page remains behind explicit legacy mode. |
| P1 | Providers | Groq leaked response bodies, did not dispose responses and retried permanent errors. Official providers now share bounded retry/deadline handling, sanitized errors and deterministic disposal. |
| P2 | Credentials | Plaintext config and full credential state broadcasts. Current-user Windows DPAPI migration, atomic save, redacted state, removal of raw message/error logs. Google key moved from URL to header. |
| P2 | Resource usage | Desktop startup created an unused microphone engine/VAD. Microphone capture is now lazy and disposed; Silero session options disposed. Groq speech buffers/queues capped; delayed output and IPC queues bounded. |
| P2 | Logging | Verbose audio/service logging and unlimited session log size. Information default, per-file size and retention limits. |
| P2 | IPC | Concurrent socket collection mutations and browser-origin access in headless mode. Synchronize sockets, reject browser origins, close bridge on disposal. No websocket listener is started in desktop mode. |
| P2 | Release | Manual publish steps and old product executable. DesktopTranslator executable/product metadata, locked frontend install, scripted checks/publish, asset verification and checksum output. |
| P2 | Tests/docs | No current regression suite, README described Rust/VRChat workflow. Dependency-free executable regression suite, WinForms presentation self-check, new desktop README and manual acceptance matrix. |
| P3 | Optional | Global hotkeys, export, generic OpenAI/Gemini translation, per-app capture and separate background opacity deferred to avoid destabilizing the alpha. |

## Preserved core

WASAPI SystemLoopback sample conversion/resampling and silence insertion, Silero model/VAD, existing Bing STT protocol/token constants, source-to-target direction, structured bilingual results, standalone native subtitle process, Groq/Google Cloud/DeepL providers, tray startup/control, and self-contained win-x64 distribution.

No namespace-wide rename, VAD algorithm rewrite, credential hardcoding, new scraping integration, remote push or release upload. Legacy OpenVR/OSC/Tauri files remain. The outdated console harness now builds and uses official Google Cloud instead of the legacy web endpoint. The old web-translator source is retained but unused by the maintained entry points.

## Tradeoffs and residual risks

- Bing is the inherited alpha service; its long-term availability is outside this application's control. No live endpoint or contractual guarantee is implied.
- Official provider retry applies only to 429/5xx. A 20-second total budget and at most three attempts bound latency. Provider-specific language support is validated by the actual API; errors are surfaced, not silently hidden.
- No external API or audio-hardware test is represented by the fake-transport regressions. Device switch recovery is implemented but requires real hardware/sleep testing.
- Whole-window opacity is the simplest WinForms solution; text also fades. Click-through requires using the tray to restore interaction.
- Source text stays available on failed/skipped translation. IPC history is not durable; crashing the subtitle process loses its prior visible history.
- DPAPI encryption can fail across Windows profiles/machines. Configuration writes are blocked after failed migration, keeping the original recoverable. Migration does not secure user-created historical backups.
- Headless bridge remains available to non-browser local tools and is not an authenticated multi-user service. Named pipes and mutexes are user/session scoped; Windows-user compromise is outside the DPAPI/IPC boundary.
- Legacy Gemini/VRChat paths retain some older behavior/logging and receive less validation. Desktop mode does not check/install upstream Kikitan updates.
- Existing generated-designer, async-handler and obsolete OpenVR/Skia warnings are intentionally not broadly refactored.

See `VALIDATION.md` in the repository for executed checks and their limits; manual checks also ship with the package.
