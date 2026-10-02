using System.Globalization;
using System.Text.Json;

namespace KikitanTranslator.Resources;

/// <summary>Desktop presentation only. Catalogs are shared with React; wire/state values stay invariant.</summary>
public static class DesktopText
{
    public static readonly string[] Locales = ["en", "zh-CN", "ja-JP"];
    private static readonly Dictionary<string, Dictionary<string, string>> Catalogs = Locales.ToDictionary(locale => locale,
        locale => Read<Dictionary<string, string>>($"Desktop.{locale}.json"));
    private static T Read<T>(string name)
    {
        using var stream = typeof(DesktopText).Assembly.GetManifestResourceStream(name) ?? throw new InvalidOperationException($"Missing desktop resource: {name}");
        return JsonSerializer.Deserialize<T>(stream)!;
    }
    public static string Detect(string systemLanguage)
    {
        var value = systemLanguage.ToLowerInvariant();
        if (value == "ja" || value.StartsWith("ja-")) return "ja-JP";
        if (value is "zh" or "zh-cn" or "zh-sg" || value.StartsWith("zh-hans")) return "zh-CN";
        return "en";
    }
    public static string Resolve(string? preference, string? systemLanguage = null) => string.IsNullOrEmpty(preference)
        ? Detect(systemLanguage ?? CultureInfo.CurrentUICulture.Name) : Locales.Contains(preference) ? preference : "en";
    public static string Get(string locale, string key)
    {
        if (Catalogs.TryGetValue(locale, out var catalog) && catalog.TryGetValue(key, out var text)) return text;
        return Catalogs["en"].TryGetValue(key, out var fallback) ? fallback : throw new ArgumentException($"Unknown localization key: {key}");
    }
    public static string FontFamily(string locale) => locale switch { "zh-CN" => "Microsoft YaHei UI", "ja-JP" => "Yu Gothic UI", _ => "Segoe UI" };
    private sealed record ErrorRule(string key, string[] patterns);
    private static readonly ErrorRule[] ErrorRules = Read<ErrorRule[]>("Desktop.error-rules.json");
    public static string ErrorKey(string message)
    {
        var text = message.ToLowerInvariant();
        return ErrorRules.FirstOrDefault(rule => rule.patterns.Any(text.Contains))?.key ?? "errors.service";
    }
}
