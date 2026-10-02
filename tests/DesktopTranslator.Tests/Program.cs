using System.Net;
using System.Text.Json;
using KikitanTranslator.Base;
using KikitanTranslator.Base.Translators;
using KikitanTranslator.Recognizers;
using KikitanTranslator.Utility;
using KikitanTranslator.Subtitles;
using Result = KikitanTranslator.Subtitles.DesktopSubtitleResult;

var tests = new List<(string Name, Func<Task> Run)>();
void Test(string name, Func<Task> run) => tests.Add((name, run));
void Check(bool condition, string message = "Assertion failed") { if (!condition) throw new Exception(message); }
async Task Wait(Func<bool> condition)
{
    using var timeout = new CancellationTokenSource(3000);
    while (!condition()) await Task.Delay(10, timeout.Token);
}
Test("Subtitle translation updates existing ID and does not resurrect trimmed entries", () =>
{
    var model = new SubtitleHistory(3);
    var ids = Enumerable.Range(0, 4).Select(_ => Guid.NewGuid()).ToArray();
    foreach (var id in ids) model.Apply(new("source", "", true, id));
    Check(model.Entries.Count == 3);
    Check(!model.Apply(new("source", "late", true, ids[0], true)));
    model.Apply(new("source", "translation", true, ids[2], true));
    Check(model.Entries.Count == 3 && model.Entries[1].TranslatedText == "translation");
    return Task.CompletedTask;
});
Test("IPC roundtrip preserves Unicode, ID and update semantics", () =>
{
    var wire = new KikitanTranslator.Photino.DesktopSubtitleResult("你好 😀", "こんにちは", true, Guid.NewGuid(), true);
    var result = JsonSerializer.Deserialize<Result>(JsonSerializer.Serialize(wire))!;
    Check(result.Id == wire.Id && result.OriginalText == wire.OriginalText && result.IsTranslationUpdate);
    return Task.CompletedTask;
});
Test("Source appears immediately; translation targets the same entry and snapshots direction", async () =>
{
    AppConfig.ConfigObject = new() { SourceLanguage = "en", TargetLanguage = "ja" };
    var recognizer = new FakeRecognizer(); var translator = new FakeTranslator(); var errors = new Errors();
    using var pipeline = new Kikitan(recognizer, translator, errors, true);
    var received = new List<(Guid Id, string Source, string Translation, bool Update)>();
    var sync = new object();
    pipeline.OnSubtitle += (id, source, translated, final, update) => { lock (sync) received.Add((id, source, translated, update)); };
    pipeline.Start(); recognizer.Emit("hello", false); recognizer.Emit("hello world", true);
    lock (sync) Check(received.Count == 2 && received[1].Translation == "");
    AppConfig.ConfigObject.TargetLanguage = "de";
    await translator.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
    Check(translator.Source == "en" && translator.Target == "ja");
    translator.Complete.TrySetResult("こんにちは");
    await Wait(() => {lock (sync) return received.Count == 3;});
    lock (sync) Check(received[0].Id == received[2].Id && received[2].Update && received[2].Translation == "こんにちは");
});
Test("Translation failure retains finalized source and reports useful error", async () =>
{
    AppConfig.ConfigObject = new(); var r = new FakeRecognizer(); var t = new FakeTranslator(); var errors = new Errors();
    using var pipeline = new Kikitan(r, t, errors, true);
    var completed = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
    pipeline.OnSubtitle += (_, source, translated, final, update) => { if (update) completed.TrySetResult(source + translated); };
    pipeline.Start(); r.Emit("retained", true); t.Complete.TrySetException(new HttpRequestException("private transport detail"));
    Check(await completed.Task.WaitAsync(TimeSpan.FromSeconds(2)) == "retained");
    Check(errors.Messages.Count == 1 && !errors.Messages[0].Contains("private"));
});
Test("Stop cancels in-flight translation and suppresses late updates", async () =>
{
    AppConfig.ConfigObject = new(); var r = new FakeRecognizer(); var t = new FakeTranslator();
    using var pipeline = new Kikitan(r, t, new Errors(), true);
    int updates = 0; pipeline.OnSubtitle += (_, _, _, _, update) => { if (update) Interlocked.Increment(ref updates); };
    pipeline.Start(); pipeline.Start(); r.Emit("hello", true);
    await t.Entered.Task;
    await Task.Run(pipeline.Stop).WaitAsync(TimeSpan.FromSeconds(2));
    Check(t.Cancelled && updates == 0 && r.Starts == 1);
});
Test("Transcription only never calls translation provider", async () =>
{
    AppConfig.ConfigObject = new() { SpeechToTextOnly = true };
    var r = new FakeRecognizer(); var t = new FakeTranslator();
    using var pipeline = new Kikitan(r, t, new Errors(), true);
    var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    pipeline.OnSubtitle += (_, _, _, _, update) => { if (update) done.TrySetResult(); };
    pipeline.Start(); r.Emit("source", true); await done.Task.WaitAsync(TimeSpan.FromSeconds(2));
    Check(!t.Entered.Task.IsCompleted);
});
Test("Provider HTTP requests use official hosts, credentials in headers and correct direction", async () =>
{
    AppConfig.ConfigObject = new() { GoogleCloudApiKey = "TEST_CREDENTIAL", DeepLApiKey = "TEST_CREDENTIAL:fx", GroqApiKey = "TEST_CREDENTIAL" };
    var cases = new[] { "google", "deepl", "groq" };
    foreach (var provider in cases)
    {
        using var handler = new FakeHttp(async (request, token) =>
        {
            Check(!request.RequestUri!.Query.Contains("TEST_CREDENTIAL"));
            var body = await request.Content!.ReadAsStringAsync(token);
            var response = provider switch
            {
                "google" => "{\"data\":{\"translations\":[{\"translatedText\":\"a &amp; b\"}]}}",
                "deepl" => "{\"translations\":[{\"text\":\"translated\"}]}",
                _ => "{\"choices\":[{\"message\":{\"content\":\" translated \"}}]}"
            };
            if (provider == "google") Check(request.RequestUri.Host == "translation.googleapis.com" && request.Headers.Contains("X-Goog-Api-Key") && body.Contains("\"target\":\"ja\""));
            if (provider == "deepl") Check(request.RequestUri.Host == "api-free.deepl.com" && request.Headers.Authorization!.Scheme == "DeepL-Auth-Key" && body.Contains("\"target_lang\":\"JA\""));
            if (provider == "groq") Check(request.RequestUri.Host == "api.groq.com" && request.Headers.Authorization!.Scheme == "Bearer");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(response) };
        });
        using ITranslator translator = provider switch { "google" => new GoogleCloudTranslator(new HttpClient(handler)), "deepl" => new DeepLTranslator(new HttpClient(handler)), _ => new GroqTranslator(new HttpClient(handler)) };
        Check(await translator.TranslateAsync("hello", "en", "ja", CancellationToken.None) == (provider == "google" ? "a & b" : "translated"));
    }
});
Test("Permanent HTTP errors do not retry or expose response bodies", async () =>
{
    AppConfig.ConfigObject = new() { GroqApiKey = "TEST_CREDENTIAL" }; var calls = 0;
    using var handler = new FakeHttp((_, _) => { calls++; return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new StringContent("private response") }); });
    using var translator = new GroqTranslator(new HttpClient(handler));
    try { await translator.TranslateAsync("hello", "en", "ja", CancellationToken.None); throw new Exception("Expected rejection"); }
    catch (InvalidOperationException e) { Check(calls == 1 && !e.Message.Contains("private") && e.Message.Contains("credentials")); }
});
Test("Transient rate limits retry with bounded attempts", async () =>
{
    AppConfig.ConfigObject = new() { GroqApiKey = "TEST_CREDENTIAL" }; var calls = 0;
    using var handler = new FakeHttp((_, _) =>
    {
        calls++; var response = new HttpResponseMessage(calls < 3 ? HttpStatusCode.TooManyRequests : HttpStatusCode.OK);
        response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.Zero);
        response.Content = new StringContent("{\"choices\":[{\"message\":{\"content\":\"ok\"}}]}");
        return Task.FromResult(response);
    });
    using var translator = new GroqTranslator(new HttpClient(handler));
    Check(await translator.TranslateAsync("hello", "en", "ja", CancellationToken.None) == "ok" && calls == 3);
});
Test("Provider cancellation reaches HTTP transport", async () =>
{
    AppConfig.ConfigObject = new() { GoogleCloudApiKey = "TEST_CREDENTIAL" };
    using var handler = new FakeHttp(async (_, token) => { await Task.Delay(Timeout.Infinite, token); return new(HttpStatusCode.OK); });
    using var translator = new GoogleCloudTranslator(new HttpClient(handler)); using var stop = new CancellationTokenSource(50);
    try { await translator.TranslateAsync("hello", "en", "ja", stop.Token); throw new Exception("Expected cancellation"); }
    catch (OperationCanceledException) { Check(stop.IsCancellationRequested); }
});
Test("Missing key fails before making a network request", async () =>
{
    AppConfig.ConfigObject = new();
    using var handler = new FakeHttp((_, _) => throw new Exception("Unexpected HTTP call"));
    using var translator = new DeepLTranslator(new HttpClient(handler));
    try { await translator.TranslateAsync("hello", "en", "ja", CancellationToken.None); throw new Exception("Expected missing key"); }
    catch (InvalidOperationException e) { Check(e.Message.Contains("missing")); }
});
Test("Configuration migrates plaintext credentials atomically and redacts public state", () =>
{
    var folder = Path.Combine(AppContext.BaseDirectory, "test-data", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
    var file = Path.Combine(folder, "config.json");
    File.WriteAllText(file, "{\"source_language\":\"en\",\"target_language\":\"ja\",\"groq_api_key\":\"TEST_CREDENTIAL\"}");
    AppConfig.Load(file); Check(AppConfig.LoadError == null && AppConfig.ConfigObject.GroqApiKey == "TEST_CREDENTIAL");
    if (OperatingSystem.IsWindows()) Check(!File.ReadAllText(file).Contains("TEST_CREDENTIAL") && File.ReadAllText(file).Contains(CredentialProtection.Prefix));
    Check((string?)AppConfig.PublicConfig()["groq_api_key"] == "" && (bool)AppConfig.PublicConfig()["groq_api_key_configured"]!);
    AppConfig.Load(file); Check(AppConfig.ConfigObject.GroqApiKey == "TEST_CREDENTIAL");
    File.Delete(file); Directory.Delete(folder);
    return Task.CompletedTask;
});
Test("Malformed configuration and unavailable credentials are preserved", () =>
{
    var folder = Path.Combine(AppContext.BaseDirectory, "test-data", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
    var file = Path.Combine(folder, "config.json");
    foreach (var damaged in new[] { "{ broken", "{\"groq_api_key\":\"dpapi:v1:AAAA\"}" })
    {
        File.WriteAllText(file, damaged); AppConfig.Load(file);
        Check(AppConfig.LoadError != null && File.ReadAllText(file) == damaged && AppConfig.ConfigObject != null);
        try { AppConfig.SaveConfig(); throw new Exception("Expected save protection"); } catch (InvalidOperationException) { }
    }
    File.Delete(file); Directory.Delete(folder); return Task.CompletedTask;
});

Test("Groq speech processing is bounded, serial and cancelled on stop", async () =>
{
    AppConfig.ConfigObject = new() { GroqApiKey = "TEST_CREDENTIAL" };
    var capture = new FakeCapture(); var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    int calls = 0; bool cancelled = false;
    using var handler = new FakeHttp(async (_, token) =>
    {
        Interlocked.Increment(ref calls); entered.TrySetResult();
        try { await Task.Delay(Timeout.Infinite, token); }
        catch (OperationCanceledException) { cancelled = true; throw; }
        return new(HttpStatusCode.OK);
    });
    var errors = new Errors();
    using var recognizer = new GroqRecognizer(capture, new HttpClient(handler));
    recognizer.Start("en", errors);
    for (var i = 0; i < 20; i++) { capture.Emit(new float[3840], true); capture.Emit(new float[960], false); }
    await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
    await Task.Run(recognizer.Stop).WaitAsync(TimeSpan.FromSeconds(2));
    Check(calls == 1 && cancelled && capture.Stopped && errors.Messages.Count > 0, $"calls={calls}, cancelled={cancelled}, stopped={capture.Stopped}, errors={errors.Messages.Count}");
});
Test("Translation backlog is bounded without discarding recognized source", async () =>
{
    AppConfig.ConfigObject = new(); var r = new FakeRecognizer(); var t = new FakeTranslator(); var errors = new Errors();
    using var pipeline = new Kikitan(r, t, errors, true);
    int sources = 0; pipeline.OnSubtitle += (_, _, _, final, update) => { if (final && !update) sources++; };
    pipeline.Start();
    for (var i = 0; i < 100; i++) r.Emit("source " + i, true);
    await t.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
    pipeline.Stop();
    Check(sources == 100 && errors.Messages.Any(m => m.Contains("queue is full")));
});
Test("Malformed provider response preserves source and allows subsequent requests", async () =>
{
    AppConfig.ConfigObject = new() { DeepLApiKey = "TEST_CREDENTIAL" }; var calls = 0;
    using var handler = new FakeHttp((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {Content = new StringContent(++calls == 1 ? "{bad" : "{\"translations\":[{\"text\":\"good\"}]}")}));
    var r = new FakeRecognizer(); var errors = new Errors();
    using var pipeline = new Kikitan(r, new DeepLTranslator(new HttpClient(handler)), errors, true);
    var completions = new List<string>(); var gate = new object();
    pipeline.OnSubtitle += (_, source, translated, _, update) => {if (update) lock (gate) completions.Add(source + translated);};
    pipeline.Start(); r.Emit("first", true); r.Emit("second", true);
    await Wait(() => {lock (gate) return completions.Count == 2;});
    lock (gate) Check(completions[0] == "first" && completions[1] == "secondgood");
});

var failed = 0;
foreach (var test in tests)
{
    try { await test.Run(); Console.WriteLine("PASS " + test.Name); }
    catch (Exception e) { failed++; Console.Error.WriteLine("FAIL " + test.Name + ": " + e.Message); }
}
Console.WriteLine($"{tests.Count - failed}/{tests.Count} tests passed");
return failed == 0 ? 0 : 1;

sealed class FakeRecognizer : IRecognizer
{
    public event OnRecognition? OnRecognitionReceived;
    public event OnRecognizerStatus? OnRecognizerStatusChanged;
    public int Starts;
    public void Start(string language, IErrorHandler errorHandler) { Starts++; OnRecognizerStatusChanged?.Invoke(RecognizerStatus.Running); }
    public void Emit(string text, bool final) => OnRecognitionReceived?.Invoke(text, final);
    public void Stop() => OnRecognizerStatusChanged?.Invoke(RecognizerStatus.NotStarted);
    public RecognizerStatus Status() => RecognizerStatus.Running;
    public void Dispose() => Stop();
}
sealed class FakeTranslator : ITranslator
{
    public TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource<string?> Complete = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public string? Source, Target; public bool Cancelled;
    public string? Translate(string text, string source, string target) => throw new NotSupportedException();
    public async Task<string?> TranslateAsync(string text, string source, string target, CancellationToken token)
    {
        Source = source; Target = target; Entered.TrySetResult();
        try { return await Complete.Task.WaitAsync(token); } catch (OperationCanceledException) { Cancelled = true; throw; }
    }
    public void Dispose() { }
}
sealed class Errors : IErrorHandler
{
    public List<string> Messages = [];
    public void OnError(string message) => Messages.Add(message);
}
sealed class FakeHttp(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => send(request, token);
}

sealed class FakeCapture : KikitanTranslator.Capture.ICapture
{
    public event KikitanTranslator.Capture.OnData? OnDataReceived;
    public bool Stopped;
    public void Emit(float[] samples, bool speech) => OnDataReceived?.Invoke(samples, speech);
    public uint GetSampleRate() => 16000;
    public bool Start() => true;
    public void Stop() => Stopped = true;
    public void Pause() { }
    public void Resume() { }
}
