import {test} from "node:test";
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
import vm from "node:vm";
import ts from "typescript";

function app() {
    let state = null, receiver, disposed = false, requests = 0;
    const effects = [], exports = {}, screen = Symbol("DesktopSettings");
    const modules = {
        "react": {useState: () => [state, value => {state = value;}], useEffect: effect => effects.push(effect)},
        "react/jsx-runtime": {jsx: (type, props) => ({type, props})},
        "./pages/DesktopSettings": {default: screen},
        "./i18n/localization": {resolveLocale: () => "ja-JP", translate: () => "Desktop Translator を開いています…"},
        "./util/photino": {
            registerStateCallback: callback => {receiver = callback; return () => {disposed = true;};},
            sendAppState: () => {requests++;},
            setConfig: () => {throw new Error("Opening settings must not modify configuration");}
        }
    };
    const code = ts.transpileModule(readFileSync(new URL("../src/DesktopApp.tsx", import.meta.url), "utf8"), {
        compilerOptions: {module: ts.ModuleKind.CommonJS, jsx: ts.JsxEmit.ReactJSX, target: ts.ScriptTarget.ES2020}
    }).outputText;
    vm.runInNewContext(code, {exports, require: name => {assert.ok(modules[name], name); return modules[name];}, navigator: {language: "ja-JP"}});
    return {render: () => exports.default(), mount: () => effects[0](), receive: value => receiver(value), screen,
        get disposed() {return disposed;}, get requests() {return requests;}};
}

test("Normal desktop entry shows modern settings for both capture states without altering configuration", () => {
    for (const enabled of [true, false]) for (const status of [0, 1, 2]) {
        const host = app(); host.render(); const cleanup = host.mount();
        const state = {status, config: {desktop_translation: enabled, source_language: "en", target_language: "zh", ui_language: "ja-JP"}};
        host.receive(state);
        const view = host.render();
        assert.equal(view.type, host.screen);
        assert.equal(view.props.state, state);
        assert.equal(state.config.desktop_translation, enabled);
        assert.equal(host.requests, 1);
        cleanup(); assert.equal(host.disposed, true);
    }
});

test("Before host state arrives, opening status is accessible and localized", () => {
    const view = app().render();
    assert.equal(view.props.role, "status");
    assert.equal(view.props.lang, "ja-JP");
    assert.match(view.props.children, /開いています/);
});

test("Production and preview mount the same desktop app instead of bypassing startup routing", () => {
    const entry = readFileSync(new URL("../src/main.tsx", import.meta.url), "utf8");
    const preview = readFileSync(new URL("../src/dev/preview.tsx", import.meta.url), "utf8");
    assert.match(entry, /import App from "\.\/DesktopApp"/);
    assert.match(preview, /<DesktopApp\/>/);
    assert.doesNotMatch(preview, /<DesktopSettings/);
    assert.match(preview, /send_app_state/);
});
