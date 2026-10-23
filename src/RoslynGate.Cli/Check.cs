using RoslynGate.Engine;
using RoslynGate.Mcp;

namespace RoslynGate.Cli;

public static class Check
{
    public static int Run(ServerOptions options)
    {
        try
        {
            var solution = SolutionLocator.Locate(options.SolutionPath, Environment.CurrentDirectory);

            Console.Out.WriteLine($"roslyngate  {BuildInfo.Version}");
            Console.Out.WriteLine($"solution    {solution}");
            Console.Out.WriteLine($"detail      {options.Detail.ToString().ToLowerInvariant()}");
            Console.Out.WriteLine($"ref-limit   {options.ReferenceLimit}");
            Console.Out.WriteLine($"search-limit {options.SearchLimit}");
            Console.Out.WriteLine($"prewarm     {(options.Prewarm ? "on" : "off")}");
            Console.Out.WriteLine($"log-level   {options.LogLevel.ToString().ToLowerInvariant()}");
            Console.Out.WriteLine("status      ok");
            return 0;
        }
        catch (SolutionNotFoundException exception)
        {
            Console.Error.WriteLine($"roslyngate: {exception.Message}");
            return 1;
        }
    }
}
