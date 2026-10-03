import ReactDOM from "react-dom/client";
import App from "./DesktopApp";

import "./globals.css";
import {init} from "./util/photino.ts";

if (import.meta.env.DEV && new URLSearchParams(location.search).has("preview")) {
    import("./dev/preview").then(preview => preview.startPreview());
} else {
    init();
    ReactDOM.createRoot(document.getElementById("root") as HTMLElement).render(<App />);
}
