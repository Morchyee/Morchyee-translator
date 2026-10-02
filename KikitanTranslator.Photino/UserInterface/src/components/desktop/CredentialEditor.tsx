import {Alert, Button, Stack, TextField, Typography} from "@mui/material";
import {LockOutlined} from "@mui/icons-material";
import {CredentialDraft} from "./credentialState";

// Saved secrets never enter the renderer. The session owns drafts across navigation.
export default function CredentialEditor({configured, disabled, draft, onEdit, onSave}: {
    configured: boolean; disabled: boolean; draft: CredentialDraft;
    onEdit: (value: string) => void; onSave: (value: string) => void;
}) {
    const pending = draft.requestId !== null;
    return <Stack spacing={1.5} className="credential-editor">
        <Stack direction="row" spacing={1} alignItems="center">
            <LockOutlined sx={{fontSize: 16, color: "text.secondary"}}/>
            <Typography variant="body2">{configured ? "Key stored securely for your Windows account" : "No API key saved"}</Typography>
        </Stack>
        <TextField fullWidth label="API key" type="password" autoComplete="off" value={draft.value}
            disabled={disabled || pending} onChange={e => onEdit(e.target.value)}
            inputProps={{spellCheck: false}}
            placeholder={configured ? "Enter a new key to replace the saved key" : "Paste your provider API key"}
            helperText="Drafts stay while settings are open. Save to keep a key securely."
            onKeyDown={e => {if (e.key === "Enter" && draft.value.trim() && !pending && !disabled) onSave(draft.value.trim());}}/>
        <Stack direction="row" spacing={1} flexWrap="wrap" useFlexGap>
            <Button variant="contained" disabled={!draft.value.trim() || disabled || pending} onClick={() => onSave(draft.value.trim())}>{pending ? "Saving…" : "Save key"}</Button>
            <Button disabled={!configured || disabled || pending} onClick={() => onSave("")}>Remove saved key</Button>
        </Stack>
        {draft.message && <Alert severity={draft.severity} role="status">{draft.message}</Alert>}
        <Typography variant="body2" color="text.secondary">Saving confirms storage, not provider access.</Typography>
    </Stack>;
}
