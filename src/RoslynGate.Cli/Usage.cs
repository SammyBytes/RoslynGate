namespace RoslynGate.Cli;

public static class Usage
{
    public const string Text =
        """
        RoslynGate - Roslyn-powered MCP server for .NET symbols

        Usage:
          roslyngate [options]

        Options:
          -s, --solution <path>    .sln/.slnx file or directory (default: auto-detect)
          -l, --log-level <level>  error, warn, info, debug (default: error)
              --detail <level>     compact or verbose (default: compact)
              --ref-limit <n>      default 50, max 200
              --search-limit <n>   default 20, max 50
              --prewarm            load the solution in the background (default)
              --no-prewarm         disable background loading
              --check              validate configuration and exit
          -h, --help               show this help
          -V, --version            show the version

        Environment:
          ROSLYGATE_SOLUTION, ROSLYGATE_LOG_LEVEL, ROSLYGATE_DETAIL,
          ROSLYGATE_REF_LIMIT, ROSLYGATE_SEARCH_LIMIT, ROSLYGATE_PREWARM

        Exit codes:
          0  normal end of input, help, version, or successful check
          1  invalid configuration or solution
          2  fatal startup failure

        Examples:
          roslyngate --solution ./src
          roslyngate --check
          roslyngate --solution . --detail verbose
        """;
}
