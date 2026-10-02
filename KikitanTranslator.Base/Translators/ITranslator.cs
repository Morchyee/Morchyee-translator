namespace KikitanTranslator.Base.Translators;

public interface ITranslator : IDisposable
{
    public string? Translate(string text, string source, string target);
    Task<string?> TranslateAsync(string text, string source, string target, CancellationToken cancellationToken)
        => Task.Run(() => Translate(text, source, target), cancellationToken);
}
