using DataCollectors_MaltheMCTS;
using MaltheMCTS;
using Microsoft.ML;
using Microsoft.ML.AutoML;
using SimpleBots.src.MaltheMCTS.Utility.HeuristicScoring;
using SimpleBots.src.MaltheMCTS.Utility.HeuristicScoring.ModelEvaluation;
using System;
using System.CommandLine;
using static SimpleBots.src.MaltheMCTS.Utility.HeuristicScoring.ModelEvaluation.EnsembledTreeModelEvaluation;

namespace IterativeSelfPlayTrainer
{
    internal class Program
    {
        private static TextWriter LOG_OUTPUT = Console.Out;
        static async Task Main(string[] args)
        {
            var numberOfMatchupsOption = new Option<int>(
                aliases: new[] { "--number-of-matches", "-n" },
                description: "Number of matches per iteration"
            )
            {
                IsRequired = true
            };

            var numberOfBenchmarkMatchupsOption = new Option<int>(
                aliases: new[] { "--number-of-benchmarks-matches", "-nb" },
                description: "Number of matchups in benchmark between iterations to choose best model",
                getDefaultValue: () => 100
                )
            {
                IsRequired = true
            };

            var numberOfIterations = new Option<int>(
                aliases: new[] { "--number-of-iteration", "-i" }
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
                aliases: new[] { "--model-name", "-mn" },
                description: "Name of final bot"
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
                numberOfMatchupsOption,
                numberOfBenchmarkMatchupsOption,
                numberOfIterations,
                timeoutOption,
                nameOption,
                maltheMCTSSettingFileOption,
            };

            var arguments = rootCommand.Parse(args);

            rootCommand.SetHandler(
                IterativelyBuildModel,
                numberOfMatchupsOption, numberOfBenchmarkMatchupsOption, numberOfIterations, timeoutOption, nameOption, maltheMCTSSettingFileOption
            );

            await rootCommand.InvokeAsync(args);
        }

        private static void IterativelyBuildModel(int matchupsPerIteration, int numberOfBenchmarkMatchupsOption, int iterationCount, int timeout, string resultModelName, string? MaltheMCTSSettingsFile)
        {
            Directory.CreateDirectory(resultModelName);

            string basePath = resultModelName;

            Settings settings;
            if (MaltheMCTSSettingsFile != null)
            {
                settings = Settings.LoadFromFile(MaltheMCTSSettingsFile!);
                Console.WriteLine("Loaded custom settings from: " + MaltheMCTSSettingsFile!);
            }
            else
            {
                settings = new Settings();
                Console.WriteLine("Using default settings");
            }

            Console.WriteLine("Starting iteration 0...");
            // For iteration 0 where we have no gamedata to train an initial model from
            settings.CHOSEN_SCORING_METHOD = ScoringMethod.Rollout;
            string iteration0DataPath = basePath + "/0" + "/data.csv";
            string iteration0ModelPath = basePath + "/0" + "/model";
            HideLogsFromConsole(0, resultModelName);
            GameDataCollection.Program.CollectMaltheMCTSData(matchupsPerIteration, timeout, iteration0DataPath, settings);
            var iteration0RsquareScore = EnsembleTreeModelBuilder.Program.TrainModel(iteration0DataPath, iteration0ModelPath, Int32.MaxValue, true, settings.FEATURE_SET_MODEL_TYPE);
            EnableConsoleLog();
            ApplyModel(iteration0ModelPath, settings.FEATURE_SET_MODEL_TYPE!.Value, iteration0RsquareScore);
            Console.WriteLine("Iteration 0 completed");

            // Resets to the correct scoring method for the remaining iterations
            settings.CHOSEN_SCORING_METHOD = ScoringMethod.ModelScoring;

            var bestBot = new MaltheMCTS.MaltheMCTS("iteration-0-MaltheMCTS", settings);
            bestBot.PredictionEngine = GetModel(0, iteration0ModelPath);
            var newBot = new MaltheMCTS.MaltheMCTS("placeholder", settings);

            for (int i = 1; i < iterationCount; i++)
            {
                Console.WriteLine("Starting iteration " + i + "...");
                HideLogsFromConsole(i, resultModelName);
                string dataPath = basePath + "/" + i + "/data";
                string modelPath = basePath + "/" + i + "/model";
                GameDataCollection.Program.CollectMaltheMCTSData(matchupsPerIteration, timeout, dataPath, settings);
                var iterationRsquareScore = EnsembleTreeModelBuilder.Program.TrainModel(dataPath, modelPath, Int32.MaxValue, true, settings.FEATURE_SET_MODEL_TYPE);
                var iterationModel = GetModel(i, modelPath);
                newBot.InstanceName = "iteration-" + i + "-bot";
                newBot.PredictionEngine = iterationModel;
                var benchmarkResult = MaltheMCTSSettingsBenchmarking.Program.PlayMatches(new List<MaltheMCTS.MaltheMCTS>() { bestBot, newBot }, numberOfBenchmarkMatchupsOption, timeout);
                EnableConsoleLog();
                if (CheckImprovement(benchmarkResult, bestBot, newBot))
                {
                    bestBot = newBot;
                    ApplyModel(modelPath, settings.FEATURE_SET_MODEL_TYPE!.Value, iterationRsquareScore);
                }
                Console.WriteLine("Iteration " + i + " completed");
            }

            return;
        }

        private static bool CheckImprovement(
            Dictionary<MaltheMCTS.MaltheMCTS, Dictionary<MaltheMCTS.MaltheMCTS, int>> benchmarkResult, 
            MaltheMCTS.MaltheMCTS defendingBot,
            MaltheMCTS.MaltheMCTS newBot)
        {
            var defendingBotResult = benchmarkResult[defendingBot];
            var newBotResult = benchmarkResult[newBot];

            Console.WriteLine(defendingBot.InstanceName + ": " + defendingBotResult[newBot] + " wins");
            Console.WriteLine(newBot.InstanceName + ": " + newBotResult[defendingBot] + " wins");

            return newBotResult[defendingBot] >= defendingBotResult[newBot];
        }


        private static void HideLogsFromConsole(int iteration, string modelPath)
        {
            var writer = new StreamWriter(modelPath + "/Library_Logs.txt", append: true) { AutoFlush = true };
            Console.SetOut(writer);
            Console.WriteLine("----------Game logs for iteration " + iteration + "----------");
            AppDomain.CurrentDomain.ProcessExit += (_, __) => writer.Dispose();
        }

        private static void EnableConsoleLog()
        {
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
        }

        private static void ApplyModel(string folderPath, RegressionTrainer modelType, double rSquaredScore)
        {
            Console.WriteLine("Applied new model from " + folderPath + " with R^2 score: " + rSquaredScore);
            string sourceFile = Directory.GetFiles(folderPath).First();
            File.Copy(sourceFile, "MaltheMCTS/Ensemble_Tree_Models/" + modelType, overwrite: true);
        }

        private static PredictionEngine<GameStateFeatureSetCsvRow, ModelOutput> GetModel(int iteration, string basePath)
        {
            return EnsembledTreeModelEvaluation.GetPredictionEngine(basePath + "/" + iteration + "/model");
        }
    }
}

//PredictionEngine<GameStateFeatureSetCsvRow, ModelOutput>