using System.Text;
using System;
using System.CommandLine;
using MaltheMCTS;
using SimpleBots.src.MaltheMCTS.Utility.HeuristicScoring;
using BenchmarkingUtility;
using ScriptsOfTribute.AI;
using HQL_BOT;
using System.Globalization;
using System.Threading;

namespace GameDataCollection
{
    internal class Program
    {
        public class ResultRates
        {
            public int Wins;
            public int Looses;
            public int Draws;
            public ResultRates() {
                Wins = 0;
                Looses = 0;
                Draws = 0;
            }
        }

        public static Dictionary<GameStateFeatureSet, ResultRates> FeatureSetToResultRates = new Dictionary<GameStateFeatureSet, ResultRates>();
        static async Task Main(string[] args)
        {
            var botOption = new Option<string>(
                aliases: new[] { "--bot", "-b" },
                description: "Bot to use for data collection"
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
                aliases: new[] { "--dataset-name", "-dn" },
                description: "Name of dataset"
            )
            {
                IsRequired = true
            };

            var maltheMCTSSettingFileOption = new Option<string?>(
               aliases: new[] { "--settingsFile", "-s" },
               description: "Optional settings file for MaltheMCTS bot. If not supplied, default values are used"
            );

            var rootCommand = new RootCommand
            {
                botOption,
                numberOfMatchupsOption,
                timeoutOption,
                nameOption,
                maltheMCTSSettingFileOption,
            };

            var arguments = rootCommand.Parse(args);

            rootCommand.SetHandler(
                CollectData,
                botOption, numberOfMatchupsOption, timeoutOption, nameOption, maltheMCTSSettingFileOption
            );

            await rootCommand.InvokeAsync(args);
        }

        private static void CollectData(string botString, int numberOfMatchups, int timeout, string datasetName, string? maltheMCTSSettingsFile)
        {
            Directory.CreateDirectory(datasetName);

            var maltheMCTSSettings = maltheMCTSSettingsFile != null ? MaltheMCTS.Settings.LoadFromFile(maltheMCTSSettingsFile) : new MaltheMCTS.Settings();

            Console.WriteLine("Starting playing matches...");
            PlayMatches(botString, numberOfMatchups, timeout, maltheMCTSSettings);
            Console.WriteLine("Finished matches");

            Console.WriteLine("Saving dataset...");
            ExportDatasetToCSV(datasetName);
            Console.WriteLine("Saving datasets into structure for linear models...");
            ExportDatasetToLinearModelCSVs(datasetName);

            var sb = new StringBuilder();
            sb.AppendLine($"DatasetName Name: {datasetName}");
            sb.AppendLine($"Bot: {botString}");
            sb.AppendLine($"Number of Matchups: {numberOfMatchups}");
            sb.AppendLine($"Timeout: {timeout}");

            //sb.AppendLine();
            //sb.AppendLine("MaltheMCTS Settings:");
            //sb.AppendLine(maltheMCTSSettings.ToString());

            File.WriteAllText(datasetName + "/" + "details.txt", sb.ToString());

            Console.WriteLine("Dataset complete. Stored in folder: " + datasetName);
        }

        private static void ExportDatasetToCSV(string datasetName)
        {
            var sb = new StringBuilder();

            // Headers (Excluding agent prestige strength from both players in the data, as no agent is the current decks has that effect)
            sb.AppendLine("Patron_1;Patron_2;Patron_3;Patron_4;" +
                          "CurrentPlayerPrestige;CurrentPlayerDeck_PrestigeStrength;CurrentPlayerDeck_PowerStrength;CurrentPlayerDeck_GoldStrength;CurrentPlayerDeck_MiscStrength;" +
                          "CurrentPlayerDeckComboProportion;CurrentPlayerAgent_PowerStrength;CurrentPlayerAgent_GoldStrength;CurrentPlayerAgent_MiscStrength;" +
                          "CurrentPlayerPatronFavour;OpponentPrestige;OpponentDeck_PrestigeStrength;OpponentDeck_PowerStrength;OpponentDeck_GoldStrength;OpponentDeck_MiscStrength;" +
                          "OpponentAgent_PowerStrength;OpponentAgent_GoldStrength;OpponentAgent_MiscStrength;OpponentPatronFavour;WinProbability");

            foreach (var entry in FeatureSetToResultRates)
            {
                var featureSet = entry.Key;
                var results = entry.Value;

                // Use this if i want each set represented only once, with a win probability next to it, then use winProbability variable for the last column
                // Then remove the AddDataRow loops
                //int totalGames = results.Wins + results.Looses + results.Draws;
                //double winProbability = (results.Wins + 0.5 * results.Draws) / totalGames;

                //sb.AppendLine($"{(int)featureSet.Patron1};{(int)featureSet.Patron2};{(int)featureSet.Patron3};{(int)featureSet.Patron4};" + 
                //              $"{featureSet.CurrentPlayerPrestige};" +
                //              $"{featureSet.CurrentPlayerDeckStrengths.PrestigeStrength};{featureSet.CurrentPlayerDeckStrengths.PowerStrength};{featureSet.CurrentPlayerDeckStrengths.GoldStrength};{featureSet.CurrentPlayerDeckStrengths.MiscellaneousStrength};" +
                //              $"{featureSet.CurrentPlayerDeckComboProportion};" +
                //              $"{featureSet.CurrentPlayerAgentStrengths.PowerStrength};{featureSet.CurrentPlayerAgentStrengths.GoldStrength};{featureSet.CurrentPlayerAgentStrengths.MiscellaneousStrength};" +
                //              $"{featureSet.CurrentPlayerPatronFavour};" +
                //              $"{featureSet.OpponentPrestige};" +
                //              $"{featureSet.OpponentDeckStrengths.PrestigeStrength};{featureSet.OpponentDeckStrengths.PowerStrength};{featureSet.OpponentDeckStrengths.GoldStrength};{featureSet.OpponentDeckStrengths.MiscellaneousStrength};" +
                //              $"{featureSet.OpponentAgentStrengths.PowerStrength};{featureSet.OpponentAgentStrengths.GoldStrength};{featureSet.OpponentAgentStrengths.MiscellaneousStrength};" +
                //              $"{featureSet.OpponentPatronFavour};" +
                //              $"{winProbability.ToString(CultureInfo.InvariantCulture)}");

                for(int i = 0; i < results.Looses; i++)
                {
                    AddDataRow(sb, featureSet, 0);
                }

                for (int i = 0; i < results.Draws; i++)
                {
                    AddDataRow(sb, featureSet, 0.5);
                }

                for (int i = 0; i < results.Wins; i++)
                {
                    AddDataRow(sb, featureSet, 1);
                }
            }

            // Write to file
            File.WriteAllText(datasetName + "/" + datasetName + ".csv", sb.ToString());
        }

        private static void ExportDatasetToLinearModelCSVs(string datasetName)
        {
            var earlyGame = FeatureSetToResultRates.Where(p => Math.Max(p.Key.CurrentPlayerPrestige, p.Key.OpponentPrestige) < 16).ToDictionary();
            var midGame = FeatureSetToResultRates.Where(p => Math.Max(p.Key.CurrentPlayerPrestige, p.Key.OpponentPrestige) < 30 &&
                                                                Math.Max(p.Key.CurrentPlayerPrestige, p.Key.OpponentPrestige) > 15
                                                                ).ToDictionary();
            var lateGame = FeatureSetToResultRates.Where(p => Math.Max(p.Key.CurrentPlayerPrestige, p.Key.OpponentPrestige) < 40 &&
                                                                Math.Max(p.Key.CurrentPlayerPrestige, p.Key.OpponentPrestige) > 29
                                                                ).ToDictionary();
            var endGame = FeatureSetToResultRates.Where(p => Math.Max(p.Key.CurrentPlayerPrestige, p.Key.OpponentPrestige) > 39).ToDictionary();


            var earlyCSVString = DatasetToLinearModelCSVString(earlyGame);
            var midCSVString = DatasetToLinearModelCSVString(midGame);
            var lateCSVString = DatasetToLinearModelCSVString(lateGame);
            var endCSVString = DatasetToLinearModelCSVString(endGame);

            Directory.CreateDirectory(datasetName + "/" + "for_linear");
            File.WriteAllText(datasetName + "/" + "for_linear/" + "earlyGame.csv", earlyCSVString);
            File.WriteAllText(datasetName + "/" + "for_linear/" + "midGame.csv", midCSVString);
            File.WriteAllText(datasetName + "/" + "for_linear/" + "lateGame.csv", lateCSVString);
            File.WriteAllText(datasetName + "/" + "for_linear/" + "endGame.csv", endCSVString);
        }

        private static string DatasetToLinearModelCSVString(Dictionary<GameStateFeatureSet, ResultRates> featureSetToResultRates)
        {
            var sb = new StringBuilder();

            // Headers (Excluding agent prestige strength from both players in the data, as no agent is the current decks has that effect)
            sb.AppendLine(
                          "CurrentPlayerPrestige;CurrentPlayerDeck_PrestigeStrength;CurrentPlayerDeck_PowerStrength;CurrentPlayerDeck_GoldStrength;CurrentPlayerDeck_MiscStrength;" +
                          "CurrentPlayerDeckComboProportion;CurrentPlayerAgent_PowerStrength;CurrentPlayerAgent_GoldStrength;CurrentPlayerAgent_MiscStrength;" +
                          "CurrentPlayerPatronFavour_0;CurrentPlayerPatronFavour_1;CurrentPlayerPatronFavour_2;CurrentPlayerPatronFavour_3;" +
                          "OpponentPrestige;OpponentDeck_PrestigeStrength;OpponentDeck_PowerStrength;OpponentDeck_GoldStrength;OpponentDeck_MiscStrength;" +
                          "OpponentAgent_PowerStrength;OpponentAgent_GoldStrength;OpponentAgent_MiscStrength;" +
                          "OpponentPatronFavour_0;OpponentPatronFavour_1;OpponentPatronFavour_2;OpponentPatronFavour_3;" +
                          "WinProbability");

            foreach (var entry in featureSetToResultRates)
            {
                var featureSet = entry.Key;
                var results = entry.Value;

                for (int i = 0; i < results.Looses; i++)
                {
                    AddLinearModelDataRow(sb, featureSet, 0);
                }

                for (int i = 0; i < results.Draws; i++)
                {
                    AddLinearModelDataRow(sb, featureSet, 0.5);
                }

                for (int i = 0; i < results.Wins; i++)
                {
                    AddLinearModelDataRow(sb, featureSet, 1);
                }
            }

            return sb.ToString();
        }

        private static void AddDataRow(StringBuilder sb, GameStateFeatureSet featureSet, double winProbability)
        {
            sb.AppendLine($"{featureSet.Patron_1};{featureSet.Patron_2};{featureSet.Patron_3};{featureSet.Patron_4};" +
              $"{featureSet.CurrentPlayerPrestige};" +
              $"{featureSet.CurrentPlayerDeck_PrestigeStrength};{featureSet.CurrentPlayerDeck_PowerStrength};{featureSet.CurrentPlayerDeck_GoldStrength};{featureSet.CurrentPlayerDeck_MiscStrength};" +
              $"{featureSet.CurrentPlayerDeckComboProportion};" +
              $"{featureSet.CurrentPlayerAgent_PowerStrength};{featureSet.CurrentPlayerAgent_GoldStrength};{featureSet.CurrentPlayerAgent_MiscStrength};" +
              $"{featureSet.CurrentPlayerPatronFavour};" +
              $"{featureSet.OpponentPrestige};" +
              $"{featureSet.OpponentDeck_PrestigeStrength};{featureSet.OpponentDeck_PowerStrength};{featureSet.OpponentDeck_GoldStrength};{featureSet.OpponentDeck_MiscStrength};" +
              $"{featureSet.OpponentAgent_PowerStrength};{featureSet.OpponentAgent_GoldStrength};{featureSet.OpponentAgent_MiscStrength};" +
              $"{featureSet.OpponentPatronFavour};" +
              $"{winProbability}");
        }

        private static void AddLinearModelDataRow(StringBuilder sb, GameStateFeatureSet featureSet, double winProbability)
        {
            sb.AppendLine(
              $"{featureSet.CurrentPlayerPrestige};" +
              $"{featureSet.CurrentPlayerDeck_PrestigeStrength};{featureSet.CurrentPlayerDeck_PowerStrength};{featureSet.CurrentPlayerDeck_GoldStrength};{featureSet.CurrentPlayerDeck_MiscStrength};" +
              $"{featureSet.CurrentPlayerDeckComboProportion};" +
              $"{featureSet.CurrentPlayerAgent_PowerStrength};{featureSet.CurrentPlayerAgent_GoldStrength};{featureSet.CurrentPlayerAgent_MiscStrength};" +
              $"{(featureSet.CurrentPlayerPatronFavour == 0 ? 1 : 0)};{(featureSet.CurrentPlayerPatronFavour == 1 ? 1 : 0)};{(featureSet.CurrentPlayerPatronFavour == 2 ? 1 : 0)};{(featureSet.CurrentPlayerPatronFavour == 3 ? 1 : 0)};" +
              $"{featureSet.OpponentPrestige};" +
              $"{featureSet.OpponentDeck_PrestigeStrength};{featureSet.OpponentDeck_PowerStrength};{featureSet.OpponentDeck_GoldStrength};{featureSet.OpponentDeck_MiscStrength};" +
              $"{featureSet.OpponentAgent_PowerStrength};{featureSet.OpponentAgent_GoldStrength};{featureSet.OpponentAgent_MiscStrength};" +
              $"{(featureSet.OpponentPatronFavour == 0 ? 1 : 0)};{(featureSet.OpponentPatronFavour == 1 ? 1 : 0)};{(featureSet.OpponentPatronFavour == 2 ? 1 : 0)};{(featureSet.OpponentPatronFavour == 3 ? 1 : 0)};" +
              $"{winProbability}");
        }

        private static void PlayMatches(string botString, int numberOfMatchups, int timeout, MaltheMCTS.Settings? maltheMCTSSettings = null)
        {
            for (int i = 0; i < numberOfMatchups; i++)
            {
                Console.WriteLine("Playing match " + (i + 1) + "...");
                var bot1 = CreateBot(botString, timeout, maltheMCTSSettings);
                var bot2 = CreateBot(botString, timeout, maltheMCTSSettings);
                var match = new ScriptsOfTribute.AI.ScriptsOfTribute(bot1, bot2, TimeSpan.FromSeconds(timeout));
                match.Play();
                Console.WriteLine("Finished match " + (i + 1));
            }
        }

        public static AI CreateBot(string botName, int timeout, MaltheMCTS.Settings? maltheMCTSSettings = null)
        {
            switch (botName)
            {
                //case "AlwaysFirstOptionBot":
                //    return new AlwaysFirstOptionBot();
                //case "BeamSearchBot":
                //    return new BeamSearchBot();
                //case "DecisionTreeBot":
                //    return new DecisionTreeBot();
                //case "MaxAgentsBot":
                //    return new MaxAgentsBot();
                //case "MaxPrestigeBot":
                //    return new MaxPrestigeBot();
                //case "MCTSBot":
                //    return new MCTSBot();
                case "MaltheMCTS":
                    return new DataCollectors_MaltheMCTS.MaltheMCTS_(instanceName: Guid.NewGuid().ToString(), settings: maltheMCTSSettings);
                //case "PatronFavorsBot":
                //    return new PatronFavorsBot();
                //case "PatronSelectionTimeoutBot":
                //    return new PatronSelectionTimeoutBot();
                //case "RandomBot":
                //    return new RandomBot();
                //case "RandomBotWithRandomStateExploring":
                //    return new RandomBotWithRandomStateExploring();
                //case "RandomSimulationBot":
                //    return new RandomSimulationBot();
                //case "RandomWithoutEndTurnBot":
                //    return new RandomWithoutEndTurnBot();
                //case "TurnTimeoutBot":
                //    return new TurnTimeoutBot();
                case "BestMCTS3":
                    var res = new DataCollectors_BestMCTS3.BestMCTS3();
                    res.turnTimeout = TimeSpan.FromSeconds(timeout - 0.1);
                    return res;
                default:
                    throw new ArgumentException($"Does not have a data collector for: '{botName}'");
            }
        }
    }
}
