import {ReactNode} from "react";
import {Box, Switch, Typography} from "@mui/material";

export function SettingRow({title, description, children}: {title: string; description: string; children: ReactNode}) {
    return <div className="setting-row"><Box><Typography fontWeight={600}>{title}</Typography>
        <Typography variant="body2" color="text.secondary">{description}</Typography></Box><div className="setting-control">{children}</div></div>;
}
export function ToggleRow({title, description, checked, onChange, disabled}: {
    title: string; description: string; checked: boolean; onChange: (checked: boolean) => void; disabled?: boolean;
}) {
    return <SettingRow title={title} description={description}><Switch inputProps={{"aria-label": title}} checked={checked} disabled={disabled} onChange={e => onChange(e.target.checked)}/></SettingRow>;
}
export function Section({title, children}: {title: string; children: ReactNode}) {
    return <section className="settings-section"><Typography component="h2" variant="h2">{title}</Typography>{children}</section>;
}
