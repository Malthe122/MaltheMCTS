using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Collections.Generic;
using System.Diagnostics;
using Bots;
using ScriptsOfTribute.AI;
using BenchmarkingUtility;
using System.Collections.Concurrent;
using System.Text;
using MaltheMCTS;
using System.IO;

namespace MaltheMCTSSettingsBenchmarking
{
    public class Program
    {
        static async Task Main(string[] args)
        {
            var settingsFolderOption = new Option<string>(
                aliases: new[] { "--settings-folder", "-sf" },
                description: "Folder containing different setting files",
                getDefaultValue: () => "Settings"
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
            settingsFolderOption,
            numberOfMatchupsOption,
            timeoutOption,
            nameOption,
        };

            var arguments = rootCommand.Parse(args);

            var settingsFolder = arguments.GetValueForOption(settingsFolderOption);
            var numberOfMatchups = arguments.GetValueForOption(numberOfMatchupsOption);
            var timeout = arguments.GetValueForOption(timeoutOption);
            var benchmarkName = arguments.GetValueForOption(nameOption);

            rootCommand.SetHandler(async (settingsFolder, numberOfMatchups, timeout, benchmarkName) =>
            {
                var settingSets = new List<(string,Settings)>();
                var filePaths = Directory.GetFiles(settingsFolder);

                foreach(var filePath in filePaths)
                {
                    var settingSet = Settings.LoadFromFile(filePath);
                    var fileName = filePath.Substring(filePath.LastIndexOf('\\') + 1);
                    settingSets.Add((fileName, settingSet));
                }

                await Benchmark(settingSets, numberOfMatchups, timeout, benchmarkName);
            }, settingsFolderOption, numberOfMatchupsOption, timeoutOption, nameOption);

            await rootCommand.InvokeAsync(args);
        }


        private static async Task Benchmark(List<(string, Settings)> settingsSets, int numberOfMatchups, int timeout, string benchmarkName)
        {
            // FUTURE update to parallel with seperate memory, if i manage to implement that

            // FUTURE add something about win reason

            Directory.CreateDirectory(benchmarkName);

            var bots = new List<MaltheMCTS.MaltheMCTS>();
            foreach (var settingSet in settingsSets)
            {
                bots.Add(new MaltheMCTS.MaltheMCTS(settingSet.Item1, settingSet.Item2));
            }

            var results = PlayMatches(bots, numberOfMatchups, timeout);

            var settingsLog = "SETTINGS:\n\n";

            foreach (var result in results)
            {
                var bot = result.Key;
                settingsLog += bot.InstanceName + ":\n";
                settingsLog += bot.Settings.ToString();
                settingsLog += "\n\n";
            }

            var stringResults = results.ToDictionary(
                outer => outer.Key.InstanceName,
                outer => outer.Value.ToDictionary(
                    inner => inner.Key.InstanceName,
                    inner => inner.Value
                )
            );

            var csvString = Utility.MatchResultsToCsv(stringResults, numberOfMatchups);

            var overallWinrates = Utility.GetOverallWinRatesText(stringResults, numberOfMatchups * (bots.Count - 1));
            await File.WriteAllTextAsync(Path.Combine(benchmarkName, "overall_winrates.txt"), overallWinrates);

            var sb = new StringBuilder();
            sb.AppendLine("Benchmark Details Log:");
            sb.AppendLine($"Benchmark Name: {benchmarkName}");
            sb.AppendLine($"Number of Matchups: {numberOfMatchups}");
            sb.AppendLine($"Timeout: {timeout}");
            sb.AppendLine($"Threads: 1");
            var benchmarkDetailsLog = sb.ToString();

            await File.WriteAllTextAsync(Path.Combine(benchmarkName, "benchmark_details.txt"), benchmarkDetailsLog);
            await File.WriteAllTextAsync(Path.Combine(benchmarkName, "settings_log.txt"), settingsLog);
            await File.WriteAllTextAsync(Path.Combine(benchmarkName, "results.csv"), csvString);

            Console.WriteLine("Benchmark complete. Results logged in folder: " + benchmarkName);
        }

        public static Dictionary<MaltheMCTS.MaltheMCTS, Dictionary<MaltheMCTS.MaltheMCTS, int>> PlayMatches(List<MaltheMCTS.MaltheMCTS> bots, int numberOfMatchups, int timeout)
        {
            Dictionary<AI, Dictionary<AI, int>> botToWins = new();

            foreach (var bot in bots)
            {
                botToWins.Add(bot, new Dictionary<AI, int>());
            }

            var matchups = Utility.BuildMatchups2(bots.Cast<AI>().ToList(), numberOfMatchups, false);
            
            foreach (var matchup in matchups)
            {
                var match = new ScriptsOfTribute.AI.ScriptsOfTribute(matchup.Item1, matchup.Item2, TimeSpan.FromSeconds(timeout));
                var result = match.Play().Item1;
                switch (result.Winner)
                {
                    case ScriptsOfTribute.PlayerEnum.PLAYER1:
                        Utility.AddWinToResultContainer(matchup.Item1, matchup.Item2, botToWins);
                        break;
                    case ScriptsOfTribute.PlayerEnum.PLAYER2:
                        Utility.AddWinToResultContainer(matchup.Item2, matchup.Item1, botToWins);
                        break;
                    case ScriptsOfTribute.PlayerEnum.NO_PLAYER_SELECTED:
                        // Draw
                        break;
                }
            }

            var convertedResult = botToWins.ToDictionary(
                outer => (MaltheMCTS.MaltheMCTS)outer.Key,
                outer => outer.Value.ToDictionary(
                inner => (MaltheMCTS.MaltheMCTS)inner.Key,
                inner => inner.Value
            )
);
            return convertedResult;
        }
    }
}
