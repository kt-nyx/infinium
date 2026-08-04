namespace Infinium.EvaluatorV2;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            return args.Length == 0 ? Usage() : args[0] switch
            {
                "protocol" => PrintProtocol(args),
                "adapt" => Adapt(args),
                "calibrate" => Calibrate(args),
                "score" => Score(args),
                _ => Usage(),
            };
        }
        catch (Exception exception) when (exception is
            IOException or
            UnauthorizedAccessException or
            InvalidDataException or
            ArgumentException)
        {
            Console.Error.WriteLine($"EVALUATOR_ERROR: {exception.Message}");
            return 2;
        }
    }

    private static int PrintProtocol(string[] args)
    {
        if (args.Length != 1)
        {
            return Usage();
        }

        string path = Path.Combine(AppContext.BaseDirectory, "protocol", "protocol.json");
        Console.Write(File.ReadAllText(path));
        return 0;
    }

    private static int Calibrate(string[] args)
    {
        string resultDirectory = RequiredOption(args, "--result-dir");
        if (args.Length != 3)
        {
            return Usage();
        }

        CalibrationResults results = CalibrationSuite.Run();
        string root = EvaluatorScorer.ConfinedResultRoot(resultDirectory);
        EvaluatorScorer.WriteNew(root, "calibration-results.json", EvaluatorProtocol.Serialize(results));
        Console.WriteLine(results.Passed ? "PASS" : "FAIL");
        return results.Passed ? 0 : 1;
    }

    private static int Adapt(string[] args)
    {
        string manifestPath = RequiredOption(args, "--manifest");
        string resultDirectory = RequiredOption(args, "--result-dir");
        if (args.Length != 5)
        {
            return Usage();
        }

        ExecutionManifest manifest = EvaluatorScorer.ReadAndValidateManifest(manifestPath);
        CandidateSemanticOutput output = ReflectionCandidateAdapter.Execute(manifest);
        string root = EvaluatorScorer.ConfinedResultRoot(resultDirectory);
        EvaluatorScorer.WriteNew(root, "candidate-output.json", EvaluatorProtocol.Serialize(output));
        Console.WriteLine("PASS");
        return 0;
    }

    private static int Score(string[] args)
    {
        string manifest = RequiredOption(args, "--manifest");
        string oracle = RequiredOption(args, "--oracle");
        string resultDirectory = RequiredOption(args, "--result-dir");
        if (args.Length != 7)
        {
            return Usage();
        }

        ScoreOutcome outcome = EvaluatorScorer.Score(manifest, oracle);
        EvaluatorScorer.WriteResults(resultDirectory, outcome);
        Console.WriteLine(outcome.Result.TerminalResult);
        return outcome.Result.TerminalResult switch
        {
            "PASS" => 0,
            "FAIL" => 1,
            _ => 2,
        };
    }

    private static string RequiredOption(string[] args, string name)
    {
        int index = Array.IndexOf(args, name);
        if (index < 0 || index + 1 >= args.Length || string.IsNullOrWhiteSpace(args[index + 1]))
        {
            throw new ArgumentException($"Required option '{name}' is absent.");
        }

        return args[index + 1];
    }

    private static int Usage()
    {
        Console.Error.WriteLine("Usage: Infinium.EvaluatorV2 protocol | adapt --manifest <file> --result-dir <dir> | calibrate --result-dir <dir> | score --manifest <file> --oracle <file> --result-dir <dir>");
        return 2;
    }
}
