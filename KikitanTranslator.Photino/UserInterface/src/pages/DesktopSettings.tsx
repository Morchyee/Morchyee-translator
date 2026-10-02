import {useEffect, useState} from "react";
import {Alert, Box, Button, Checkbox, FormControlLabel, MenuItem, Select, Stack, TextField, Typography} from "@mui/material";
import {app_state, langSource, langTo} from "../util/constants";
import {controlKikitan, registerNotificationCallback, setConfig, showSubtitles} from "../util/photino";

export default function DesktopSettings({state}: {state: app_state}) {
    const [draftKey, setDraftKey] = useState("");
    const [error, setError] = useState("");
    const c = state.config;
    const keyField = c.desktop_translation_provider === "google" ? "google_cloud_api_key" :
        c.desktop_translation_provider === "deepl" ? "deepl_api_key" : "groq_api_key";
    const configured = c[keyField + "_configured" as keyof typeof c] === true;
    useEffect(() => { registerNotificationCallback(msg => setError(msg)); }, []);
    useEffect(() => { setDraftKey(""); }, [keyField]);
    const muted = "#b8c4d3";
    return <Box sx={{minHeight: "100vh", background: "#111827", color: "#f3f4f6", p: 4,
        "& .MuiInputBase-root": {color: "#f3f4f6"}, "& .MuiInputLabel-root": {color: muted},
        "& .MuiOutlinedInput-notchedOutline": {borderColor: "#64748b"}}}>
        <Stack spacing={3} sx={{maxWidth: 780, mx: "auto"}}>
            <Box><Typography variant="h4">Desktop Translator</Typography>
                <Typography sx={{color: muted}}>System audio → speech recognition → bilingual subtitles</Typography></Box>
            <Stack direction="row" spacing={2} alignItems="center">
                <Button variant="contained" onClick={() => controlKikitan(state.status === 0)}>
                    {state.status === 0 ? "Start translation" : "Stop translation"}</Button>
                <Button variant="outlined" onClick={showSubtitles}>Show subtitles</Button>
                <Typography>{["Stopped", "Connecting…", "Listening"][state.status]}</Typography>
            </Stack>
            {(error || state.configuration_error) && <Alert severity="warning" onClose={() => setError("")}>{state.configuration_error || error}</Alert>}
            <Stack direction="row" spacing={2}>
                <Box sx={{flex: 1}}><Typography id="source-label">Source language</Typography>
                    <Select fullWidth aria-labelledby="source-label" value={c.source_language} onChange={e => setConfig("source_language", e.target.value)}>
                        {langSource.map(l => <MenuItem key={l.code} value={l.code}>{l.name.en}</MenuItem>)}
                    </Select></Box>
                <Box sx={{flex: 1}}><Typography id="target-label">Target language</Typography>
                    <Select fullWidth aria-labelledby="target-label" value={c.target_language} onChange={e => setConfig("target_language", e.target.value)}>
                        {langTo.map(l => <MenuItem key={l.code} value={l.code}>{l.name.en}</MenuItem>)}
                    </Select></Box>
            </Stack>
            <Box><Typography id="speech-label">Speech recognition provider</Typography>
                <Select fullWidth aria-labelledby="speech-label" value={c.recognizer} onChange={e => setConfig("recognizer", e.target.value)}>
                    <MenuItem value={0}>Bing (existing alpha service)</MenuItem><MenuItem value={1}>Groq Whisper (API key required)</MenuItem>
                    {c.recognizer === 2 && <MenuItem value={2}>Gemini Live (legacy combined mode)</MenuItem>}
                </Select>
                {c.recognizer === 1 && c.desktop_translation_provider !== "groq" && <TextField fullWidth type="password" label="Groq speech API key" placeholder={c.groq_api_key_configured ? "Saved securely — enter to replace" : "Required for speech recognition"} onBlur={e => {if (e.target.value.trim()) {setConfig("groq_api_key", e.target.value.trim()); e.target.value = "";}}} sx={{mt: 2}}/>}
            </Box>
            <Box><Typography id="provider-label">Translation provider</Typography>
                <Select fullWidth aria-labelledby="provider-label" value={c.desktop_translation_provider} onChange={e => setConfig("desktop_translation_provider", e.target.value)}>
                    <MenuItem value="groq">Groq</MenuItem><MenuItem value="google">Google Cloud Translation</MenuItem><MenuItem value="deepl">DeepL API</MenuItem>
                </Select></Box>
            <Stack spacing={1}>
                <TextField fullWidth type="password" label="Translation provider API key" autoComplete="off" value={draftKey} onChange={e => setDraftKey(e.target.value)}
                    placeholder={configured ? "Saved securely — enter a key to replace" : "Enter your provider API key"}/>
                <Stack direction="row" spacing={2}>
                    <Button variant="contained" disabled={!draftKey.trim() || !!state.configuration_error} onClick={() => {setConfig(keyField, draftKey.trim()); setDraftKey("");}}>Save key</Button>
                    <Button disabled={!configured || !!state.configuration_error} onClick={() => setConfig(keyField, "")}>Remove saved key</Button>
                </Stack>
                {!configured && !c.speech_to_text_only && <Alert severity="info">No translation API key saved. Source subtitles still work; add a key to enable translation.</Alert>}
                <Typography sx={{color: muted}}>Google requires Cloud Translation API access. DeepL requires an API plan. Windows protects saved keys for your user account.</Typography>
            </Stack>
            <Box>
                <FormControlLabel control={<Checkbox checked={c.speech_to_text_only} onChange={e => setConfig("speech_to_text_only", e.target.checked)}/>} label="Transcription only"/>
                <FormControlLabel control={<Checkbox checked={c.auto_start} onChange={e => setConfig("auto_start", e.target.checked)}/>} label="Start translation when the app launches"/>
            </Box>
            <Box><Typography variant="h6">Subtitle appearance</Typography>
                <Typography sx={{color: muted}}>Use the tray menu → Subtitle appearance to adjust font size, opacity, history, always on top, click through and position locking. Drag and resize the subtitle window normally.</Typography></Box>
            <Typography sx={{color: muted}} variant="body2">Audio is captured locally; speech audio and recognized text are sent to your selected cloud providers. Close this settings window to keep translating in the tray. Version {state.app_version}.</Typography>
        </Stack>
    </Box>;
}
