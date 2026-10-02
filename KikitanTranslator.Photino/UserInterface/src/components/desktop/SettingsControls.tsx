import {ReactNode, useId} from "react";
import {Box, Switch, Typography} from "@mui/material";

export function SettingRow({title, description, descriptionId, children}: {title: string; description: string; descriptionId?: string; children: ReactNode}) {
    return <div className="setting-row"><Box><Typography fontWeight={600}>{title}</Typography>
        <Typography id={descriptionId} variant="body2" color="text.secondary">{description}</Typography></Box><div className="setting-control">{children}</div></div>;
}
export function ToggleRow({title, description, checked, onChange, disabled}: {
    title: string; description: string; checked: boolean; onChange: (checked: boolean) => void; disabled?: boolean;
}) {
    const descriptionId = useId();
    return <SettingRow title={title} description={description} descriptionId={descriptionId}><Switch inputProps={{"aria-label": title, "aria-describedby": descriptionId}} checked={checked} disabled={disabled} onChange={e => onChange(e.target.checked)}/></SettingRow>;
}
export function Section({title, children}: {title: string; children: ReactNode}) {
    return <section className="settings-section"><Typography component="h2" variant="h2">{title}</Typography>{children}</section>;
}
