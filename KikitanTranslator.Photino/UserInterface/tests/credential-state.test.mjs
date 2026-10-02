import {test} from "node:test";
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
import vm from "node:vm";
import ts from "typescript";

const source = readFileSync(new URL("../src/components/desktop/credentialState.ts", import.meta.url), "utf8");
const exports = {};
vm.runInNewContext(ts.transpileModule(source, {compilerOptions: {module: ts.ModuleKind.CommonJS}}).outputText, {exports});
const reduce = exports.credentialReducer;
const edit = (field, value) => ({type: "edit", field, value});
const saving = (field, requestId, operation = "save") => ({type: "saving", field, requestId, operation});

test("Provider drafts remain separate and a shared Groq draft has one owner", () => {
    let state = reduce({}, edit("groq_api_key", "speech-and-translation-draft"));
    state = reduce(state, edit("deepl_api_key", "deepl-local-draft"));
    assert.equal(state.groq_api_key.value, "speech-and-translation-draft");
    assert.equal(state.deepl_api_key.value, "deepl-local-draft");
    state = reduce(state, saving("groq_api_key", 1));
    state = reduce(state, {type: "saved", field: "groq_api_key", requestId: 1});
    assert.equal(state.groq_api_key.value, "");
    assert.equal(state.deepl_api_key.value, "deepl-local-draft");
});

test("Failed saves retain drafts and late confirmations cannot clear a retry", () => {
    let state = reduce({}, edit("groq_api_key", "first-local-draft"));
    state = reduce(state, saving("groq_api_key", 1));
    state = reduce(state, {type: "failed"});
    assert.equal(state.groq_api_key.requestId, null);
    assert.equal(state.groq_api_key.value, "first-local-draft");
    assert.equal(state.groq_api_key.severity, "warning");
    state = reduce(state, edit("groq_api_key", "replacement-local-draft"));
    state = reduce(state, saving("groq_api_key", 2));
    assert.equal(reduce(state, {type: "saved", field: "groq_api_key", requestId: 1}), state);
    state = reduce(state, {type: "saved", field: "groq_api_key", requestId: 2});
    assert.equal(state.groq_api_key.value, "");
    assert.equal(state.groq_api_key.requestId, null);
    assert.equal(state.groq_api_key.severity, "success");
});

test("A timeout retains its draft and an old timer cannot cancel a newer request", () => {
    let state = reduce({}, edit("google_cloud_api_key", "local-draft"));
    state = reduce(state, saving("google_cloud_api_key", 1));
    state = reduce(state, {type: "timeout", field: "google_cloud_api_key", requestId: 1});
    assert.equal(state.google_cloud_api_key.value, "local-draft");
    assert.equal(state.google_cloud_api_key.requestId, null);
    state = reduce(state, saving("google_cloud_api_key", 2));
    assert.equal(reduce(state, {type: "timeout", field: "google_cloud_api_key", requestId: 1}), state);
});

test("Removing a stored key preserves a replacement draft", () => {
    let state = reduce({}, edit("deepl_api_key", "replacement-local-draft"));
    state = reduce(state, saving("deepl_api_key", 1, "remove"));
    state = reduce(state, {type: "saved", field: "deepl_api_key", requestId: 1});
    assert.equal(state.deepl_api_key.value, "replacement-local-draft");
    assert.equal(state.deepl_api_key.message, "credential.removed");
});
