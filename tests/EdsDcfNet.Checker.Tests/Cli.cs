namespace EdsDcfNet.Checker.Tests;

using EdsDcfNet.Checker;

/// <summary>Runs <see cref="Program.Main"/> with captured console output.</summary>
internal static class Cli
{
    public static (int ExitCode, string Stdout, string Stderr) Run(params string[] args)
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        try
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);
            var exitCode = Program.Main(args);
            return (exitCode, stdout.ToString(), stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }
}
