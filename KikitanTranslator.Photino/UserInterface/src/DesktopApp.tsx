import {useEffect, useState} from "react";
import DesktopSettings from "./pages/DesktopSettings";
import type {app_state} from "./util/constants";
import {registerStateCallback, sendAppState} from "./util/photino";
import {resolveLocale, translate} from "./i18n/localization";

// Presentation does not depend on the capture configuration. Settings must remain
// available even when capture is disabled or an existing configuration is loaded.
export default function DesktopApp() {
    const [state, setState] = useState<app_state | null>(null);
    useEffect(() => {
        const unsubscribe = registerStateCallback(setState);
        sendAppState();
        return unsubscribe;
    }, []);
    if (state?.config) return <DesktopSettings state={state}/>;
    const locale = resolveLocale(undefined, navigator.language);
    return <div role="status" lang={locale} style={{height: "100dvh", display: "grid", placeItems: "center", background: "#171b22", color: "#bac5d4", fontFamily: '"Segoe UI", "Microsoft YaHei UI", "Yu Gothic UI", system-ui, sans-serif', fontSize: 13}}>{translate(locale, "common.opening")}</div>;
}
