using RoslynGate.Cli;

try
{
    return await CommandLine.RunAsync(args);
}
catch (CliException exception)
{
    Console.Error.WriteLine($"{CliText.ErrorPrefix}{exception.Message}");
    Console.Error.WriteLine(CliText.HelpHint);
    return ExitCodes.InvalidConfiguration;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"{CliText.ErrorPrefix}fatal: {exception.Message}");
    return ExitCodes.Fatal;
}
