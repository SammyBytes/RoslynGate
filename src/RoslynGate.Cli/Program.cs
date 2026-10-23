using RoslynGate.Cli;
using RoslynGate.Engine;
using RoslynGate.Mcp;

try
{
    var cli = CliArgsParser.Parse(args);

    if (cli.ShowHelp)
    {
        Console.Out.Write(Usage.Text);
        return 0;
    }

    if (cli.ShowVersion)
    {
        Console.Out.WriteLine($"roslyngate {BuildInfo.Version}");
        return 0;
    }

    var options = cli.Options!;

    if (cli.Check)
    {
        return Check.Run(options);
    }

    return await McpHost.RunAsync(options);
}
catch (CliException exception)
{
    Console.Error.WriteLine($"roslyngate: {exception.Message}");
    Console.Error.WriteLine("Try 'roslyngate --help'.");
    return 1;
}
catch (SolutionNotFoundException exception)
{
    Console.Error.WriteLine($"roslyngate: {exception.Message}");
    return 1;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"roslyngate: fatal: {exception.Message}");
    return 2;
}
