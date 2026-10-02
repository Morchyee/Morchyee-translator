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
- **General** — interface language, dark/light settings theme, translation on application launch, tray behavior, saving explanation.
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

Credentials use a reusable password editor with no reveal control and no prefilled saved secret. The renderer receives only configured indicators. Drafts are sent only by Save key or Enter, never on blur. Drafts, pending saves, and feedback belong to the open settings session, keyed by credential field: section/provider navigation retains them, and Groq speech and translation share one draft. Closing the settings session discards unsaved drafts; no browser storage is used. A save stays pending until the host confirms the particular field and request ID. Successful confirmation clears the saved draft; errors/timeouts retain it. Late confirmations cannot erase a newer draft. Remove saved key is explicit and preserves a replacement draft already being edited. Save confirmation means settings storage succeeded, not that billing, model access, quotas, or provider connectivity were tested.

Provider requirements appear beside the selected form. Missing credentials explain that source text is retained. Runtime errors appear above the current content instead of silently disappearing. Key protection remains Windows DPAPI; the UI does not create a second credential store.

## Product localization

Supported locales are `en` (English fallback), `zh-CN` (简体中文), and `ja-JP` (日本語). **General → Interface language** applies immediately and is independent of source/target languages and the legacy `language` setting. An absent/null `ui_language` follows Windows `CurrentUICulture`: `zh-CN`, `zh-SG`, and `zh-Hans` variants select Simplified Chinese; Japanese variants select Japanese; all others, including Traditional Chinese environments, select English. Once explicitly chosen, the canonical locale is saved and wins over system culture. Unsupported stored values resolve to English. Invalid incoming choices are rejected.

Resources live in `KikitanTranslator.Photino/UserInterface/src/i18n/locales/{en,zh-CN,ja-JP}.json`. Flat semantic keys are grouped by namespace (`common`, `navigation`, `translation`, `speech`, `subtitles`, `general`, `about`, `credential`, `errors`, `native`, `preview`). React's small `LocaleProvider` and typed `translate` helper reuse the existing configuration bridge. The same JSON files are embedded in `KikitanTranslator.Resources` and read by `DesktopText` for native presentation. No additional i18n dependency, browser persistence, or second preference store is introduced. Catalog parity is enforced by TypeScript and tests; unknown keys throw rather than quietly displaying IDs. Missing values in a supported catalog fall back to English.

Only presentation uses translated text. Field/provider IDs, IPC commands and states remain invariant. Subtitle status messages carry an optional `UiLanguage` field, and the child also receives the locale at launch. Changing locale updates title, tray/menu, waiting text, and an already-open appearance dialog without rebuilding subtitle blocks, altering content, moving the viewport, restarting recognition, or resetting appearance edits. Native fonts are cached per locale for the window/dialog lifetime because WinForms may retain an equal assigned font object.

The shared `error-rules.json` adapts existing safe provider diagnostics into stable presentation categories (authentication, rate limit, quota, request, network, saving, generic service). React shows a localized explanation and expandable **Technical details** containing the original diagnostic. Native balloons show the localized explanation with the original detail; storage error dialogs are localized. This presentation adapter does not change service exceptions or provider networking. Unknown diagnostics retain useful detail instead of being silently discarded.

Terminology: Chinese uses **语音识别**, **源语言**, **目标语言**, **原文/译文**, **窗口置顶**, and **鼠标穿透**. Japanese uses **音声認識**, **音声の言語**, **翻訳先の言語**, **原文/翻訳**, **常に最前面に表示**, and **クリック透過**. Prefer concise action labels (开始翻译 / 翻訳を開始), natural explanatory sentences, and consistent terms over literal English syntax. Desktop Translator, provider names, WebView2, API identifiers, and technical diagnostics keep their original spelling. The fixed English/Japanese preview content deliberately demonstrates bilingual hierarchy in every locale; actual subtitles are never translated by this layer. Separate legacy-mode UI retains its older localization resources.

Typography preserves the design scale and uses installed Windows fonts only: English `Segoe UI Variable` → `Segoe UI`; Chinese `Microsoft YaHei UI` → `Microsoft YaHei` → `Segoe UI`; Japanese `Yu Gothic UI` → `Yu Gothic` → `Meiryo` → `Segoe UI`; each then falls back to system sans-serif. Native UI uses the matching regional UI family. Sample passages declare their own language, and the document, navigation names, controls, placeholders, switch descriptions, and close actions use the selected locale. Dropdown language names use locale-aware `Intl.DisplayNames`, with raw-code fallback for malformed legacy values.

For new user-facing strings, add one semantic English key plus reviewed Chinese/Japanese entries, including accessibility text. Use placeholders such as `{version}` rather than sentence fragments; preserve placeholder names in every locale. Store feedback keys rather than rendered strings when it must update after switching. Do not use translated text as logic/state identifiers. To add a locale, add a complete catalog, register it in React `supportedLocales`/`catalogs` and native `DesktopText.Locales`, add detection/font rules where appropriate, and extend tests and the full theme/resize review. Native resource embedding picks up the new JSON automatically.

Localization acceptance completed: 90 browser views (all five sections × three locales × dark/light × 600×600, 900×700, 1280×800 CSS sizes), with screenshots reviewed for every section/locale/theme and no horizontal overflow. Native render artifacts and subtitle self-tests were reviewed/run for each locale. Real Photino screen-reader, Windows high-contrast, cross-monitor DPI, audio hardware, and external-provider acceptance remain manual. Preserve the current row/navigation wrapping rather than shrinking CJK fonts to force English-sized labels.

## Native subtitle presentation

The real overlay retains the reliable buffered FlowLayoutPanel and per-ID blocks. Original text uses `#bac5d4` on `#171b22`, Segoe UI at the configured point size. Translation uses `#f1f4f9`, Segoe UI Semibold at size + 2 points. Entries have 18 px bottom spacing and the history panel uses 22 px horizontal / 16 px vertical padding, scaled by native DPI behavior. Long lines wrap within measured available width. No per-entry frames, timestamps, colored badges, or motion are added.

An empty window distinguishes stopped, connecting, and listening: start translation when stopped, wait for recognition while connecting, and play Windows playback audio when listening. It disappears when the first source arrives. These messages do not rebuild subtitle entries or alter history/scroll behavior. The preview is local sample content, not recognition output.

Native title bar and sizable tool-window frame remain for dependable dragging, resizing, focus, and Windows behavior. Closing still hides the overlay. Tray controls remain available when position is locked or click through is enabled.

The appearance dialog groups a live sample, text/history preferences, and window behavior. Repeated customization requests activate the existing dialog, and native ownership keeps it associated with the subtitle window. Numeric controls have explicit units and bounds. Save/Cancel stay outside the scrolling content; Enter accepts and Escape cancels. Editing is local to the dialog until Save. Font size and original/translation visibility update the sample immediately. Opacity affects the entire overlay (including text); the sample intentionally stays opaque and explains this. History is an entry limit, not a promise that all entries fit on screen.

## Accessibility and resizing

- Real buttons, names on the actual MUI comboboxes, labelled switches with associated descriptions, password fields, heading hierarchy, navigation `aria-current`, status text, and a skip-to-content link. Target-language options are unique by code, filtering a duplicate in legacy data without changing that data or provider mappings.
- Visible keyboard focus, including an explicit switch-thumb outline; no icon-only navigation or color-only state. Forced-color navigation retains a selected outline and marker. Button ripples are disabled; input/menu behavior remains native to MUI.
- Independent content scrolling keeps navigation and daily controls accessible.
- At compact widths, setting rows wrap; below 650 px navigation becomes a labelled horizontal row. This is a desktop fallback, not a mobile-first redesign.
- Neutral text pairs exceed WCAG AA normal-text contrast in the design palette. Window opacity and user font choices can reduce effective overlay readability; the dialog explains opacity.
- WinForms uses DPI autoscaling, native input controls, wrapping labels, a scrollable settings body, and persistent action buttons.
- Cross-monitor 125%/150%/175% interaction, Windows high-contrast mode, screen-reader behavior in Photino/WebView2, and very long localized labels still require manual acceptance on real displays.

## Components and ownership

- `DesktopSettings`: desktop shell, navigation, current state, supported settings pages.
- `SettingsControls`: small section, row, and switch composition helpers.
- `CredentialEditor`: masked field, explicit actions, storage status, and feedback presentation.
- `useCredentialDrafts` / `credentialState`: session-owned drafts, request-correlated acknowledgements, timeout cleanup, and retry behavior shared by translation and speech credentials.
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

Fourteen frontend tests exercise credential acknowledgements, failed saves, unsubscribe behavior, subtitle controls, request identity, shared/separate drafts, retry/late acknowledgements, timeout handling, replacement drafts during removal, localization completeness/fallback, and UI-only configuration updates without browser/network/hardware access. Eighteen .NET regressions cover the existing non-hardware logic plus catalog loading and persisted locale independence. Native self-tests cover the existing IDs/history/manual-scroll/bottom-follow rules plus state-specific empty messages, preview updates, locale switching, and minimum-size wrapping/no horizontal history scrollbar.

For isolated browser review, run Vite and visit `http://127.0.0.1:1420/?preview`. Add `&theme=light` for light mode or `&save-error` to simulate save failure. This development-only fixture has no real credentials, network calls, or persisted settings and is removed from production builds. It is not evidence of provider connectivity.

Set `DESKTOP_TRANSLATOR_TEST_ARTIFACTS` to an ignored local directory before the subtitle self-test to generate native render images for inspection. It does not change user subtitle preferences. DrawToBitmap artifacts can differ from actual compositor/chrome rendering; they complement, rather than replace, manual review.

## Known platform compromises and follow-up

The native appearance dialog uses Windows system control colors, so it may be light while React settings use dark mode. No custom WinForms dark control framework or fake frameless chrome is introduced. Photino wraps a web frontend rather than WinUI; native caption/control rendering and WebView accessibility need acceptance testing. The whole-window opacity setting cannot provide independent background-only alpha with the existing WinForms design.

The resumed review connected to the local development preview. All five sections were checked in dark and light themes at 600×600, 720×600, 900×600, and 1280×800 CSS viewports, with no document or content horizontal overflow. Screenshots confirmed compact navigation and desktop hierarchy. Provider-specific forms, language direction, transcription-only, Start/Stop status, shared Groq drafts across sections, acknowledged saves, simulated failure retention, and native-customization guidance were exercised. Keyboard review confirmed select names and Tab focus on switches. This fixture does not verify Photino host integration, external services, real secure storage, screen readers, or mixed-monitor DPI behavior. Native self-tests pass; real compositor/chrome acceptance remains manual.

Next design work should prioritize real-device mixed-DPI and keyboard/accessibility acceptance, native-speaker review of translated copy, and a future synchronized appearance API only if consolidating native controls into React demonstrates a clear benefit. Avoid adding monitoring charts or optional provider forms to solve presentation gaps.

## Files changed in the initial design phase

Frontend: `UserInterface/index.html`, `UserInterface/package.json`, `src/main.tsx`, `src/page.tsx`, `src/pages/DesktopSettings.tsx`, `src/pages/desktop.css`, `src/components/desktop/theme.ts`, `src/components/desktop/SettingsControls.tsx`, `src/components/desktop/CredentialEditor.tsx`, `src/util/photino.ts`, `src/vite-env.d.ts`, `src/dev/preview.tsx`, and `tests/desktop-bridge.test.mjs` (all under `KikitanTranslator.Photino`).

Settings integration only: `KikitanTranslator.Photino/Handlers/Control.cs`, `Handlers/UpdateConfig.cs`, and `Manager.cs`. These changes add the appearance action and durable, correlated save feedback, without modifying capture/recognition/translation logic.

Native presentation: `KikitanTranslator.Subtitles/SubtitleWindow.cs` and new `SubtitleAppearanceDialog.cs` (including shared palette and preview self-checks).

Documentation/build verification: `README.md`, `docs/UI_DESIGN.md`, `docs/MANUAL_TESTS.md`, and `scripts/build-release.ps1`.

## Resumed review changes

Retained the initial information architecture, palette, spacing, typography, and native subtitle renderer. Refined `DesktopSettings.tsx`, `CredentialEditor.tsx`, `SettingsControls.tsx`, `theme.ts`, and `desktop.css` for credential-session ownership, actual combobox names, switch descriptions/focus, forced-color navigation, quiet buttons, informational/error semantics, and unique target options. Added `credentialState.ts`, `useCredentialDrafts.ts`, and `tests/credential-state.test.mjs`. Updated `UserInterface/index.html` with `public/desktop-translator.svg`, using the same existing Material Outlined caption icon as the header. Legacy icon assets remain for compatibility. Updated `SubtitleWindow.cs` only for state-specific empty copy and corresponding self-tests, plus these design/acceptance documents. No dependency, pipeline, transport, publish-script, or packaging changes were needed in this continuation.

## Packaged product review (2026-10-02)

The local Windows x64 alpha.5 package was launched with `--start-stopped --settings` and inspected through the real Photino window, not a preview fixture. General's interface-language menu visibly contained English, 简体中文 and 日本語. Chinese → English → Japanese updated the real UI and saved `ui_language` while keeping English → Chinese audio/translation direction. Japanese survived a full application restart; the native subtitle waiting message was Japanese as well. English light-theme screenshots covered Translation, Speech recognition, Subtitles, General and About; Chinese dark and Japanese light settings were also checked. The existing design system was retained without another visual redesign. See PERFORMANCE.md for the measured stopped-process behavior and remaining manual acceptance limits.
