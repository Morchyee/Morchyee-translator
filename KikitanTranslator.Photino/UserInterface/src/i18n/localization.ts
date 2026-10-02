import en from "./locales/en.json";
import zh from "./locales/zh-CN.json";
import ja from "./locales/ja-JP.json";
import errorRules from "./error-rules.json";

export const supportedLocales = [{id: "en", name: "English"}, {id: "zh-CN", name: "简体中文"}, {id: "ja-JP", name: "日本語"}] as const;
export type Locale = typeof supportedLocales[number]["id"];
export type MessageKey = keyof typeof en;
export type Translate = (key: MessageKey, values?: Record<string, string | number>) => string;
export const catalogs: Record<Locale, Record<MessageKey, string>> = {en, "zh-CN": zh, "ja-JP": ja};
export function detectLocale(language: string): Locale {
    const value = language.toLowerCase();
    if (value === "ja" || value.startsWith("ja-")) return "ja-JP";
    if (value === "zh" || value === "zh-cn" || value === "zh-sg" || value.startsWith("zh-hans")) return "zh-CN";
    return "en";
}
export function resolveLocale(preference?: string, systemLanguage = "en"): Locale {
    return preference ? supportedLocales.find(item => item.id === preference)?.id || "en" : detectLocale(systemLanguage);
}
export function translate(locale: Locale, key: MessageKey, values: Record<string, string | number> = {}): string {
    const text = catalogs[locale]?.[key] || en[key];
    if (!text) throw new Error(`Unknown localization key: ${key}`);
    return text.replace(/\{(\w+)\}/g, (placeholder, name: string) => values[name] === undefined ? placeholder : String(values[name]));
}
export function uiFont(locale: Locale): string {
    const regional = {en: '"Segoe UI Variable", "Segoe UI"', "zh-CN": '"Microsoft YaHei UI", "Microsoft YaHei", "Segoe UI"', "ja-JP": '"Yu Gothic UI", "Yu Gothic", Meiryo, "Segoe UI"'};
    return `${regional[locale]}, system-ui, sans-serif`;
}
// Presentation adapter for the existing safe service messages; no exception or provider logic changes.
export function errorKey(message: string): MessageKey {
    const text = message.toLowerCase();
    return (errorRules.find(rule => rule.patterns.some(pattern => text.includes(pattern)))?.key || "errors.service") as MessageKey;
}
export function knownMessageKey(message: string): MessageKey | undefined {
    for (const catalog of Object.values(catalogs)) {
        const entry = Object.entries(catalog).find(([, text]) => text === message);
        if (entry) return entry[0] as MessageKey;
    }
    return undefined;
}
