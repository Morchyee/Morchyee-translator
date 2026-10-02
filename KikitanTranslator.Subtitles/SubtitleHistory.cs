namespace KikitanTranslator.Subtitles;

internal sealed record DesktopSubtitleResult(string OriginalText, string TranslatedText, bool IsFinal, Guid Id,
    bool IsTranslationUpdate = false, string? Command = null, string? State = null, string? Error = null, string? UiLanguage = null);

internal sealed class SubtitleHistory(int capacity)
{
    public int Capacity { get; set; } = capacity;
    public List<DesktopSubtitleResult> Entries { get; } = [];
    public bool Apply(DesktopSubtitleResult result)
    {
        if (string.IsNullOrWhiteSpace(result.OriginalText)) return false;
        var index = Entries.FindIndex(e => e.Id == result.Id);
        if (index >= 0) Entries[index] = result;
        else
        {
            if (result.IsTranslationUpdate) return false; // Late translation must not resurrect trimmed history.
            Entries.Add(result);

        }
        while (Entries.Count > Capacity) Entries.RemoveAt(0);
        return true;
    }
}
