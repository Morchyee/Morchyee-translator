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

const pages = [
    {id: "translation", label: "Translation", icon: TranslateOutlined, description: "Choose what you hear and the language you want to read."},
    {id: "speech", label: "Speech recognition", icon: MicNoneOutlined, description: "Turn system audio into source subtitles."},
    {id: "subtitles", label: "Subtitles", icon: ClosedCaptionOutlined, description: "Keep the words clear and the window out of your way."},
    {id: "general", label: "General", icon: SettingsOutlined, description: "Make Desktop Translator fit your daily routine."},
    {id: "about", label: "About", icon: InfoOutlined, description: "A quiet companion for audio across your desktop."}
];
const providers = {
    groq: {name: "Groq", field: "groq_api_key", help: "Uses your Groq API account. The same key can also be used for Groq speech recognition."},
    google: {name: "Google Cloud Translation", field: "google_cloud_api_key", help: "Enable the Cloud Translation API in your Google Cloud project. Billing and API access are managed by Google."},
    deepl: {name: "DeepL API", field: "deepl_api_key", help: "Use a key from a DeepL API Free or Pro plan. A regular DeepL subscription does not include API access."}
};
// Legacy language data contains repeated codes; show each target once in desktop settings.
const targetLanguages = langTo.filter((language, index) => langTo.findIndex(item => item.code === language.code) === index);

export default function DesktopSettings({state}: {state: app_state}) {
    const [page, setPage] = useState("translation");
    const [error, setError] = useState("");
    const [noticeSeverity, setNoticeSeverity] = useState<"info" | "warning">("warning");
    const [failures, setFailures] = useState(0);
    const main = useRef<HTMLElement>(null);
    const credentials = useCredentialDrafts(failures);
    const c = state.config;
    const theme = useMemo(() => desktopTheme(c.light_mode), [c.light_mode]);
    const provider = providers[c.desktop_translation_provider] || providers.groq;
    const configured = c[(provider.field + "_configured") as keyof typeof c] === true;
    const current = pages.find(p => p.id === page)!;
    const source = langSource.find(l => l.code === c.source_language)?.name.en || c.source_language;
    const target = langTo.find(l => l.code === c.target_language)?.name.en || c.target_language;
    const recognition = ["Bing", "Groq Whisper", "Gemini Live"][c.recognizer] || "Unknown provider";
    const disabled = !!state.configuration_error;
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
    return <ThemeProvider theme={theme}><Box className="desktop-app" data-theme={c.light_mode ? "light" : "dark"} sx={{bgcolor: "background.default", color: "text.primary"}}>
        <a className="skip-link" href="#desktop-main">Skip to settings</a>
        <header className="desktop-header">
            <div className="product-name"><ClosedCaptionOutlined sx={{color: "primary.main", fontSize: 25}}/><span>Desktop Translator</span></div>
            <div className="header-actions"><span className="translator-state" role="status"><i data-state={state.status}/>{["Stopped", "Connecting…", "Listening"][state.status]}</span>
                <Button variant={state.status === 0 ? "contained" : "outlined"} disabled={disabled && state.status === 0}
                    startIcon={state.status === 0 ? <PlayArrowOutlined/> : <StopOutlined/>} onClick={() => controlKikitan(state.status === 0)}>
                    {state.status === 0 ? "Start translation" : "Stop translation"}</Button>
                <Button variant="outlined" onClick={showSubtitles}>Show subtitles</Button></div>
        </header>
        <aside className="desktop-sidebar"><nav aria-label="Settings sections">{pages.map(p => <button type="button" key={p.id} className={page === p.id ? "selected" : ""}
            aria-current={page === p.id ? "page" : undefined} onClick={() => setPage(p.id)}><p.icon fontSize="small"/><span>{p.label}</span></button>)}</nav>
            <div className="sidebar-footer"><VolumeUpOutlined fontSize="small"/><span>Windows system audio<br/><small>Default playback device</small></span></div>
        </aside>
        <main id="desktop-main" tabIndex={-1} ref={main} className="desktop-main"><div className="settings-content">
            <div className="page-heading"><Typography component="h1" variant="h1">{current.label}</Typography><Typography color="text.secondary">{current.description}</Typography></div>
            {(error || state.configuration_error) && <Alert severity={state.configuration_error ? "error" : noticeSeverity} onClose={state.configuration_error ? undefined : () => setError("")} sx={{mb: 3}}>{state.configuration_error || error}</Alert>}
            <div className="session-summary" aria-label="Current configuration"><span>{source} <ArrowForwardOutlined sx={{fontSize: 14}}/> {target}</span><span>{recognition} · {c.speech_to_text_only ? "Transcription only" : provider.name}</span></div>
            {page === "translation" && <>
                <Section title="Languages"><div className="language-direction"><div><label id="source-label">Source language</label><Select fullWidth labelId="source-label" disabled={disabled} value={c.source_language} onChange={e => setConfig("source_language", e.target.value)}>{langSource.map(l => <MenuItem key={l.code} value={l.code}>{l.name.en}</MenuItem>)}</Select><Typography variant="body2" color="text.secondary">The language spoken in your audio</Typography></div>
                    <ArrowForwardOutlined className="language-arrow" sx={{color: "text.secondary"}}/>
                    <div><label id="target-label">Target language</label><Select fullWidth labelId="target-label" disabled={disabled} value={c.target_language} onChange={e => setConfig("target_language", e.target.value)}>{targetLanguages.map(l => <MenuItem key={l.code} value={l.code}>{l.name.en}</MenuItem>)}</Select><Typography variant="body2" color="text.secondary">The language shown in translation</Typography></div></div></Section>
                <Section title="Translation provider"><SettingRow title="Provider" description="Translate recognized text using your own API account."><Select inputProps={{"aria-label": "Translation provider"}} disabled={disabled} value={c.desktop_translation_provider} onChange={e => setConfig("desktop_translation_provider", e.target.value)}>{Object.entries(providers).map(([id, p]) => <MenuItem key={id} value={id}>{p.name}</MenuItem>)}</Select></SettingRow>
                    <Typography color="text.secondary" sx={{my: 2}}>{provider.help}</Typography>
                    {credential(provider.field, configured)}
                    {!configured && !c.speech_to_text_only && <Alert severity="info" sx={{mt: 2}}>Add an API key to enable translation. Recognized source text will still appear if translation is unavailable.</Alert>}
                </Section>
                <Section title="Output"><ToggleRow title="Transcription only" description="Show recognized speech without requesting translations." checked={c.speech_to_text_only} disabled={disabled} onChange={value => setConfig("speech_to_text_only", value)}/></Section>
            </>}
            {page === "speech" && <>
                <Section title="Recognition"><SettingRow title="Speech recognition provider" description="Recognizes the source language selected in Translation."><Select inputProps={{"aria-label": "Speech recognition provider"}} disabled={disabled} value={c.recognizer} onChange={e => setConfig("recognizer", e.target.value)}><MenuItem value={0}>Bing</MenuItem><MenuItem value={1}>Groq Whisper</MenuItem>{c.recognizer === 2 && <MenuItem value={2}>Gemini Live</MenuItem>}</Select></SettingRow>
                    <Typography color="text.secondary" sx={{my: 2}}>{c.recognizer === 0 ? "Bing uses the existing speech service and does not require your own API key. Internet access is required." : c.recognizer === 1 ? "Groq Whisper requires a Groq API key. Speech and translation share the same saved Groq credential." : "This existing combined speech and translation mode uses Gemini Live. Select Bing or Groq to use the standard translation providers."}</Typography>
                    {c.recognizer !== 0 && credential(c.recognizer === 1 ? "groq_api_key" : "gemini_api_key", c.recognizer === 1 ? c.groq_api_key_configured : c.gemini_api_key_configured)}
                </Section>
                <Section title="Audio source"><SettingRow title="Windows system audio" description="Captures audio from your default Windows playback device."><VolumeUpOutlined sx={{color: "text.secondary"}}/></SettingRow><Typography color="text.secondary" sx={{mt: 2}}>Play a video, call, or game through your speakers or headphones. If audio stops after a device change, stop and restart translation.</Typography></Section>
            </>}
            {page === "subtitles" && <>
                <Section title="Bilingual presentation"><div className="subtitle-preview" aria-label="Illustrative subtitle preview"><div className="preview-original">A little clarity makes all the difference.</div><div className="preview-translation" lang="ja">少しの明瞭さが、大きな違いを生みます。</div><div className="preview-entry"><div className="preview-original">Keep listening. The words will follow.</div><div className="preview-translation" lang="ja">聞き続けてください。言葉がついてきます。</div></div></div><Typography variant="body2" color="text.secondary" sx={{mt: 1}}>Layout example · local sample text, independent of your current languages and saved appearance.</Typography></Section>
                <Section title="Window appearance"><SettingRow title="Customize subtitles" description="Font size, opacity, history, original and translated text."><Button variant="contained" onClick={showSubtitleAppearance}>Customize…</Button></SettingRow><SettingRow title="Window behavior" description="Always on top, click through, and position locking are in the same customization window."><Button variant="outlined" onClick={showSubtitles}>Show window</Button></SettingRow><Typography color="text.secondary" sx={{mt: 2}}>The customization window includes a live preview. Changes are saved on this computer. Drag and resize the subtitle window normally; use the tray menu to unlock it or turn off click through.</Typography></Section>
            </>}
            {page === "general" && <>
                <Section title="Appearance"><SettingRow title="Settings theme" description="Choose a comfortable background for this settings window."><Select inputProps={{"aria-label": "Settings theme"}} value={c.light_mode ? "light" : "dark"} disabled={disabled} onChange={e => setConfig("light_mode", e.target.value === "light")}><MenuItem value="dark">Dark</MenuItem><MenuItem value="light">Light</MenuItem></Select></SettingRow></Section>
                <Section title="Startup and tray"><ToggleRow title="Start translation when the app opens" description="Begin listening immediately when you launch Desktop Translator." checked={c.auto_start} disabled={disabled} onChange={value => setConfig("auto_start", value)}/><SettingRow title="Keep translating in the tray" description="Closing settings leaves the translator running. Use Exit in the tray menu to quit."><Typography variant="body2" color="text.secondary">Always available</Typography></SettingRow></Section>
                <Section title="Saving settings"><Typography color="text.secondary">Selections and switches save automatically. API keys save only when you choose Save key. Changing language or provider while listening restarts the active session.</Typography></Section>
            </>}
            {page === "about" && <>
                <Section title="Desktop Translator"><Typography>A Windows desktop companion for real-time bilingual subtitles from videos, calls, games, and live streams.</Typography><Typography color="text.secondary" sx={{mt: 1}}>Version {state.app_version} · Windows x64</Typography></Section>
                <Section title="Privacy"><Typography color="text.secondary">Audio capture and speech detection happen on your computer. Speech audio is sent to your recognition provider; recognized text is sent to your translation provider. Provider accounts, charges, and retention policies are managed by those services.</Typography><Typography color="text.secondary" sx={{mt: 2}}>Saved API keys are protected by Windows for your user account. They are never displayed in this settings window.</Typography></Section>
                <Section title="Origins"><Typography color="text.secondary">Based on the open-source Kikitan Translator project. Desktop Translator keeps its proven recognition workflow and adds a standalone Windows subtitle experience.</Typography></Section>
            </>}
            <footer className="content-footer">Close settings to return to your audio. Controls stay available in the system tray.</footer>
        </div></main>
    </Box></ThemeProvider>;
}
