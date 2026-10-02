import {test} from "node:test";
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
import vm from "node:vm";
import ts from "typescript";

function bridge() {
    let receive;
    const sent = [];
    const exports = {};
    const source = readFileSync(new URL("../src/util/photino.ts", import.meta.url), "utf8");
    const code = ts.transpileModule(source, {compilerOptions: {module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2020}}).outputText;
    vm.runInNewContext(code, {exports, window: {external: {receiveMessage: callback => {receive = callback;}, sendMessage: value => sent.push(JSON.parse(value))}}});
    exports.init();
    return {api: exports, sent, respond: (method, data) => receive(JSON.stringify({method, data: JSON.stringify(data)}))};
}

test("A credential save sends only its draft and waits for an explicit acknowledgement", () => {
    const {api, sent, respond} = bridge();
    const saved = [];
    api.registerConfigSavedCallback(field => saved.push(field));
    const requestId = api.setConfig("groq_api_key", "local-test-draft");
    assert.equal(sent[0].method, "update_config");
    assert.deepEqual(JSON.parse(sent[0].data), {field: "groq_api_key", value: "local-test-draft", request_id: requestId});
    respond("state", {config: {groq_api_key_configured: true}});
    assert.deepEqual(saved, []);
    respond("update_config", {field: "groq_api_key", saved: true});
    assert.deepEqual(saved, ["groq_api_key"]);
});

test("Unconfirmed settings and provider errors never report a successful save", () => {
    const {api, respond} = bridge();
    const saved = []; const errors = [];
    api.registerConfigSavedCallback(field => saved.push(field));
    api.registerNotificationCallback(message => errors.push(message));
    respond("update_config", {field: "deepl_api_key", saved: false});
    respond("notification", {msg: "Could not save settings", level: 2});
    assert.deepEqual(saved, []);
    assert.deepEqual(errors, ["Could not save settings"]);
});

test("Unmounted credential and notification listeners are released", () => {
    const {api, respond} = bridge();
    let calls = 0;
    const unsubscribeSaved = api.registerConfigSavedCallback(() => calls++);
    const unsubscribeNotice = api.registerNotificationCallback(() => calls++);
    unsubscribeSaved(); unsubscribeNotice();
    respond("update_config", {field: "groq_api_key", saved: true});
    respond("notification", {msg: "error", level: 2});
    assert.equal(calls, 0);
});

test("Subtitle customization uses the existing control channel", () => {
    const {api, sent} = bridge();
    api.showSubtitleAppearance(); api.showSubtitles();
    assert.deepEqual(sent, [{method: "control", data: "APPEARANCE"}, {method: "control", data: "SHOW"}]);
});


test("Concurrent configuration saves retain their request identity", () => {
    const {api, respond} = bridge();
    const confirmations = [];
    api.registerConfigSavedCallback((field, id) => confirmations.push({field, id}));
    const first = api.setConfig("groq_api_key", "first-local-draft");
    const second = api.setConfig("groq_api_key", "second-local-draft");
    assert.notEqual(first, second);
    respond("update_config", {field: "groq_api_key", request_id: second, saved: true});
    respond("update_config", {field: "groq_api_key", request_id: first, saved: true});
    assert.deepEqual(confirmations, [{field: "groq_api_key", id: second}, {field: "groq_api_key", id: first}]);
});
