// Development-only fixture. Vite removes the import from production builds.
// No credentials, provider calls, or persisted application settings are used here.
import {useState} from "react";
import {createRoot} from "react-dom/client";
import DesktopSettings from "../pages/DesktopSettings";
import {app_state, config} from "../util/constants";
import {init} from "../util/photino";

export function startPreview() {
    const params = new URLSearchParams(location.search);
    let receiver: ((message: string) => void) | undefined;
    let update: ((state: app_state) => void) | undefined;
    let state: app_state = {status: 0, app_version: "2.0.2 · Design preview", server_version: "", is_linux: false, is_appimage: false, is_muted: false, microphones: [],
        config: {desktop_translation: true, desktop_translation_provider: "groq", source_language: "en", target_language: "ja", recognizer: 0,
            light_mode: params.get("theme") === "light", speech_to_text_only: false, auto_start: false,
            groq_api_key_configured: false, google_cloud_api_key_configured: false, deepl_api_key_configured: false, gemini_api_key_configured: false} as config};
    const respond = (method: string, data: object) => receiver?.(JSON.stringify({method, data: JSON.stringify(data)}));
    Object.assign(window.external, {
        receiveMessage: (callback: (message: string) => void) => {receiver = callback;},
        sendMessage: (message: string) => {
            const request = JSON.parse(message);
            if (request.method === "update_config") {
                const {field, value, request_id} = JSON.parse(request.data);
                if (params.has("save-error")) {respond("notification", {msg: "Preview: settings could not be saved. Your draft is retained.", level: 2}); return;}
                state = {...state, config: {...state.config, [field.endsWith("_api_key") ? field + "_configured" : field]: field.endsWith("_api_key") ? !!value : value}};
                update?.(state); queueMicrotask(() => respond("update_config", {field, request_id, saved: true}));
            } else if (request.method === "control") {
                if (request.data === "ON" || request.data === "OFF") {state = {...state, status: request.data === "ON" ? 2 : 0}; update?.(state);}
                else respond("notification", {msg: "Design preview: the native subtitle window is available in the Windows application.", level: 0});
            }
        }
    });
    init();
    function Fixture() {const [current, setCurrent] = useState(state); update = setCurrent; return <DesktopSettings state={current}/>;}
    createRoot(document.getElementById("root")!).render(<Fixture/>);
}
