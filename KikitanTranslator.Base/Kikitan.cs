using System.Threading.Channels;
using KikitanTranslator.Base.Outputs;
using KikitanTranslator.Base.Translators;
using KikitanTranslator.Recognizers;
using KikitanTranslator.Utility;
using Serilog;

namespace KikitanTranslator.Base;

public sealed class Kikitan : IDisposable
{
    private readonly IRecognizer _recognizer;
    private readonly ITranslator _translator;
    private readonly IErrorHandler _errorHandler;
    private readonly List<IOutput> _outputs = [];
    private readonly bool _isLoopback;
    private readonly object _recognitionLock = new();
    private readonly CancellationTokenSource _lifetime = new();
    private readonly Channel<PendingTranslation> _translations;
    private readonly Channel<(string Source, string Translation)> _delayed = Channel.CreateBounded<(string, string)>(32);
    private Task? _translationWorker;
    private Task? _outputWorker;
    private volatile bool _running;
    private bool _disposed;
    private bool _started;
    private Guid _utteranceId = Guid.NewGuid();
    private DateTime _lastFailure;

    private sealed record PendingTranslation(Guid Id, string Text, string Source, string Target, bool TranscriptionOnly,
        long EnqueuedAt);
    public event OnRecognizerStatus? OnRecognizerStatusChanged;
    public event Action<Guid, string, string, bool, bool>? OnSubtitle;

    public Kikitan(IRecognizer recognizer, ITranslator translator, IErrorHandler errorHandler, bool loopback)
    {
        _recognizer = recognizer;
        _translator = translator;
        _errorHandler = errorHandler;
        _isLoopback = loopback;
        // Desktop captions favor recent speech during provider stalls. Legacy chat output retains FIFO behavior.
        _translations = Channel.CreateBounded<PendingTranslation>(new BoundedChannelOptions(loopback ? 3 : 32)
        {
            FullMode = loopback ? BoundedChannelFullMode.DropOldest : BoundedChannelFullMode.Wait,
            SingleReader = true
        });
        recognizer.OnRecognitionReceived += OnRecognition;
        recognizer.OnRecognizerStatusChanged += OnRecognizerStatus;
    }
    public void AddOutput(IOutput output) => _outputs.Add(output);
    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_started) return; // A stopped pipeline is disposed and replaced by Manager.
        _started = true;
        _running = true;
        _translationWorker = TranslateQueuedAsync(_lifetime.Token);
        _outputWorker = SendDelayedAsync(_lifetime.Token);
        _recognizer.Start(AppConfig.ConfigObject.SourceLanguage, _errorHandler);
    }
    private void OnRecognition(string text, bool final)
    {
        lock (_recognitionLock)
        {
            if (!_running || string.IsNullOrWhiteSpace(text)) return;
            var id = _utteranceId;
            if (AppConfig.ConfigObject.Recognizer == 2)
            {
                var parts = text.Split('|', 2);
                Publish(id, parts[0], parts.Length > 1 ? parts[1] : "", final);
            }
            else
            {
                // Commit source immediately; translation later updates exactly this ID.
                OnSubtitle?.Invoke(id, text, "", final, false);
                foreach (var output in _outputs.Where(o => !o.IsDelayed())) SendOutput(output, text, "", false);
                if (final && !_translations.Writer.TryWrite(new PendingTranslation(id, text,
                        AppConfig.ConfigObject.SourceLanguage, AppConfig.ConfigObject.TargetLanguage,
                        AppConfig.ConfigObject.SpeechToTextOnly, System.Diagnostics.Stopwatch.GetTimestamp())))
                    ReportFailure("Translation queue is full. Original subtitles are retained.");
            }
            if (final) _utteranceId = Guid.NewGuid();
        }
    }
    private async Task TranslateQueuedAsync(CancellationToken token)
    {
        try
        {
            await foreach (var item in _translations.Reader.ReadAllAsync(token))
            {
                if (token.IsCancellationRequested) break;
                // Never spend another provider request on desktop speech that is already stale.
                if (_isLoopback && System.Diagnostics.Stopwatch.GetElapsedTime(item.EnqueuedAt) > TimeSpan.FromSeconds(10))
                    continue;
                string translated = "";
                try
                {
                    if (!item.TranscriptionOnly)
                        translated = await _translator.TranslateAsync(item.Text, item.Source, item.Target, token) ?? "";
                    if (!item.TranscriptionOnly && string.IsNullOrWhiteSpace(translated))
                        ReportFailure("Translation provider returned no text. Original subtitles are retained.");
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
                catch (Exception e)
                {
                    // Do not log provider exceptions: some include request URLs or credentials.
                    Log.Warning("[TRANSLATION] Failed with {ErrorType}", e.GetType().Name);
                    ReportFailure(e is InvalidOperationException ? e.Message :
                        "Translation failed or timed out. Check provider credentials, languages and network. Original subtitles are retained.");
                }
                lock (_recognitionLock)
                {
                    if (!_running) break;
                    Publish(item.Id, item.Text, translated, true, true);
                }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }
    private void Publish(Guid id, string text, string translated, bool final, bool translationUpdate = false)
    {
        OnSubtitle?.Invoke(id, text, translated, final, translationUpdate);
        foreach (var output in _outputs.Where(o => !o.IsDelayed())) SendOutput(output, text, translated, final);
        if (final && _outputs.Any(o => o.IsDelayed()) && !_delayed.Writer.TryWrite((text, translated)))
            ReportFailure("Delayed output queue is full.");
    }
    private static void SendOutput(IOutput output, string source, string translation, bool final)
    {
        try { output.Send(source, translation, final); }
        catch (Exception e) { Log.Warning("[OUTPUT] Delivery failed: {ErrorType}", e.GetType().Name); }
    }
    private void ReportFailure(string message)
    {
        if (DateTime.UtcNow - _lastFailure < TimeSpan.FromSeconds(15)) return;
        _lastFailure = DateTime.UtcNow;
        _errorHandler.OnError(message);
    }
    private async Task SendDelayedAsync(CancellationToken token)
    {
        try
        {
            await foreach (var item in _delayed.Reader.ReadAllAsync(token))
            {
                if (token.IsCancellationRequested) break;
                foreach (var output in _outputs.Where(o => o.IsDelayed())) SendOutput(output, item.Source, item.Translation, true);
                var length = Math.Max(item.Source.Length, item.Translation.Length);
                await Task.Delay((int)Math.Clamp((long)length * AppConfig.ConfigObject.ChatboxWaitPerCharMs, 0, 60000), token);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception e) { Log.Warning("[OUTPUT] Failed with {ErrorType}", e.GetType().Name); }
    }
    private void OnRecognizerStatus(RecognizerStatus status) => OnRecognizerStatusChanged?.Invoke(status);
    public void ManualTranslate(string text) => OnRecognition(text, true);
    public void Stop()
    {
        _running = false;
        _lifetime.Cancel(); // Cancel HTTP before waiting for recognizer callbacks.
        _recognizer.Stop();
        Task.WhenAll(_translationWorker ?? Task.CompletedTask, _outputWorker ?? Task.CompletedTask).GetAwaiter().GetResult();
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _running = false;
        _lifetime.Cancel();
        _recognizer.OnRecognitionReceived -= OnRecognition;
        _recognizer.OnRecognizerStatusChanged -= OnRecognizerStatus;
        try { _recognizer.Dispose(); }
        finally
        {
            Task.WhenAll(_translationWorker ?? Task.CompletedTask, _outputWorker ?? Task.CompletedTask).GetAwaiter().GetResult();
            lock (_recognitionLock) { }
            _translator.Dispose();
            _lifetime.Dispose();
        }
    }
}
