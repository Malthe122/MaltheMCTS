using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Diagnostics;
using Bots;
using ScriptsOfTribute.AI;
using Docker.DotNet;
using System.Threading;

namespace ComputationBenchmarking
{
    internal class Program
    {
        static async void Main(string[] args)
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

            // Root command
            var rootCommand = new RootCommand
        {
            botsOption,
            numberOfMatchupsOption,
            timeoutOption,
            threadsOption,
            logFileOption,
        };

            var arguments = rootCommand.Parse(args);

            var bots = arguments.GetValueForOption(botsOption);
            var numberOfMatchups = arguments.GetValueForOption(numberOfMatchupsOption);
            var timeout = arguments.GetValueForOption(timeoutOption);
            var threads = arguments.GetValueForOption(threadsOption);
            var logFilePath = arguments.GetValueForOption(logFileOption);

            BenchmarkComputation(bots, numberOfMatchups, timeout, threads, logFilePath);
        }


        private static async void BenchmarkComputation(List<string> bots, int numberOfMatchups, int timeout, int threads, string? logFilePath)
        {
            var matchups = Utility.BuildMatchups(bots);

            // Sequential

            List<float> sequentialAverageComputations = PlaySequential();

            // parallel using new method

            List<float> seperateMemoryParallelAverageComputations = new List<float>();

            // parallel with shared memory

            var sharedMemoryParallelAverageComputations = PlayParallelWithSharedMemory(numberOfMatchups, timeout, threads, matchups);
        }

        private static List<float> PlaySequential(int numberOfMatchups, int timeout, List<(AI, AI)> matchups)
        {
            var res = new List<float>();

            foreach (var matchup in matchups)
            {
                for (int i = 0; i < numberOfMatchups; i++)
                {
                    var match = new ScriptsOfTribute.AI.ScriptsOfTribute(matchup.Item1, matchup.Item2, TimeSpan.FromSeconds(timeout));
                    var matchResult = match.Play();
                    res.Add(matchResult.Item1.ComputationsPerTurn);
                }
            }

            return res;
        }

        private static new List<float> PlayParallelWithSharedMemory(int numberOfMatchups, int timeout, int threads, List<(AI, AI)> matchups)
        {
            List<float> res = new List<float>();

            var matches = new List<ScriptsOfTribute.AI.ScriptsOfTribute>();

            foreach (var matchup in matchups)
            {
                for (int i = 0; i < numberOfMatchups; i++)
                {
                    var match = new ScriptsOfTribute.AI.ScriptsOfTribute(matchup.Item1, matchup.Item2, TimeSpan.FromSeconds(timeout));
                    matches.Add(match);
                }
            }

            var options = new ParallelOptions { MaxDegreeOfParallelism = threads };
            Parallel.ForEach(matches, options, match =>
            {
                var matchResult = match.Play();
                res.Add(matchResult.Item1.ComputationsPerTurn);
            });

            return res;
        }
    }
}
