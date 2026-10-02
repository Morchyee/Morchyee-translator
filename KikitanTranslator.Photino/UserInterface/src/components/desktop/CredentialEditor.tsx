import {useEffect, useRef, useState} from "react";
import {Alert, Button, Stack, TextField, Typography} from "@mui/material";
import {LockOutlined} from "@mui/icons-material";
import {registerConfigSavedCallback, setConfig} from "../../util/photino";

// Saved credentials never enter the renderer. A draft is cleared only on a save acknowledgement.
export default function CredentialEditor({field, configured, disabled, failures}: {
    field: string; configured: boolean; disabled: boolean; failures: number;
}) {
    const [draft, setDraft] = useState("");
    const [pending, setPending] = useState(false);
    const [feedback, setFeedback] = useState("");
    const activeRequest = useRef<number | null>(null);
    useEffect(() => registerConfigSavedCallback((savedField, requestId) => {
        if (savedField !== field || requestId !== activeRequest.current) return;
        activeRequest.current = null;
        setDraft(""); setPending(false); setFeedback("Credential settings saved.");
    }), [field]);
    useEffect(() => { activeRequest.current = null; setPending(false); }, [failures]);
    useEffect(() => {
        if (!pending) return;
        const timer = window.setTimeout(() => {
            activeRequest.current = null;
            setPending(false); setFeedback("Save was not confirmed. Your draft is still here; try again.");
        }, 10000);
        return () => window.clearTimeout(timer);
    }, [pending]);
    function save(value: string) { setFeedback(""); setPending(true); activeRequest.current = setConfig(field, value); }
    return <Stack spacing={1.5} className="credential-editor">
        <Stack direction="row" spacing={1} alignItems="center">
            <LockOutlined sx={{fontSize: 16, color: "text.secondary"}}/>
            <Typography variant="body2">{configured ? "Key stored securely for your Windows account" : "No API key saved"}</Typography>
        </Stack>
        <TextField fullWidth label="API key" type="password" autoComplete="new-password" value={draft}
            disabled={disabled || pending} onChange={e => {setDraft(e.target.value); setFeedback("");}}
            placeholder={configured ? "Enter a new key to replace the saved key" : "Paste your provider API key"}
            helperText="Saved keys stay hidden. Saving a key does not verify provider access."
            onKeyDown={e => {if (e.key === "Enter" && draft.trim() && !pending && !disabled) save(draft.trim());}}/>
        <Stack direction="row" spacing={1} flexWrap="wrap" useFlexGap>
            <Button variant="contained" disabled={!draft.trim() || disabled || pending} onClick={() => save(draft.trim())}>{pending ? "Saving…" : "Save key"}</Button>
            <Button disabled={!configured || disabled || pending} onClick={() => save("")}>Remove saved key</Button>
        </Stack>
        {feedback && <Alert severity={feedback.startsWith("Save was") ? "warning" : "success"} role="status">{feedback}</Alert>}
    </Stack>;
}
