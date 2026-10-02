namespace KikitanTranslator.Photino;

public sealed record DesktopSubtitleResult(string OriginalText, string TranslatedText, bool IsFinal, Guid Id,
    bool IsTranslationUpdate = false, string? Command = null, string? State = null, string? Error = null);
