using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading.Tasks;
using System.Collections.Generic;

enum BenchmarkType
{
    Winrates,
    Computation
}

class Program
{
    static async Task<int> Main(string[] args)
    {
        var botsOption = new Option<List<string>>(
            aliases: new[] { "--bots", "-b" },
            description: "List of bot names",
            parseArgument: result => result.Tokens.Select(t => t.Value).ToList()
        )
        {
            IsRequired = true
        };

        var numberOfMatchupsOption = new Option<int>(
            aliases: new[] { "--number-of-matchups", "-n" },
            description: "Number of matchups between each bot (Round-Robin format)"
        )
        {
            IsRequired = true
        };

        var timeoutOption = new Option<int>(
            aliases: new[] { "--timeout", "-to" },
            description: "Timeout in seconds",
            getDefaultValue: () => 10
        );

        var threadsOption = new Option<int>(
            aliases: new[] { "--threads", "-t" },
            description: "Number of threads"
        )
        {
            IsRequired = true
        };

        var logFileOption = new Option<string>(
            aliases: new[] { "--log-file", "-l" },
            description: "Path to the log file"
        )
        {
            IsRequired = true
        };

        var benchmarkTypeOption = new Option<BenchmarkType>(
            aliases: new[] { "--benchmark-type", "-bt" },
            description: "Benchmark type (Winrates, Computation)",
            getDefaultValue: () => BenchmarkType.Winrates
        );

        // Root command
        var rootCommand = new RootCommand
        {
            botsOption,
            numberOfMatchupsOption,
            timeoutOption,
            threadsOption,
            logFileOption,
            benchmarkTypeOption
        };

        
    }
}