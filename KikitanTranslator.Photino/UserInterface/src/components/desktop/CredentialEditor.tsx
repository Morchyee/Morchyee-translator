import {Alert, Button, Stack, TextField, Typography} from "@mui/material";
import {LockOutlined} from "@mui/icons-material";
import {useLocale} from "../../i18n/LocaleProvider";
import {CredentialDraft} from "./credentialState";

// Saved secrets never enter the renderer. The session owns drafts across navigation.
export default function CredentialEditor({configured, disabled, draft, onEdit, onSave}: {
    configured: boolean; disabled: boolean; draft: CredentialDraft;
    onEdit: (value: string) => void; onSave: (value: string) => void;
}) {
    const {t} = useLocale();
    const pending = draft.requestId !== null;
    return <Stack spacing={1.5} className="credential-editor">
        <Stack direction="row" spacing={1} alignItems="center">
            <LockOutlined sx={{fontSize: 16, color: "text.secondary"}}/>
            <Typography variant="body2">{configured ? t("credential.stored") : t("credential.missing")}</Typography>
        </Stack>
        <TextField fullWidth label={t("credential.key")} type="password" autoComplete="off" value={draft.value}
            disabled={disabled || pending} onChange={e => onEdit(e.target.value)}
            inputProps={{spellCheck: false}}
            placeholder={configured ? t("credential.replace") : t("credential.paste")}
            helperText={t("credential.draftHelp")}
            onKeyDown={e => {if (e.key === "Enter" && draft.value.trim() && !pending && !disabled) onSave(draft.value.trim());}}/>
        <Stack direction="row" spacing={1} flexWrap="wrap" useFlexGap>
            <Button variant="contained" disabled={!draft.value.trim() || disabled || pending} onClick={() => onSave(draft.value.trim())}>{pending ? t("credential.saving") : t("credential.save")}</Button>
            <Button disabled={!configured || disabled || pending} onClick={() => onSave("")}>{t("credential.remove")}</Button>
        </Stack>
        {draft.message && <Alert severity={draft.severity} role="status">{t(draft.message)}</Alert>}
        <Typography variant="body2" color="text.secondary">{t("credential.storageHelp")}</Typography>
    </Stack>;
}
