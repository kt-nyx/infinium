using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

internal sealed record ProcessResult(int ExitCode, string Output, string Error);

internal static class TestProcessRunner
{
    internal static ProcessResult RunDotnetProject(
        string project,
        IReadOnlyList<string> arguments,
        int timeoutMilliseconds,
        string timeoutMessage)
    {
        ProcessStartInfo start = new()
        {
            FileName = "dotnet",
            WorkingDirectory = TestRepository.Root,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        start.ArgumentList.Add("run");
        start.ArgumentList.Add("--project");
        start.ArgumentList.Add(project);
        start.ArgumentList.Add("-c");
        start.ArgumentList.Add("Release");
        start.ArgumentList.Add("--no-build");
        start.ArgumentList.Add("--");
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(start)
            ?? throw new InvalidOperationException($"Could not start dotnet project '{project}'.");
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        bool exited = process.WaitForExit(timeoutMilliseconds);
        if (!exited)
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
        }

        Assert.IsTrue(exited, timeoutMessage);
        Task.WaitAll(output, error);
        return new ProcessResult(process.ExitCode, output.Result, error.Result);
    }
}
