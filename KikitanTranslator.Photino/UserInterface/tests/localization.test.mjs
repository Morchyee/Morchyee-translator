import {test} from "node:test";
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
import vm from "node:vm";
import ts from "typescript";

const directory = new URL("../src/i18n/", import.meta.url);
const load = name => JSON.parse(readFileSync(new URL(name, directory), "utf8"));
const en = load("locales/en.json"), zh = load("locales/zh-CN.json"), ja = load("locales/ja-JP.json");
const exports = {};
vm.runInNewContext(ts.transpileModule(readFileSync(new URL("localization.ts", directory), "utf8"), {
    compilerOptions: {module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2020}
}).outputText, {exports, require: name => ({default: load(name)} )});

test("All interface catalogs contain the same nonempty keys and placeholders", () => {
    for (const catalog of [zh, ja]) {
        assert.deepEqual(Object.keys(catalog).sort(), Object.keys(en).sort());
        for (const key of Object.keys(en)) {
            assert.equal(typeof catalog[key], "string"); assert.ok(catalog[key].trim());
            assert.deepEqual([...catalog[key].matchAll(/\{(\w+)\}/g)].map(m => m[1]).sort(), [...en[key].matchAll(/\{(\w+)\}/g)].map(m => m[1]).sort());
        }
    }
});
test("System detection distinguishes Simplified Chinese and explicit choices win", () => {
    for (const language of ["zh-CN", "zh-SG", "zh-Hans", "zh-Hans-CN"]) assert.equal(exports.resolveLocale(undefined, language), "zh-CN");
    assert.equal(exports.resolveLocale(undefined, "ja"), "ja-JP");
    assert.equal(exports.resolveLocale(undefined, "zh-TW"), "en");
    assert.equal(exports.resolveLocale(undefined, "fr-FR"), "en");
    assert.equal(exports.resolveLocale("ja-JP", "zh-CN"), "ja-JP");
    assert.equal(exports.resolveLocale("invalid", "zh-CN"), "en");
});
test("English fallback and interpolation work; unknown keys do not silently render", () => {
    const saved = exports.catalogs["ja-JP"]["common.start"];
    delete exports.catalogs["ja-JP"]["common.start"];
    assert.equal(exports.translate("ja-JP", "common.start"), "Start translation");
    exports.catalogs["ja-JP"]["common.start"] = saved;
    assert.equal(exports.translate("zh-CN", "about.version", {version: "1.0"}), "版本 1.0 · Windows x64");
    assert.throws(() => exports.translate("en", "nonexistent.key"), /Unknown localization key/);
});
test("Safe provider diagnostics map to localized actionable categories", () => {
    assert.equal(exports.errorKey("Groq API key is missing."), "errors.authentication");
    assert.equal(exports.errorKey("Provider rate limit reached."), "errors.rateLimit");
    assert.equal(exports.errorKey("Provider quota exhausted."), "errors.quota");
    assert.equal(exports.errorKey("Translation failed or timed out."), "errors.network");
    assert.equal(exports.errorKey("Unrecognized service problem"), "errors.service");
});
