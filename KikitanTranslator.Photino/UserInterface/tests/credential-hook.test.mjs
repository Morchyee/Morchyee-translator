import {test} from "node:test";
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
import vm from "node:vm";
import ts from "typescript";
function load(path, dependencies = {}, globals = {}) {
    const exports = {};
    const source = readFileSync(new URL(path, import.meta.url), "utf8");
    vm.runInNewContext(ts.transpileModule(source, {compilerOptions: {module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2020}}).outputText,
        {exports, require: name => dependencies[name], ...globals});
    return exports;
}
// Exercise the real hook with deterministic hook slots, effects, bridge acknowledgements and timers.
function session() {
    const slots = [], effects = [], sent = []; const timers = new Map();
    let cursor = 0, nextTimer = 0, receive, subscriptions = 0;
    const react = {
        useReducer(reducer, initial) {
            const index = cursor++; slots[index] ??= {state: initial};
            return [slots[index].state, action => {slots[index].state = reducer(slots[index].state, action);}];
        },
        useRef(initial) {const index = cursor++; return slots[index] ??= {current: initial};},
        useEffect(callback, dependencies) {
            const index = cursor++, previous = slots[index];
            if (!previous || dependencies.some((value, i) => value !== previous.dependencies[i]))
                effects.push(() => {previous?.cleanup?.(); slots[index] = {dependencies, cleanup: callback()};});
        }
    };
    const reducer = load("../src/components/desktop/credentialState.ts");
    const bridge = {
        setConfig(field, value) {const id = sent.length + 1; sent.push({field, value, id}); return id;},
        registerConfigSavedCallback(callback) {receive = callback; subscriptions++; return () => {subscriptions--; receive = undefined;};}
    };
    const hook = load("../src/components/desktop/useCredentialDrafts.ts", {react, "./credentialState": reducer, "../../util/photino": bridge},
        {window: {setTimeout(callback) {const id = ++nextTimer; timers.set(id, () => {timers.delete(id); callback();}); return id;}, clearTimeout(id) {timers.delete(id);}}});
    function render(failures = 0) {cursor = 0; const result = hook.useCredentialDrafts(failures); effects.splice(0).forEach(effect => effect()); return result;}
    return {render, sent, timers, confirm: (field, id) => receive(field, id), subscriptions: () => subscriptions,
        unmount: () => slots.forEach(slot => slot?.cleanup?.())};
}
test("Repeated Save before a render sends one request per credential", () => {
    const s = session(), editor = s.render();
    editor.edit("groq_api_key", "disposable-draft"); editor.save("groq_api_key", "disposable-draft"); editor.save("groq_api_key", "disposable-draft");
    assert.equal(s.sent.length, 1); assert.equal(s.timers.size, 1);
});
test("An already queued old timeout cannot remove a retry's timer", () => {
    const s = session(); let editor = s.render();
    editor.edit("groq_api_key", "disposable-draft"); editor.save("groq_api_key", "disposable-draft");
    const oldTimeout = [...s.timers.values()][0]; oldTimeout();
    editor = s.render(); assert.equal(editor.get("groq_api_key").value, "disposable-draft");
    editor.save("groq_api_key", "disposable-draft"); oldTimeout();
    assert.equal(s.timers.size, 1); s.confirm("groq_api_key", 1); assert.equal(s.timers.size, 1);
    s.confirm("groq_api_key", 2); assert.equal(s.timers.size, 0); assert.equal(s.render().get("groq_api_key").value, "");
});
test("Failure and removal keep replacement drafts and release pending timers", () => {
    const s = session(); let editor = s.render();
    editor.edit("deepl_api_key", "disposable-replacement"); editor.save("deepl_api_key", "disposable-replacement");
    s.render(1); editor = s.render(1);
    assert.equal(editor.get("deepl_api_key").value, "disposable-replacement"); assert.equal(s.timers.size, 0);
    editor.save("deepl_api_key", ""); s.confirm("deepl_api_key", 2);
    assert.equal(s.render(1).get("deepl_api_key").value, "disposable-replacement");
});
test("Rerenders keep one acknowledgement subscription; unmount cancels timers", () => {
    const s = session(), editor = s.render(); s.render(); s.render(); assert.equal(s.subscriptions(), 1);
    editor.save("groq_api_key", "disposable-draft"); s.unmount(); assert.equal(s.subscriptions(), 0); assert.equal(s.timers.size, 0);
});
