using DataCollectors_MaltheMCTS;
using MaltheMCTS;
using Microsoft.ML;
using Microsoft.ML.AutoML;
using SimpleBots.src.MaltheMCTS.Utility.HeuristicScoring;
using SimpleBots.src.MaltheMCTS.Utility.HeuristicScoring.ModelEvaluation;
using System;
using System.CommandLine;
using System.Globalization;
using static SimpleBots.src.MaltheMCTS.Utility.HeuristicScoring.ModelEvaluation.EnsembledTreeModelEvaluation;

namespace IterativeSelfPlayTrainer
{
    internal class Program
    {
        private static TextWriter LOG_OUTPUT = Console.Out;
        static async Task Main(string[] args)
        {
            // ML.Net had problems readings csv on system with danish culture, no matter whether they were formatted as danish or english
            // so before starting this, i change the culture to english
            var culture = CultureInfo.CreateSpecificCulture("en-US");
            culture.NumberFormat.NumberDecimalSeparator = ".";

            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;

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

            var startFromRolloutOption = new Option<bool>(
                aliases: new[] { "--rollout-start", "-rs" },
                description: "Whether first iteration should be done using rollout. Use if no model is available",
                getDefaultValue: () => false
            );

            var rootCommand = new RootCommand
            {
                numberOfMatchupsOption,
                numberOfBenchmarkMatchupsOption,
                numberOfIterations,
                timeoutOption,
                nameOption,
                maltheMCTSSettingFileOption,
                startFromRolloutOption
            };

            var arguments = rootCommand.Parse(args);

            rootCommand.SetHandler(
                IterativelyBuildModel,
                numberOfMatchupsOption, numberOfBenchmarkMatchupsOption, numberOfIterations, timeoutOption, nameOption, maltheMCTSSettingFileOption, startFromRolloutOption
            );

            await rootCommand.InvokeAsync(args);
        }

        private static void IterativelyBuildModel(int matchupsPerIteration, int numberOfBenchmarkMatchupsOption, int iterationCount, int timeout, string resultModelName, string? MaltheMCTSSettingsFile, bool startFromRollout)
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

            MaltheMCTS.MaltheMCTS bestBot;

            if (startFromRollout)
            {

                var originalBotBuffer = settings.ITERATION_COMPLETION_MILLISECONDS_BUFFER;
                Console.WriteLine("Starting iteration 0...");

                settings.CHOSEN_SCORING_METHOD = ScoringMethod.Rollout;
                settings.ITERATION_COMPLETION_MILLISECONDS_BUFFER = 500;

                string iteration0DataPath = basePath + "/0" + "/data.csv";
                string iteration0ModelPath = basePath + "/0" + "/model";
                HideLogsFromConsole(0, resultModelName);
                GameDataCollection.Program.CollectMaltheMCTSData(matchupsPerIteration, timeout, iteration0DataPath, settings);
                var iteration0RsquareScore = EnsembleTreeModelBuilder.Program.TrainModel(iteration0DataPath, iteration0ModelPath, Int32.MaxValue, true, settings.FEATURE_SET_MODEL_TYPE);
                EnableConsoleLog();
                ApplyModel(iteration0ModelPath, settings.FEATURE_SET_MODEL_TYPE!.Value, iteration0RsquareScore);
                Console.WriteLine("Iteration 0 completed");

                // Resets settings to original
                settings.CHOSEN_SCORING_METHOD = ScoringMethod.LightGbmScoring;
                settings.ITERATION_COMPLETION_MILLISECONDS_BUFFER = originalBotBuffer;

                bestBot = new MaltheMCTS.MaltheMCTS("iteration-0-MaltheMCTS", settings);
                bestBot.PredictionEngine = GetModel(iteration0ModelPath, settings.FEATURE_SET_MODEL_TYPE!.Value);
            }
            else
            {
                bestBot = new MaltheMCTS.MaltheMCTS("starting-MaltheMCTS", settings);
            }


            for (int i = 1; i < iterationCount; i++)
            {
                Console.WriteLine("Starting iteration " + i + " (with " + matchupsPerIteration + " games)...");
                HideLogsFromConsole(i, resultModelName);
                string dataPath = basePath + "/" + i + "/data.csv";
                string modelPath = basePath + "/" + i + "/model";
                GameDataCollection.Program.CollectMaltheMCTSData(matchupsPerIteration, timeout, dataPath, settings);
                var iterationRsquareScore = EnsembleTreeModelBuilder.Program.TrainModel(dataPath, modelPath, Int32.MaxValue, true, settings.FEATURE_SET_MODEL_TYPE);
                var iterationModel = GetModel(modelPath, settings.FEATURE_SET_MODEL_TYPE!.Value);
                var newBot = new MaltheMCTS.MaltheMCTS(instanceName: "iteration-" + i + "-MaltheMCTS", settings);
                newBot.PredictionEngine = iterationModel;
                var benchmarkResult = MaltheMCTSSettingsBenchmarking.Program.PlayMatches(new List<MaltheMCTS.MaltheMCTS>() { bestBot, newBot }, numberOfBenchmarkMatchupsOption, timeout);
                EnableConsoleLog();
                if (CheckImprovement(benchmarkResult, bestBot, newBot))
                {
                    bestBot = newBot;
                    // For data collection
                    ApplyModel(modelPath, settings.FEATURE_SET_MODEL_TYPE!.Value, iterationRsquareScore);
                }
                Console.WriteLine("Iteration " + i + " completed");

                //Hardcoded quickfix, TODO should be added as argument. I want more games through later generations
                matchupsPerIteration += 5;
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

            var defendingWins = (defendingBotResult.ContainsKey(newBot) ? defendingBotResult[newBot] : 0);
            var newWins = (newBotResult.ContainsKey(defendingBot) ? newBotResult[defendingBot] : 0);

            Console.WriteLine(defendingBot.InstanceName + ": " + defendingWins + " wins");
            Console.WriteLine(newBot.InstanceName + ": " + newWins + " wins");

            return newWins >= defendingWins;
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
            string sourceFile = folderPath + "/" + modelType + "_model";
            File.Copy(sourceFile, "MaltheMCTS/Ensemble_Tree_Models/" + modelType, overwrite: true);
        }

        private static PredictionEngine<GameStateFeatureSetCsvRow, ModelOutput> GetModel(string path, RegressionTrainer modelType)
        {
            return EnsembledTreeModelEvaluation.GetPredictionEngine(path, modelType);
        }
    }
}

//PredictionEngine<GameStateFeatureSetCsvRow, ModelOutput>