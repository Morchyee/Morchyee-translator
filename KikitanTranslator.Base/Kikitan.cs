using KikitanTranslator.Base.Outputs;
using KikitanTranslator.Base.Translators;
using KikitanTranslator.Recognizers;
using KikitanTranslator.Utility;
using Serilog;

namespace KikitanTranslator.Base;

public class Kikitan : IDisposable
{
    private IRecognizer _recognizer;
    private ITranslator _translator;
    private IErrorHandler _errorHandler;
    private List<IOutput> _outputs = [];

    private List<string[]> _queue = [];
    private readonly object _queueLock = new object();

    private readonly object _recognitionLock = new();
    private readonly CancellationTokenSource _workerCancellation = new();
    private Task? _workerTask;
    private volatile bool _running;
    private bool _disposed;
    private bool _isLoopback;
    
    public event OnRecognizerStatus? OnRecognizerStatusChanged;

    public Kikitan(IRecognizer recognizer, ITranslator translator, IErrorHandler errorHandler, bool loopback)
    {
        _recognizer = recognizer;
        _translator = translator;
        _errorHandler = errorHandler;

        recognizer.OnRecognitionReceived += OnRecognition;
        recognizer.OnRecognizerStatusChanged += OnRecognizerStatus;
        
        _isLoopback = loopback;
        
        Log.Information($"[KKTN] Kikitan is starting up: desktop={_isLoopback}, recognizer={_recognizer.GetType().Name}, translator={_translator.GetType().Name}");
    }

    public void AddOutput(IOutput output) => _outputs.Add(output);

    public void Start()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(Kikitan));
        _running = true;
        _recognizer.Start(_isLoopback ? AppConfig.ConfigObject.TargetLanguage : AppConfig.ConfigObject.SourceLanguage, _errorHandler);
        Log.Information($"[KKTN] Recognizer start result: desktop={_isLoopback}, status={_recognizer.Status()}");
        
        _workerTask = QueueWorkerAsync(_workerCancellation.Token);
    }

    public void Stop()
    {
        _running = false;
        _recognizer.Stop();
        _workerCancellation.Cancel();
        _workerTask?.GetAwaiter().GetResult();
        lock (_recognitionLock) { }
        
        Log.Information("[KKTN] Kikitan has stopped");
    }

    private void OnRecognition(string text, bool final)
    {
        lock (_recognitionLock)
        {
            if (!_running) return;
            ProcessRecognition(text, final);
        }
    }

    private void ProcessRecognition(string text, bool final)
    {
        if (final) Log.Debug($"[KKTN] Final recognition received: desktop={_isLoopback}, chars={text.Length}, running={_running}");
        if (AppConfig.ConfigObject.Recognizer == 2)
        {
            foreach (var output in _outputs)
            {
                if (!_running) return;
                output.Send(text.Split("|")[0], text.Split("|")[1], final);
            }

            return;
        }
        
        foreach (var output in _outputs)
        {
            if (!_running) return;
            output.Send(text, "", false);
        }

        if (!final || text.Length == 0) return;

        try
        {
            var translationTimer = System.Diagnostics.Stopwatch.StartNew();
            Log.Debug($"[KKTN] Translation started: desktop={_isLoopback}, translator={_translator.GetType().Name}, chars={text.Length}, transcriptionOnly={AppConfig.ConfigObject.SpeechToTextOnly}");
            var translated = AppConfig.ConfigObject.SpeechToTextOnly ? "" : _isLoopback ? _translator.Translate(text, AppConfig.ConfigObject.TargetLanguage, AppConfig.ConfigObject.SourceLanguage) : _translator.Translate(text, AppConfig.ConfigObject.SourceLanguage, AppConfig.ConfigObject.TargetLanguage);
        
            Log.Debug($"[KKTN] Translation completed: desktop={_isLoopback}, elapsedMs={translationTimer.ElapsedMilliseconds}, resultChars={translated?.Length}, hasResult={translated != null}");
            if (!_running) return;
            if (translated == null) Log.Warning($"[KKTN] No translation result; final output skipped: desktop={_isLoopback}");
            if (translated != null)
            {
                lock (_queueLock)
                {
                    _queue.Add([text, translated]);
                    Log.Debug($"[KKTN] Translation queued: desktop={_isLoopback}, depth={_queue.Count}");
                }
            
                foreach (var output in _outputs.Where(v => !v.IsDelayed())) output.Send(text, translated, true);
            }
        }
        catch (Exception e)
        {
            Log.Error(e, $"[KKTN] Translation/output processing failed: desktop={_isLoopback}");
            
            _errorHandler.OnError($"Error while translating: {e.Message}");
        }
    }

    private void OnRecognizerStatus(RecognizerStatus status) => OnRecognizerStatusChanged?.Invoke(status);

    private async Task QueueWorkerAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                string[]? texts = null;
                lock (_queueLock)
                {
                    if (_queue.Count > 0)
                    {
                        texts = _queue[0];
                        _queue.RemoveAt(0);
                    }
                }
                if (texts == null)
                {
                    await Task.Delay(50, cancellationToken);
                    continue;
                }

                Log.Debug($"[KKTN] Processing delayed output: desktop={_isLoopback}, remaining={_queue.Count}, sourceChars={texts[0].Length}, translationChars={texts[1].Length}");
                if (cancellationToken.IsCancellationRequested) break;
                foreach (var output in _outputs.Where(v => v.IsDelayed())) output.Send(texts[0], texts[1], true);
                Log.Verbose($"[KKTN] Waiting {texts[1].Length * AppConfig.ConfigObject.ChatboxWaitPerCharMs}ms...");
                await Task.Delay(texts[1].Length * AppConfig.ConfigObject.ChatboxWaitPerCharMs, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception e)
        {
            Log.Error(e, $"[KKTN] Delayed output failed: desktop={_isLoopback}");
            _errorHandler.OnError($"Error while sending output: {e.Message}");
        }
    }

    public void ManualTranslate(string text) => OnRecognition(text, true);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _running = false;
        _recognizer.OnRecognitionReceived -= OnRecognition;
        _recognizer.OnRecognizerStatusChanged -= OnRecognizerStatus;
        _workerCancellation.Cancel();
        try
        {
            _recognizer.Dispose();
        }
        finally
        {
            try
            {
                _workerTask?.GetAwaiter().GetResult();
                lock (_recognitionLock) { }
            }
            finally
            {
                _translator.Dispose();
                _workerCancellation.Dispose();
            }
        }
    }
}
