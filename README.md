## Desktop Translator v0.2.0-alpha.2

Second alpha release focused on stability, performance, security, and desktop usability.

### Improvements

- Stable bilingual subtitle updates with persistent subtitle IDs
- Improved subtitle scrolling and viewport behavior
- Bounded asynchronous translation pipeline
- Better cancellation and Start/Stop lifecycle handling
- Bing STT reconnect improvements
- Default playback-device recovery
- Subtitle process recovery
- Cleaner application shutdown
- Single-instance protection
- Improved tray controls
- Windows DPAPI credential protection
- Atomic configuration saves
- Redacted sensitive state and sanitized errors
- Improved provider timeout, retry, and cancellation handling
- Reproducible Windows x64 release build
- Added regression and native subtitle UI self-tests

### Validation

- 16/16 regression tests passed
- Subtitle native rendering/self-test passed
- Frontend production build passed
- Full .NET solution build passed
- Self-contained Windows x64 publish passed
- Release package checked for source files, config files, logs, node_modules, and known credential patterns

### Known limitations

The following still require real-world testing:

- Long-running audio capture
- Real Groq / Google Cloud / DeepL / Gemini service calls
- Device disconnect/reconnect
- Windows sleep/resume
- Mixed-DPI and multi-monitor behavior
- Exclusive fullscreen games
- Clean-machine WebView2 behavior

This is still a pre-release build.

## License

[Check the LICENSE.md for details](https://github.com/YusufOzmen01/kikitan-translator/blob/main/LICENSE.md)
