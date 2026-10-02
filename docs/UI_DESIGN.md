# Desktop Translator UI design

## Purpose and direction

Desktop Translator is a tray companion, not a dashboard. Its settings should explain the listening workflow, make provider setup approachable, and let users return to playback quickly. The memorable product detail is the consistent original-above-translation hierarchy, carried from the settings sample into the real subtitle window.

This phase changes presentation and settings interactions. Audio capture, VAD, recognition, translation scheduling, provider implementations, subtitle IDs, history bounds, update semantics, and scrolling ownership stay in their existing components.

## Research and rationale

Primary project materials studied before implementation:

| Reference | Material inspected | Pattern selected | Deliberately not copied |
| --- | --- | --- | --- |
| [LocalSend](https://localsend.org/) | Official product presentation and [settings tab source](https://github.com/localsend/localsend/blob/main/app/lib/pages/tabs/settings_tab.dart) | Named settings sections, reusable label/control rows, relevant desktop/tray preferences, advanced options kept out of ordinary setup | Mobile-sized controls, transfer/device layouts, branding, animations, account/privacy claims |
| [Clash Verge Rev](https://github.com/clash-verge-rev/clash-verge-rev) | [Page structure](https://github.com/clash-verge-rev/clash-verge-rev/tree/main/src/pages) and [settings composition](https://github.com/clash-verge-rev/clash-verge-rev/blob/main/src/pages/settings.tsx) | Persistent navigation and small settings groups composed from existing React/MUI components | Networking terminology, dual-column dashboard groups, social toolbar, dense operational metrics |
| [Files](https://files.community/) | Official product material and [appearance documentation](https://files.community/docs/customize-settings/appearance), including linked UI examples | Windows-oriented typography, grouped preferences, distinct light/dark surfaces, useful status without consuming the workspace | File-manager commands/tabs, backdrop effects, elaborate theme customization, assets |

The resulting design is our interpretation of those patterns. No reference assets, branding, or source components were copied. Research was source/documentation based; these applications were not installed or benchmarked here. Some initial source URLs failed; the table lists the materials successfully retrieved.

## Information architecture

The persistent header holds product identity, Stopped / Connecting / Listening, Start/Stop translation, and Show subtitles. A compact summary inside the content area shows language direction and selected providers; it describes configuration, not verified service connectivity.

- **Translation** — source → target, selected translation provider, that provider's credentials, transcription-only output.
- **Speech recognition** — actual supported recognition providers, relevant credential editor, default playback-device explanation.
- **Subtitles** — illustrative local bilingual sample, direct native customization action, show window, interaction guidance.
- **General** — dark/light settings theme, translation on application launch, tray behavior, saving explanation.
- **About** — version, purpose, provider privacy, original project attribution.

The existing Gemini combined mode appears only when already configured; this redesign does not advertise an unimplemented independent Gemini translation provider. Auto-detection, per-application capture, system-theme synchronization, and launch with Windows are not invented UI options. The audio source is correctly identified as the default playback device, without pretending to know its hardware name.

Normal desktop navigation contains no OSC/chatbox/VRChat controls, donations, or upstream update prompts. Legacy mode remains separate. Kikitan appears only in the attribution and unchanged internal/storage names.

## Visual tokens

Use `src/components/desktop/theme.ts`, `src/pages/desktop.css`, and native `SubtitlePalette` as the implementation references. Keep palette changes synchronized rather than adding isolated colors.

| Role | Dark | Light |
| --- | --- | --- |
| Content background | `#171b22` | `#f7f8fa` |
| Sidebar | `#1c212a` | `#eff2f6` |
| Raised surface / menus | `#202630` | `#ffffff` |
| Primary text | `#f1f4f9` | `#202631` |
| Secondary text | `#aebacc` | `#586373` |
| Separator | `#343e4d` | `#dde2e9` |
| Accent and focus | `#8ab4ff` | `#235fc4` |
| Selected navigation | `#283a55` | `#dce8fc` |

Status dots use neutral gray for stopped, amber for connecting, and green for listening. Text always accompanies color. Errors use MUI's themed alert semantics. A listening state means the local session is active; it is not a guarantee that every external request succeeds.

Use a 4 px spacing rhythm: 4/8/12/16/20/24/32. Content inset is normally 32 px horizontally and 28 px vertically; compact widths use 20–24 px. Section spacing is 24 px. Rows use 14 px vertical padding and separators. Sidebar is 190 px (176 px at compact desktop widths). Content caps at 800 px, avoiding long form lines on very wide windows.

Controls use 6 px corners; navigation uses 5 px. Buttons are at least 34 px high, with a contained primary action and outlined/text secondary actions. Form groups use borders only where they establish a useful boundary, principally credentials and the subtitle sample. No gradients, backdrop blur, panel entrances, animated subtitle scrolling, or card-per-setting layouts.

## Typography and icons

Settings: `Segoe UI Variable`, `Segoe UI`, `system-ui`, `sans-serif`. Body 13 px / 1.6; secondary 12 px / 1.6; page title 22 px, weight 600; section title 16 px, weight 600. Use sentence case and concrete labels. Tiny footer text must never contain essential instructions or errors.

Use the existing MUI Material **Outlined** icon family, generally 20 px, without adding an icon library or emoji. Icons supplement labels. Windows/CJK system fallbacks provide subtitle language coverage; no downloaded font is required for the desktop design. Legacy Inter assets remain for the separate legacy UI.

## Forms and credential behavior

Selections and switches save automatically through the existing configuration bridge. Language/provider changes may restart an active session; General explains this behavior. Configuration-load errors disable editing while showing the reason.

Credentials use a reusable password editor with no reveal control and no prefilled saved secret. The renderer receives only configured indicators. Drafts are sent only by Save key or Enter, never on blur. A save stays pending until the host confirms the particular field and request ID. Successful confirmation clears the draft; errors/timeouts retain it. Late confirmations cannot erase a newer draft. Remove saved key is explicit. Save confirmation means settings storage succeeded, not that billing, model access, quotas, or provider connectivity were tested.

Provider requirements appear beside the selected form. Missing credentials explain that source text is retained. Runtime errors appear above the current content instead of silently disappearing. Key protection remains Windows DPAPI; the UI does not create a second credential store.

## Native subtitle presentation

The real overlay retains the reliable buffered FlowLayoutPanel and per-ID blocks. Original text uses `#bac5d4` on `#171b22`, Segoe UI at the configured point size. Translation uses `#f1f4f9`, Segoe UI Semibold at size + 2 points. Entries have 18 px bottom spacing and the history panel uses 22 px horizontal / 16 px vertical padding, scaled by native DPI behavior. Long lines wrap within measured available width. No per-entry frames, timestamps, colored badges, or motion are added.

An empty window says it is ready and tells the user to start translation and play audio. It disappears when the first source arrives. The preview is local sample content, not recognition output.

Native title bar and sizable tool-window frame remain for dependable dragging, resizing, focus, and Windows behavior. Closing still hides the overlay. Tray controls remain available when position is locked or click through is enabled.

The appearance dialog groups a live sample, text/history preferences, and window behavior. Repeated customization requests activate the existing dialog, and native ownership keeps it associated with the subtitle window. Numeric controls have explicit units and bounds. Save/Cancel stay outside the scrolling content; Enter accepts and Escape cancels. Editing is local to the dialog until Save. Font size and original/translation visibility update the sample immediately. Opacity affects the entire overlay (including text); the sample intentionally stays opaque and explains this. History is an entry limit, not a promise that all entries fit on screen.

## Accessibility and resizing

- Real buttons, labelled selects, labelled switches, password fields, heading hierarchy, navigation `aria-current`, status text, and a skip-to-content link.
- Visible keyboard focus; no icon-only navigation or color-only state.
- Independent content scrolling keeps navigation and daily controls accessible.
- At compact widths, setting rows wrap; below 650 px navigation becomes a labelled horizontal row. This is a desktop fallback, not a mobile-first redesign.
- Neutral text pairs exceed WCAG AA normal-text contrast in the design palette. Window opacity and user font choices can reduce effective overlay readability; the dialog explains opacity.
- WinForms uses DPI autoscaling, native input controls, wrapping labels, a scrollable settings body, and persistent action buttons.
- Cross-monitor 125%/150%/175% interaction, Windows high-contrast mode, screen-reader behavior in Photino/WebView2, and very long localized labels still require manual acceptance on real displays.

## Components and ownership

- `DesktopSettings`: desktop shell, navigation, current state, supported settings pages.
- `SettingsControls`: small section, row, and switch composition helpers.
- `CredentialEditor`: draft/pending/acknowledgement/error behavior shared by translation and speech credentials.
- `desktopTheme` and `desktop.css`: scoped theme and layout, leaving legacy appearance separate.
- `SubtitleAppearanceDialog` and `SubtitlePalette`: native preview and presentation; subtitle process owns persisted appearance.
- Existing settings/control IPC gets a save acknowledgement with a request ID and an appearance command. No new transport, state store, UI framework, or translation architecture is introduced.

The React subtitle page deliberately labels its sample as illustrative. The **actual** live controls are in the native dialog, reached in one click. This preserves a single preference owner and avoids a second unsynchronized store.

## Verification and developer preview

```powershell
npm test --prefix KikitanTranslator.Photino/UserInterface
npm run build --prefix KikitanTranslator.Photino/UserInterface
dotnet build kikitan-translator.sln -m:1 -p:UseSharedCompilation=false
dotnet run --project tests/DesktopTranslator.Tests --no-build --no-restore
& ./KikitanTranslator.Subtitles/bin/Debug/net9.0-windows/KikitanTranslator.Subtitles.exe --self-test | Out-Host
```

Frontend bridge tests exercise credential acknowledgements, failed saves, unsubscribe behavior, subtitle controls, and request identity without browser/network/hardware access. Native self-tests cover the existing IDs/history/manual-scroll/bottom-follow rules plus empty state, preview updates, and minimum-size wrapping/no horizontal history scrollbar.

For isolated browser review, run Vite and visit `http://127.0.0.1:1420/?preview`. Add `&theme=light` for light mode or `&save-error` to simulate save failure. This development-only fixture has no real credentials, network calls, or persisted settings and is removed from production builds. It is not evidence of provider connectivity.

Set `DESKTOP_TRANSLATOR_TEST_ARTIFACTS` to an ignored local directory before the subtitle self-test to generate native render images for inspection. It does not change user subtitle preferences. DrawToBitmap artifacts can differ from actual compositor/chrome rendering; they complement, rather than replace, manual review.

## Known platform compromises and follow-up

The native appearance dialog uses Windows system control colors, so it may be light while React settings use dark mode. No custom WinForms dark control framework or fake frameless chrome is introduced. Photino wraps a web frontend rather than WinUI; native caption/control rendering and WebView accessibility need acceptance testing. The whole-window opacity setting cannot provide independent background-only alpha with the existing WinForms design.

In this environment the browser automation connection failed, so interactive frontend screenshots, keyboard walkthroughs, and the resize matrix remain unverified. Native render images were inspected and code/layout checks were completed; build and automated checks must not be described as proof of the missing interactive review.

Next design work should prioritize real-device mixed-DPI and keyboard/accessibility review, localization of desktop copy, and a future synchronized appearance API only if consolidating native controls into React demonstrates a clear benefit. Avoid adding monitoring charts or optional provider forms to solve presentation gaps.

## Files changed in this phase

Frontend: `UserInterface/index.html`, `UserInterface/package.json`, `src/main.tsx`, `src/page.tsx`, `src/pages/DesktopSettings.tsx`, `src/pages/desktop.css`, `src/components/desktop/theme.ts`, `src/components/desktop/SettingsControls.tsx`, `src/components/desktop/CredentialEditor.tsx`, `src/util/photino.ts`, `src/vite-env.d.ts`, `src/dev/preview.tsx`, and `tests/desktop-bridge.test.mjs` (all under `KikitanTranslator.Photino`).

Settings integration only: `KikitanTranslator.Photino/Handlers/Control.cs`, `Handlers/UpdateConfig.cs`, and `Manager.cs`. These changes add the appearance action and durable, correlated save feedback, without modifying capture/recognition/translation logic.

Native presentation: `KikitanTranslator.Subtitles/SubtitleWindow.cs` and new `SubtitleAppearanceDialog.cs` (including shared palette and preview self-checks).

Documentation/build verification: `README.md`, `docs/UI_DESIGN.md`, `docs/MANUAL_TESTS.md`, and `scripts/build-release.ps1`.
