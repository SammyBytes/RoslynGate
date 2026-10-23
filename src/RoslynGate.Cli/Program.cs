using RoslynGate.Cli;

try
{
    return await CommandLine.RunAsync(args);
}
catch (CliException exception)
{
    Console.Error.WriteLine($"roslyngate: {exception.Message}");
    Console.Error.WriteLine("Try 'roslyngate --help'.");
    return 1;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"roslyngate: fatal: {exception.Message}");
    return 2;
}
