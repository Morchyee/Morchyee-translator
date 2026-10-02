# Manual acceptance matrix

These tests require Windows audio hardware and/or real provider credentials. Automated fake-provider tests do not substitute for them.

1. **First launch and tray:** extract a complete release on a clean Windows user profile; double-click DesktopTranslator.exe. Confirm one host, one subtitle process and one tray icon. Launch again and verify no duplicate. Close subtitles, reopen from tray, open/close settings repeatedly.
2. **Recognition/direction:** play English speech with English → Japanese, then reverse the languages. Test Bing and Groq Whisper separately. Verify original text appears before translation and the translation belongs to that same block. Test transcription-only.
3. **Providers/failures:** test valid Groq, Google Cloud and DeepL API keys. Test missing/wrong keys, disabled API, quota/rate limits and unsupported languages. Source captions must remain and errors must be understandable. Do not share keys/logs.
4. **Scroll/history:** feed long sentences and rapid partials. Verify no visible top-to-bottom scroll or flicker. Scroll upward and receive new/translated entries; stay where reading. Scroll to bottom and verify natural following. Test history 3/5/50, both text visibility toggles and font sizes 10/36.
5. **DPI/position:** move across 100%, 150% and 200% monitors, resize narrow/wide, unplug a saved monitor and relaunch. Verify wrapping and accessible bounds. Test always-on-top, click-through and position lock; recover through tray.
6. **Start/stop/shutdown:** rapidly Start/Stop 20 times; change provider/languages while running. Stop during a slow request, then Exit. Verify no capture, service tasks, child settings/subtitle processes or tray icon remain. Retry after failed start.
7. **Recovery:** disconnect internet, restore it, switch default playback device, unplug/replug headphones, sleep/resume. Confirm either automatic recovery or reliable tray Stop/Start. Kill only the owned subtitle child; verify it restarts. Confirm settings crashes do not kill capture.
8. **Long session:** run for 4–8 hours with silence, continuous speech and intermittent failures. Check CPU/memory plateau and bounded logs. Confirm untranslated source survives provider outages and queues remain bounded.
9. **Secure migration:** back up existing plaintext config, launch updated app, verify API fields are encrypted while languages/providers remain unchanged. Relaunch and verify credentials work. Test an unreadable/corrupt copy in an isolated user profile; original must remain intact and UI must explain the failure.
10. **Clean machine package:** without SDK/npm installed, run the extracted package with WebView2 available. Confirm frontend and Silero load. Verify no config/log/source/node_modules shipped. Check SHA256SUMS against the extracted files.

Record machine/Windows version, playback hardware, monitor scale, provider and pass/fail details; never record actual credentials.

## Desktop design acceptance

These interaction checks are separate from provider/hardware tests above. Automated bridge/native tests do not substitute for a real Photino keyboard or monitor walkthrough.

1. Open each section in dark and light mode at 900×600 and a larger window. Verify header actions remain reachable, only content scrolls, rows align, provider names fit, and no horizontal overflow occurs.
2. Repeat at 125%, 150%, and 175% scaling and move settings/subtitles between monitors. Check CJK wrapping, focus outlines, and the native appearance dialog's scroll area and fixed Save/Cancel actions.
3. Use only Tab, Shift+Tab, Enter, Space, arrows, and Escape. Reach navigation, language/provider selects, switches, password editor, native number controls, Save/Cancel, and the skip link. Review with a screen reader and Windows high-contrast mode.
4. Select Groq, Google Cloud, and DeepL. Only the selected translation credential appears; Groq speech uses the shared credential. Type a test draft: blur must not save it; navigating between sections/providers must retain it, including the shared Groq speech draft. Enter/Save must save, and successful confirmation must clear it. Removing a saved key must preserve an unsaved replacement draft. Closing settings may discard unsaved drafts. Use an isolated profile for storage-failure testing: retain the draft on failure, avoid false success, and allow retry. Never put a real key in screenshots.
5. Customize font, history, original/translation visibility, opacity, lock, and click through. Preview font/visibility should update immediately; Cancel must leave saved preferences unchanged. Reopen after Save/relaunch and check persistence. Recover locked/click-through overlays through settings/tray.
6. Start/Stop from every section. Verify status and provider problems are understandable, and configured provider labels are not mistaken for verified connectivity. Test the empty subtitle window and first recognition result.

Resumed browser review: all five sections checked in both themes at 600×600, 720×600, 900×600, and 1280×800 CSS viewports without horizontal overflow. Provider forms, language direction, transcription-only, Start/Stop status, shared Groq drafts, successful-save clearing, simulated failure retention, native-action guidance, combobox names, and keyboard switch focus were exercised using disposable development fixtures. Frontend tests, .NET regressions, and native subtitle self-tests pass. Real Photino/WebView2 integration, screen-reader, Windows high-contrast, mixed-monitor scaling, provider connectivity, and hardware checks remain manual acceptance items.
