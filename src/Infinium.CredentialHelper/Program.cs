using System.IO.Pipes;
using Infinium.CredentialHelper;

if (args.Length != 4 || args[0] != "--request-handle" || args[2] != "--response-handle"
    || string.IsNullOrWhiteSpace(args[1]) || string.IsNullOrWhiteSpace(args[3]))
{
    Console.Error.WriteLine("The one-shot helper requires exactly two inherited private anonymous-pipe handles.");
    return 64;
}

try
{
    using AnonymousPipeClientStream request = new(PipeDirection.In, args[1]);
    using AnonymousPipeClientStream response = new(PipeDirection.Out, args[3]);
    using DeterministicFakeSecureStore store = new();
    OneShotHelperEngine engine = new(store);
    using CancellationTokenSource deadline = new(TimeSpan.FromSeconds(30));
    await engine.RunAsync(request, response, deadline.Token);
    return 0;
}
catch (Exception exception) when (exception is IOException or InvalidDataException or OperationCanceledException)
{
    Console.Error.WriteLine($"Helper terminated with typed non-secret failure: {exception.GetType().Name}");
    return 65;
}
