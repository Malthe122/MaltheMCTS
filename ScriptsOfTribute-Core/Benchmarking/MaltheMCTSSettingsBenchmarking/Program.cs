using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Diagnostics;
using Bots;
using ScriptsOfTribute.AI;
using System.Threading;
using BenchmarkingUtility;
using System.Collections.Concurrent;
using System.Text;
using MaltheMCTS;

namespace MaltheMCTSSettingsBenchmarking
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var file1Option = new Option<string>(
                aliases: new[] { "--file-1", "-f1" },
                description: "File 1 containing settings",
                getDefaultValue: () => "Settings1.txt"
            )
            {
                IsRequired = true,
            };

            var file2Option = new Option<string>(
                aliases: new[] { "--file-2", "-f2" },
                description: "File 2 containing settings",
                getDefaultValue: () => "Settings2.txt"
            )
            {
                IsRequired = true,
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

            var nameOption = new Option<string>(
                aliases: new[] { "--benchmark-name", "-bm" },
                description: "Name of benchmark"
            )
            {
                IsRequired = true
            };

            // Root command
            var rootCommand = new RootCommand
        {
            file1Option,
            file2Option,
            numberOfMatchupsOption,
            timeoutOption,
            threadsOption,
            nameOption,
        };

            var arguments = rootCommand.Parse(args);

            var file1 = arguments.GetValueForOption(file1Option);
            var file2 = arguments.GetValueForOption(file2Option);
            var numberOfMatchups = arguments.GetValueForOption(numberOfMatchupsOption);
            var timeout = arguments.GetValueForOption(timeoutOption);
            var threads = arguments.GetValueForOption(threadsOption);
            var benchmarkName = arguments.GetValueForOption(nameOption);

            rootCommand.SetHandler(async (file1, file2, numberOfMatchups, timeout, threads, benchmarkName) =>
            {
                var settings1 = Settings.LoadFromFile(file1);
                var settings2 = Settings.LoadFromFile(file2);

                await Benchmark(settings1, settings2, numberOfMatchups, timeout, threads, benchmarkName);
            }, file1Option, file2Option, numberOfMatchupsOption, timeoutOption, threadsOption, nameOption);

            await rootCommand.InvokeAsync(args);
        }


        private static async Task Benchmark(Settings settings1, Settings settings2, int numberOfMatchups, int timeout, int threads, string benchmarkName)
        {
            // FUTURE update to parallel with seperate memory, if i manage to implement that

            // FUTURE add something about win reason

            Directory.CreateDirectory(benchmarkName);

            var results = PlayMatches(settings1, settings2, numberOfMatchups, timeout, threads, ConcurrencyType.ParallelWithSharedMemory);

            var resultLog = "Settings 1:\n"
                + settings1.ToString()
                + "\n\nSettings 2:\n"
                + settings2.ToString()
                + "\n\nResult:\n"
                + "Setting 1 wins:\t" + results.Item1 + "\n"
                + "Setting 2 wins:\t" + results.Item2 + "\n"
                + "Draws:\t" + results.Item3;
            await File.WriteAllTextAsync(Path.Combine(benchmarkName, "results.txt"), resultLog);


            var sb = new StringBuilder();
            sb.AppendLine("Benchmark Details Log:");
            sb.AppendLine($"Benchmark Name: {benchmarkName}");
            sb.AppendLine($"Number of Matchups: {numberOfMatchups}");
            sb.AppendLine($"Timeout: {timeout}");
            sb.AppendLine($"Threads: {threads}");
            var benchmarkDetailsLog = sb.ToString();
            await File.WriteAllTextAsync(Path.Combine(benchmarkName, "benchmark_details.txt"), benchmarkDetailsLog);

            Console.WriteLine("Benchmark complete. Results logged in folder: " + benchmarkName);
        }

        private static (int, int, int) PlayMatches(Settings settings1, Settings settings2, int numberOfMatchups, int timeout, int threads, ConcurrencyType concurrencyType)
        {
            (int, int, int) res = (0, 0, 0);

            var options = new ParallelOptions { MaxDegreeOfParallelism = threads };

            if (concurrencyType != ConcurrencyType.ParallelWithSharedMemory)
            {
                throw new NotImplementedException("Chosen concurrency type has not been implemented yet: " + concurrencyType.ToString());
            }

            Parallel.For(0, numberOfMatchups, options, matchup =>
            {
                var bot1 = new MaltheMCTS.MaltheMCTS(settings: settings1);
                var bot2 = new MaltheMCTS.MaltheMCTS(settings: settings2);

                var match = new ScriptsOfTribute.AI.ScriptsOfTribute(bot1, bot2, TimeSpan.FromSeconds(timeout));
                var result = match.Play().Item1;
                switch (result.Winner)
                {
                    case ScriptsOfTribute.PlayerEnum.PLAYER1:
                        res.Item1++;
                        break;
                    case ScriptsOfTribute.PlayerEnum.PLAYER2:
                        res.Item2++;
                        break;
                    case ScriptsOfTribute.PlayerEnum.NO_PLAYER_SELECTED:
                        // Draw
                        res.Item3++;
                        break;
                }
            });

            return res;
        }
    }
}
