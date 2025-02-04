using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Diagnostics;
using Bots;
using ScriptsOfTribute.AI;

enum BenchmarkType
{
    Winrates,
    Computation
}

class Program
{
    private const string PATH_TO_GAMERUNNER = "..\\GameRunner\\bin\\Release\\net7.0";

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

        var arguments = rootCommand.Parse(args);

        var bots = arguments.GetValueForOption(botsOption);
        var numberOfMatchups = arguments.GetValueForOption(numberOfMatchupsOption);
        var timeout = arguments.GetValueForOption(timeoutOption);
        var threads = arguments.GetValueForOption(threadsOption);
        var logFilePath = arguments.GetValueForOption(logFileOption);
        var benchmarkType = arguments.GetValueForOption(benchmarkTypeOption);

        switch(benchmarkType)
        {
            case BenchmarkType.Winrates:
                // TODO
                break;
            case BenchmarkType.Computation:
                BenchmarkComputation(bots, numberOfMatchups, timeout, threads, logFilePath);
                break;
        }
    }

    private static async void BenchmarkComputation(List<string> bots, int numberOfMatchups, int timeout, int threads, string? logFilePath)
    {
        var matchups = BuildMatchups(bots);

        // Sequential

        List<float> sequentialAverageComputations = new List<float>(); 

        foreach (var matchup in matchups)
        {
            for(int i = 0; i  < numberOfMatchups; i++)
            {
                var match = new ScriptsOfTribute.AI.ScriptsOfTribute(matchup.Item1, matchup.Item2, TimeSpan.FromSeconds(timeout));
                var matchResult = match.Play();
                sequentialAverageComputations.Add(matchResult.Item1.ComputationsPerTurn);
            }
        }

        // parallel using new method

        // TODO

        // parallel with shared memory

        List<float> sharedMemoryParallelAverageComputations = new List<float>();

        var matches = new List<ScriptsOfTribute.AI.ScriptsOfTribute>();

        foreach (var matchup in matchups)
        {
            for (int i = 0; i < numberOfMatchups; i++)
            {
                var match = new ScriptsOfTribute.AI.ScriptsOfTribute(matchup.Item1, matchup.Item2, TimeSpan.FromSeconds(timeout));
                matches.Add(match);
            }
        }

        Parallel.ForEach(matches, match =>
        {
            var matchResult = match.Play();
            sharedMemoryParallelAverageComputations.Add(matchResult.Item1.ComputationsPerTurn);
        });
    }

    public static AI CreateBot(string botName)
    {
        switch (botName)
        {
            case "AlwaysFirstOptionBot":
                return new AlwaysFirstOptionBot();
            case "BeamSearchBot":
                return new BeamSearchBot();
            case "DecisionTreeBot":
                return new DecisionTreeBot();
            case "MaxAgentsBot":
                return new MaxAgentsBot();
            case "MaxPrestigeBot":
                return new MaxPrestigeBot();
            case "MCTSBot":
                return new MCTSBot();
            case "MaltheMCTS":
                return new MaltheMCTS.MaltheMCTS();
            case "PatronFavorsBot":
                return new PatronFavorsBot();
            case "PatronSelectionTimeoutBot":
                return new PatronSelectionTimeoutBot();
            case "RandomBot":
                return new RandomBot();
            case "RandomBotWithRandomStateExploring":
                return new RandomBotWithRandomStateExploring();
            case "RandomSimulationBot":
                return new RandomSimulationBot();
            case "RandomWithoutEndTurnBot":
                return new RandomWithoutEndTurnBot();
            case "TurnTimeoutBot":
                return new TurnTimeoutBot();
            default:
                throw new ArgumentException($"Bot '{botName}' is not recognized.");
            // TODO add tournament bots
        }
    }

    private static List<(AI, AI)> BuildMatchups(List<string> bots)
    {
        List<(AI, AI)> matchups = new List<(AI, AI)>();

        for (int i = 0; i < bots.Count; i++)
        {
            for (int j = i + 1; j < bots.Count; j++)
            {
                matchups.Add((CreateBot(bots[i]), CreateBot(bots[j])));
            }
        }

        return matchups;
    }
}