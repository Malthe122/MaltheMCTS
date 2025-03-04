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

        private static async Task CollectData(string botString, int numberOfMatchups, int timeout, string datasetName, string? maltheMCTSSettingsFile)
        {
            Directory.CreateDirectory(datasetName);

            // FUTURE readd if i start training using MaltheMCTS
            //var maltheMCTSSettings = maltheMCTSSettingsFile != null ? Settings.LoadFromFile(maltheMCTSSettingsFile) : new Settings();

            Console.WriteLine("Starting playing matches...");
            PlayMatches(botString, numberOfMatchups, timeout);
            Console.WriteLine("Finished matches");

            Console.WriteLine("Saving dataset...");
            ExportDatasetToCSV(datasetName);

            var sb = new StringBuilder();
            sb.AppendLine($"DatasetName Name: {datasetName}");
            sb.AppendLine($"Bot: {botString}");
            sb.AppendLine($"Number of Matchups: {numberOfMatchups}");
            sb.AppendLine($"Timeout: {timeout}");

            //sb.AppendLine();
            //sb.AppendLine("MaltheMCTS Settings:");
            //sb.AppendLine(maltheMCTSSettings.ToString());

            File.WriteAllText(datasetName + "/" + "details.csv", sb.ToString());

            Console.WriteLine("Dataset complete. Stored in folder: " + datasetName);
        }

        private static void ExportDatasetToCSV(string datasetName)
        {
            var sb = new StringBuilder();

            // Headers
            sb.AppendLine("CurrentPlayerPrestige;CurrentPlayerDeck_PrestigeStrength;CurrentPlayerDeck_PowerStrength;CurrentPlayerDeck_GoldStrength;CurrentPlayerDeck_MiscStrength;" +
                          "CurrentPlayerDeckComboProportion;CurrentPlayerAgent_PrestigeStrength;CurrentPlayerAgent_PowerStrength;CurrentPlayerAgent_GoldStrength;CurrentPlayerAgent_MiscStrength;" +
                          "CurrentPlayerPatronFavour;OpponentPrestige;OpponentDeck_PrestigeStrength;OpponentDeck_PowerStrength;OpponentDeck_GoldStrength;OpponentDeck_MiscStrength;" +
                          "OpponentAgent_PrestigeStrength;OpponentAgent_PowerStrength;OpponentAgent_GoldStrength;OpponentAgent_MiscStrength;OpponentPatronFavour;WinProbability");

            foreach (var entry in FeatureSetToResultRates)
            {
                var featureSet = entry.Key;
                var results = entry.Value;

                int totalGames = results.Wins + results.Looses + results.Draws;
                double winProbability = (results.Wins + 0.5 * results.Draws) / totalGames;

                sb.AppendLine($"{featureSet.CurrentPlayerPrestige};" +
                              $"{featureSet.CurrentPlayerDeckStrengths.PrestigeStrength};{featureSet.CurrentPlayerDeckStrengths.PowerStrength};{featureSet.CurrentPlayerDeckStrengths.GoldStrength};{featureSet.CurrentPlayerDeckStrengths.MiscellaneousStrength};" +
                              $"{featureSet.CurrentPlayerDeckComboProportion};" +
                              $"{featureSet.CurrentPlayerAgentStrengths.PrestigeStrength};{featureSet.CurrentPlayerAgentStrengths.PowerStrength};{featureSet.CurrentPlayerAgentStrengths.GoldStrength};{featureSet.CurrentPlayerAgentStrengths.MiscellaneousStrength};" +
                              $"{featureSet.CurrentPlayerPatronFavour};" +
                              $"{featureSet.OpponentPrestige};" +
                              $"{featureSet.OpponentDeckStrengths.PrestigeStrength};{featureSet.OpponentDeckStrengths.PowerStrength};{featureSet.OpponentDeckStrengths.GoldStrength};{featureSet.OpponentDeckStrengths.MiscellaneousStrength};" +
                              $"{featureSet.OpponentAgentStrengths.PrestigeStrength};{featureSet.OpponentAgentStrengths.PowerStrength};{featureSet.OpponentAgentStrengths.GoldStrength};{featureSet.OpponentAgentStrengths.MiscellaneousStrength};" +
                              $"{featureSet.OpponentPatronFavour};" +
                              $"{winProbability.ToString(CultureInfo.InvariantCulture)}");
            }

            // Write to file
            File.WriteAllText(datasetName + "/" + datasetName + ".csv", sb.ToString());
        }

        private static void PlayMatches(string botString, int numberOfMatchups, int timeout)
        {
            for (int i = 0; i < numberOfMatchups; i++)
            {
                Console.WriteLine("Playing match " + (i + 1) + "...");
                var bot1 = CreateBot(botString);
                var bot2 = CreateBot(botString);
                var match = new ScriptsOfTribute.AI.ScriptsOfTribute(bot1, bot2, TimeSpan.FromSeconds(timeout));
                match.Play();
                Console.WriteLine("Finished match " + (i + 1));
            }
        }

        public static AI CreateBot(string botName)
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
                //case "MaltheMCTS":
                //    return new MaltheMCTS.MaltheMCTS(instanceName: Guid.NewGuid().ToString());
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
                    return new DataCollectors_BestMCTS3.BestMCTS3();
                default:
                    throw new ArgumentException($"Does not have a data collector for: '{botName}'");
            }
        }
    }
}
