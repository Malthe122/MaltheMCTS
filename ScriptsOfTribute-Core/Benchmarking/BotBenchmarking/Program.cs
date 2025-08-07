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
using ScriptsOfTribute;
using ScriptsOfTribute.Board;
using SimpleBots.src.MaltheMCTS.Utility.HeuristicScoring.ModelEvaluation;

namespace BotBenchmarking
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var botsOption = new Option<List<string>>(
                aliases: new[] { "--bots", "-b" },
                description: "List of bot names",
                parseArgument: result => result.Tokens.Select(t => t.Value).ToList()
            )
            {
                IsRequired = true,
                AllowMultipleArgumentsPerToken = true
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

            var skipExternalOption = new Option<bool>(
                aliases: new[] { "--skipExt", "-se" },
                description: "Whether matches between external (non MaltheMCTS) bots should be skipped",
                getDefaultValue: () => true
            );

            var maltheMCTSSettingFileOption = new Option<string?>(
               aliases: new[] { "--settingsFile", "-s" },
               description: "Optional settings file for MaltheMCTS bot. If not supplied, default values are used"
               );

            var rootCommand = new RootCommand
            {
                botsOption,
                numberOfMatchupsOption,
                timeoutOption,
                threadsOption,
                nameOption,
                skipExternalOption,
                maltheMCTSSettingFileOption,
            };

            var arguments = rootCommand.Parse(args);

            rootCommand.SetHandler(
                Benchmark,
                botsOption, numberOfMatchupsOption, timeoutOption, threadsOption, nameOption, skipExternalOption, maltheMCTSSettingFileOption
            );

            await rootCommand.InvokeAsync(args);
        }

        private static async Task Benchmark(List<string> bots, int numberOfMatchups, int timeout, int threads, string benchmarkName, bool skipExternalMatches, string? maltheMCTSSettingsFile)
        {
            // FUTURE update to parallel with seperate memory, if i manage to implement that

            // FUTURE add something about win reason

            Directory.CreateDirectory(benchmarkName);

            var maltheMCTSSettings = maltheMCTSSettingsFile != null ? Settings.LoadFromFile(maltheMCTSSettingsFile) : new Settings();

            var results = PlayMatches(bots, numberOfMatchups, timeout, threads, skipExternalMatches, ConcurrencyType.ParallelWithSharedMemory, maltheMCTSSettings);

            var matchupWinrates = Utility.MatchResultsToCsv(results, numberOfMatchups);
            await File.WriteAllTextAsync(Path.Combine(benchmarkName, "matchup_winrate.csv"), matchupWinrates);

            var overallWinrates = Utility.GetOverallWinRatesText(results, numberOfMatchups * (bots.Count - 1));
            await File.WriteAllTextAsync(Path.Combine(benchmarkName, "overall_winrates.txt"), overallWinrates);

            var botPatronWinRatesText = Utility.GetBotPatronWinRatesText(results, numberOfMatchups * (bots.Count - 1));
            await File.WriteAllTextAsync(Path.Combine(benchmarkName, "bot_patron_winrates.txt"), botPatronWinRatesText);

            var sb = new StringBuilder();
            sb.AppendLine("Benchmark Details Log:");
            sb.AppendLine($"Benchmark Name: {benchmarkName}");
            sb.AppendLine($"Bots: {string.Join(", ", bots)}");
            sb.AppendLine($"Number of Matchups: {numberOfMatchups}");
            sb.AppendLine($"Timeout: {timeout}");
            sb.AppendLine($"Threads: {threads}");
            sb.AppendLine($"Skip External Matches: {skipExternalMatches}");

            sb.AppendLine();
            sb.AppendLine("MaltheMCTS Settings:");
            sb.AppendLine(maltheMCTSSettings.ToString());

            var benchmarkDetailsLog = sb.ToString();
            await File.WriteAllTextAsync(Path.Combine(benchmarkName, "benchmark_details.txt"), benchmarkDetailsLog);

            Console.WriteLine("Benchmark complete. Results logged in folder: " + benchmarkName);
        }

        private static List<Result> PlayMatches(List<string> botStrings, int numberOfMatchups, int timeout, int threads, bool skipExternalMatches, ConcurrencyType concurrencyType, Settings? maltheMCTSSettings = null)
        {
            Dictionary<string, Dictionary<string, int>> botToWins = new();
            var bots = new List<AI>();

            foreach(var botString in botStrings)
            {
                botToWins.Add(botString, new Dictionary<string, int>());
                bots.Add(Utility.CreateBot(botString));
            }

            var matchups = Utility.BuildMatchups(bots, numberOfMatchups, skipExternalMatches);

            var options = new ParallelOptions { MaxDegreeOfParallelism = threads };

            if (concurrencyType != ConcurrencyType.ParallelWithSharedMemory)
            {
                throw new NotImplementedException("Chosen concurrency type has not been implemented yet: " + concurrencyType.ToString());
            }

            var results = new List<Result>();

            Parallel.ForEach(matchups, options, matchup =>
            {
                var bot1 = matchup.Item1;
                var bot2 = matchup.Item2;

                if (bot1 is MaltheMCTS.MaltheMCTS && maltheMCTSSettings != null)
                {
                    (bot1 as MaltheMCTS.MaltheMCTS).Settings = maltheMCTSSettings;
                    if (maltheMCTSSettings.CHOSEN_SCORING_METHOD == ScoringMethod.ModelScoring)
                    {
                        (bot1 as MaltheMCTS.MaltheMCTS).PredictionEngine = EnsembledTreeModelEvaluation.GetPredictionEngine(maltheMCTSSettings.FEATURE_SET_MODEL_TYPE);
                    }
                }

                if (bot2 is MaltheMCTS.MaltheMCTS && maltheMCTSSettings != null)
                {
                    (bot2 as MaltheMCTS.MaltheMCTS).Settings = maltheMCTSSettings;
                    if (maltheMCTSSettings.CHOSEN_SCORING_METHOD == ScoringMethod.ModelScoring)
                    {
                        (bot2 as MaltheMCTS.MaltheMCTS).PredictionEngine = EnsembledTreeModelEvaluation.GetPredictionEngine(maltheMCTSSettings.FEATURE_SET_MODEL_TYPE);
                    }
                }

                var match = new ScriptsOfTribute.AI.ScriptsOfTribute(bot1, bot2, TimeSpan.FromSeconds(timeout));
                Console.WriteLine("Playing match between " + matchup.Item1.GetType().Name + " and " + matchup.Item2.GetType().Name + "...");;
                var (endGameState, fullstate) = match.Play();
                AI winner = null;
                AI looser = null;
                switch (endGameState.Winner)
                {
                    case ScriptsOfTribute.PlayerEnum.PLAYER1:
                        winner = matchup.Item1;
                        looser = matchup.Item2;
                        break;
                    case ScriptsOfTribute.PlayerEnum.PLAYER2:
                        winner = matchup.Item2;
                        looser = matchup.Item1;
                        break;
                    case ScriptsOfTribute.PlayerEnum.NO_PLAYER_SELECTED:
                        // Draw
                        break;
                }

                

                Result result = new Result
                {
                    Competitors = (matchup.Item1, matchup.Item2),
                    // Treasury is in every game, so no reason to add info about it
                    Patrons = fullstate.Patrons.Where(p => p != PatronId.TREASURY).Order().ToList(), 
                    Looser = looser,
                    Winner = winner,
                    GameEndReason = endGameState.Reason
                };

                results.Add(result);
            });

            return results;
        }
    }
}
