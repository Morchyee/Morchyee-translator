import {useEffect, useMemo, useRef, useState} from "react";
import {Alert, Box, Button, MenuItem, Select, ThemeProvider, Typography} from "@mui/material";
import {ArrowForwardOutlined, ClosedCaptionOutlined, InfoOutlined, MicNoneOutlined, PlayArrowOutlined,
    SettingsOutlined, StopOutlined, TranslateOutlined, VolumeUpOutlined} from "@mui/icons-material";
import {app_state, langSource, langTo} from "../util/constants";
import {controlKikitan, registerNotificationCallback, setConfig, showSubtitleAppearance, showSubtitles} from "../util/photino";
import {useCredentialDrafts} from "../components/desktop/useCredentialDrafts";
import CredentialEditor from "../components/desktop/CredentialEditor";
import {Section, SettingRow, ToggleRow} from "../components/desktop/SettingsControls";
import {desktopTheme} from "../components/desktop/theme";
import "./desktop.css";
import {LocaleProvider, useLocale} from "../i18n/LocaleProvider";
import {Translate, supportedLocales, errorKey, knownMessageKey} from "../i18n/localization";

const makePages = (t: Translate) => [
    {id: "translation", label: t("navigation.translation"), icon: TranslateOutlined, description: t("translation.description")},
    {id: "speech", label: t("navigation.speech"), icon: MicNoneOutlined, description: t("speech.description")},
    {id: "subtitles", label: t("navigation.subtitles"), icon: ClosedCaptionOutlined, description: t("subtitles.description")},
    {id: "general", label: t("navigation.general"), icon: SettingsOutlined, description: t("general.description")},
    {id: "about", label: t("navigation.about"), icon: InfoOutlined, description: t("about.description")}
];
const makeProviders = (t: Translate) => ({
    groq: {name: "Groq", field: "groq_api_key", help: t("translation.groqHelp")},
    google: {name: "Google Cloud Translation", field: "google_cloud_api_key", help: t("translation.googleHelp")},
    deepl: {name: "DeepL API", field: "deepl_api_key", help: t("translation.deeplHelp")}
});
// Legacy language data contains repeated codes; show each target once in desktop settings.
const targetLanguages = langTo.filter((language, index) => langTo.findIndex(item => item.code === language.code) === index);

export default function DesktopSettings({state}: {state: app_state}) {
    return <LocaleProvider locale={state.config.ui_language}><DesktopSettingsContent state={state}/></LocaleProvider>;
}

function DesktopSettingsContent({state}: {state: app_state}) {
    const {t, locale} = useLocale();
    const pages = makePages(t);
    const providers = makeProviders(t);
    const languageNames = useMemo(() => new Intl.DisplayNames([locale], {type: "language"}), [locale]);
    const languageName = (code: string) => {
        try { return languageNames.of(code) || code; }
        catch { return code; } // Preserve diagnostics for a malformed legacy setting without crashing the UI.
    };
    const [page, setPage] = useState("translation");
    const [error, setError] = useState("");
    const [noticeSeverity, setNoticeSeverity] = useState<"info" | "warning">("warning");
    const [failures, setFailures] = useState(0);
    const main = useRef<HTMLElement>(null);
    const credentials = useCredentialDrafts(failures);
    const c = state.config;
    const theme = useMemo(() => desktopTheme(c.light_mode, locale), [c.light_mode, locale]);
    const provider = providers[c.desktop_translation_provider] || providers.groq;
    const configured = c[(provider.field + "_configured") as keyof typeof c] === true;
    const current = pages.find(p => p.id === page)!;
    const source = languageName(c.source_language);
    const target = languageName(c.target_language);
    const recognition = ["Bing", "Groq Whisper", "Gemini Live"][c.recognizer] || t("common.unknownProvider");
    const disabled = !!state.configuration_error;
    const noticeKey = knownMessageKey(error);
    useEffect(() => registerNotificationCallback((msg, level) => {
        setError(msg);
        setNoticeSeverity(level === 0 ? "info" : "warning");
        if (level >= 2) setFailures(n => n + 1);
    }), []);
    function credential(field: string, isConfigured: boolean) {
        return <CredentialEditor configured={isConfigured} disabled={disabled} draft={credentials.get(field)}
            onEdit={value => credentials.edit(field, value)} onSave={value => credentials.save(field, value)}/>;
    }
    useEffect(() => { if (main.current) main.current.scrollTop = 0; }, [page]);
    return <ThemeProvider theme={theme}><Box className="desktop-app" lang={locale} data-theme={c.light_mode ? "light" : "dark"} sx={{bgcolor: "background.default", color: "text.primary"}}>
        <a className="skip-link" href="#desktop-main">{t("common.skip")}</a>
        <header className="desktop-header">
            <div className="product-name"><ClosedCaptionOutlined sx={{color: "primary.main", fontSize: 25}}/><span>Desktop Translator</span></div>
            <div className="header-actions"><span className="translator-state" role="status"><i data-state={state.status}/>{[t("common.stopped"), t("common.connecting"), t("common.listening")][state.status]}</span>
                <Button variant={state.status === 0 ? "contained" : "outlined"} disabled={disabled && state.status === 0}
                    startIcon={state.status === 0 ? <PlayArrowOutlined/> : <StopOutlined/>} onClick={() => controlKikitan(state.status === 0)}>
                    {state.status === 0 ? t("common.start") : t("common.stop")}</Button>
                <Button variant="outlined" onClick={showSubtitles}>{t("common.showSubtitles")}</Button></div>
        </header>
        <aside className="desktop-sidebar"><nav aria-label={t("common.sections")}>{pages.map(p => <button type="button" key={p.id} className={page === p.id ? "selected" : ""}
            aria-current={page === p.id ? "page" : undefined} onClick={() => setPage(p.id)}><p.icon fontSize="small"/><span>{p.label}</span></button>)}</nav>
            <div className="sidebar-footer"><VolumeUpOutlined fontSize="small"/><span>{t("speech.systemAudio")}<br/><small>{t("speech.defaultDevice")}</small></span></div>
        </aside>
        <main id="desktop-main" tabIndex={-1} ref={main} className="desktop-main"><div className="settings-content">
            <div className="page-heading"><Typography component="h1" variant="h1">{current.label}</Typography><Typography color="text.secondary">{current.description}</Typography></div>
            {(error || state.configuration_error) && <Alert severity={state.configuration_error ? "error" : noticeSeverity} closeText={t("common.close")} onClose={state.configuration_error ? undefined : () => setError("")} sx={{mb: 3}}>{state.configuration_error ? t("errors.configuration") : noticeKey ? t(noticeKey) : noticeSeverity === "info" ? error : t(errorKey(error))}{(state.configuration_error || noticeSeverity !== "info") && <details><summary>{t("errors.details")}</summary>{state.configuration_error || error}</details>}</Alert>}
            <div className="session-summary" aria-label={t("common.configuration")}><span>{source} <ArrowForwardOutlined sx={{fontSize: 14}}/> {target}</span><span>{recognition} · {c.speech_to_text_only ? t("translation.transcription") : provider.name}</span></div>
            {page === "translation" && <>
                <Section title={t("translation.languages")}><div className="language-direction"><div><label id="source-label">{t("translation.source")}</label><Select fullWidth labelId="source-label" inputProps={{"aria-describedby": "source-help"}} disabled={disabled} value={c.source_language} onChange={e => setConfig("source_language", e.target.value)}>{langSource.map(l => <MenuItem key={l.code} value={l.code}>{languageName(l.code)}</MenuItem>)}</Select><Typography id="source-help" variant="body2" color="text.secondary">{t("translation.sourceHelp")}</Typography></div>
                    <ArrowForwardOutlined className="language-arrow" sx={{color: "text.secondary"}}/>
                    <div><label id="target-label">{t("translation.target")}</label><Select fullWidth labelId="target-label" inputProps={{"aria-describedby": "target-help"}} disabled={disabled} value={c.target_language} onChange={e => setConfig("target_language", e.target.value)}>{targetLanguages.map(l => <MenuItem key={l.code} value={l.code}>{languageName(l.code)}</MenuItem>)}</Select><Typography id="target-help" variant="body2" color="text.secondary">{t("translation.targetHelp")}</Typography></div></div></Section>
                <Section title={t("translation.provider")}><SettingRow title={t("translation.providerLabel")} description={t("translation.providerHelp")} descriptionId="translation-provider-help"><Select inputProps={{"aria-label": t("translation.provider"), "aria-describedby": "translation-provider-help"}} disabled={disabled} value={c.desktop_translation_provider} onChange={e => setConfig("desktop_translation_provider", e.target.value)}>{Object.entries(providers).map(([id, p]) => <MenuItem key={id} value={id}>{p.name}</MenuItem>)}</Select></SettingRow>
                    <Typography color="text.secondary" sx={{my: 2}}>{provider.help}</Typography>
                    {credential(provider.field, configured)}
                    {!configured && !c.speech_to_text_only && <Alert severity="info" sx={{mt: 2}}>{t("translation.missingKey")}</Alert>}
                </Section>
                <Section title={t("translation.output")}><ToggleRow title={t("translation.transcription")} description={t("translation.transcriptionHelp")} checked={c.speech_to_text_only} disabled={disabled} onChange={value => setConfig("speech_to_text_only", value)}/></Section>
            </>}
            {page === "speech" && <>
                <Section title={t("speech.recognition")}><SettingRow title={t("speech.provider")} description={t("speech.providerHelp")} descriptionId="speech-provider-help"><Select inputProps={{"aria-label": t("speech.provider"), "aria-describedby": "speech-provider-help"}} disabled={disabled} value={c.recognizer} onChange={e => setConfig("recognizer", e.target.value)}><MenuItem value={0}>Bing</MenuItem><MenuItem value={1}>Groq Whisper</MenuItem>{c.recognizer === 2 && <MenuItem value={2}>Gemini Live</MenuItem>}</Select></SettingRow>
                    <Typography color="text.secondary" sx={{my: 2}}>{c.recognizer === 0 ? t("speech.bingHelp") : c.recognizer === 1 ? t("speech.groqHelp") : t("speech.geminiHelp")}</Typography>
                    {c.recognizer !== 0 && credential(c.recognizer === 1 ? "groq_api_key" : "gemini_api_key", c.recognizer === 1 ? c.groq_api_key_configured : c.gemini_api_key_configured)}
                </Section>
                <Section title={t("translation.languages")}><SettingRow title={t("speech.language")} description={t("speech.languageHelp")} descriptionId="recognition-language-help"><Select inputProps={{"aria-label": t("speech.language"), "aria-describedby": "recognition-language-help"}} disabled={disabled} value={c.source_language} onChange={e => setConfig("source_language", e.target.value)}>{langSource.map(l => <MenuItem key={l.code} value={l.code}>{languageName(l.code)}</MenuItem>)}</Select></SettingRow></Section>
                <Section title={t("speech.audioSource")}><SettingRow title={t("speech.systemAudio")} description={t("speech.captureHelp")}><VolumeUpOutlined sx={{color: "text.secondary"}}/></SettingRow><Typography color="text.secondary" sx={{mt: 2}}>{t("speech.deviceHelp")}</Typography></Section>
            </>}
            {page === "subtitles" && <>
                <Section title={t("subtitles.bilingual")}><div className="subtitle-preview" role="group" aria-label={t("subtitles.preview")}><div className="preview-original" lang="en">hello</div><div className="preview-translation" lang="zh-CN">你好</div><div className="preview-entry"><div className="preview-original" lang="en">how are you</div><div className="preview-translation" lang="zh-CN">你好吗</div></div></div><Typography variant="body2" color="text.secondary" sx={{mt: 1}}>{t("subtitles.sampleHelp")}</Typography></Section>
                <Section title={t("subtitles.appearance")}><SettingRow title={t("subtitles.customize")} description={t("subtitles.customizeHelp")}><Button variant="contained" onClick={showSubtitleAppearance}>{t("subtitles.customizeAction")}</Button></SettingRow><SettingRow title={t("subtitles.behavior")} description={t("subtitles.behaviorHelp")}><Button variant="outlined" onClick={showSubtitles}>{t("subtitles.showWindow")}</Button></SettingRow><Typography color="text.secondary" sx={{mt: 2}}>{t("subtitles.guidance")}</Typography></Section>
            </>}
            {page === "general" && <>
                <Section title={t("general.appearance")}><SettingRow title={t("general.interfaceLanguage")} description={t("general.languageHelp")} descriptionId="interface-language-help"><Select inputProps={{"aria-label": t("general.interfaceLanguage"), "aria-describedby": "interface-language-help"}} value={locale} disabled={disabled} onChange={e => setConfig("ui_language", e.target.value)}>{supportedLocales.map(item => <MenuItem key={item.id} value={item.id}>{item.name}</MenuItem>)}</Select></SettingRow><SettingRow title={t("general.theme")} description={t("general.themeHelp")} descriptionId="theme-help"><Select inputProps={{"aria-label": t("general.theme"), "aria-describedby": "theme-help"}} value={c.light_mode ? "light" : "dark"} disabled={disabled} onChange={e => setConfig("light_mode", e.target.value === "light")}><MenuItem value="dark">{t("general.dark")}</MenuItem><MenuItem value="light">{t("general.light")}</MenuItem></Select></SettingRow></Section>
                <Section title={t("general.startup")}><ToggleRow title={t("general.autoStart")} description={t("general.autoStartHelp")} checked={c.auto_start} disabled={disabled} onChange={value => setConfig("auto_start", value)}/><SettingRow title={t("general.tray")} description={t("general.trayHelp")}><Typography variant="body2" color="text.secondary">{t("general.available")}</Typography></SettingRow></Section>
                <Section title={t("general.saving")}><Typography color="text.secondary">{t("general.savingHelp")}</Typography></Section>
            </>}
            {page === "about" && <>
                <Section title="Desktop Translator"><Typography>{t("about.product")}</Typography><Typography color="text.secondary" sx={{mt: 1}}>{t("about.version", {version: state.app_version})}</Typography></Section>
                <Section title={t("about.privacy")}><Typography color="text.secondary">{t("about.privacyHelp")}</Typography><Typography color="text.secondary" sx={{mt: 2}}>{t("about.keyPrivacy")}</Typography></Section>
                <Section title={t("about.origins")}><Typography color="text.secondary">{t("about.originsHelp")}</Typography></Section>
            </>}
            <footer className="content-footer">{t("common.footer")}</footer>
        </div></main>
    </Box></ThemeProvider>;
}
