using System.CommandLine;
using Extractor;
using Insights;

namespace Swissquote.Analyzer.Cli;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("Swissquote Kontoauszug Analyzer - Extract bank statements from PDF to JSON");

        rootCommand.Subcommands.Add(new ExtractCommand());
        rootCommand.Subcommands.Add(new InsightsCommand());

        return await rootCommand.Parse(args).InvokeAsync();
    }
}
