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
