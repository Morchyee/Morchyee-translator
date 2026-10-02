using KikitanTranslator.Base;
using KikitanTranslator.Base.Outputs;
using KikitanTranslator.Base.Translators;
using KikitanTranslator.Capture;
using KikitanTranslator.Recognizers;
using KikitanTranslator.Utility;

Console.OutputEncoding = System.Text.Encoding.UTF8;
Logger.Initialize();
AppConfig.Load();
using var pipeline = new Kikitan(new Bing(new Microphone("silero_vad.onnx", new ConsoleErrors())), new GoogleCloudTranslator(), new ConsoleErrors(), false);
pipeline.AddOutput(new OSC("/microphone"));
pipeline.AddOutput(new ConsoleOut());
var exit = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
Console.CancelKeyPress += (_, e) => { e.Cancel = true; exit.TrySetResult(); };
pipeline.Start();
await exit.Task;

sealed class ConsoleErrors : IErrorHandler
{
    public void OnError(string message) => Console.Error.WriteLine(message);
}
