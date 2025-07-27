using DataCollectors_MaltheMCTS;
using MaltheMCTS;
using Microsoft.ML.AutoML;
using System;
using System.CommandLine;

namespace IterativeSelfPlayTrainer
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var numberOfMatchupsOption = new Option<int>(
                aliases: new[] { "--number-of-matches", "-n" },
                description: "Number of matches per iteration"
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
                numberOfIterations,
                timeoutOption,
                nameOption,
                maltheMCTSSettingFileOption,
            };

            var arguments = rootCommand.Parse(args);

            rootCommand.SetHandler(
                IterativelyBuildModel,
                numberOfMatchupsOption, numberOfIterations, timeoutOption, nameOption, maltheMCTSSettingFileOption
            );
        }
    
        private static void IterativelyBuildModel(int matchupsPerIteration, int iterationCount, int timeout, string modelName, string? MaltheMCTSSettingsFile)
        {
            string basePath = "Training";

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
            settings.CHOSEN_SCORING_METHOD = ScoringMethod.BestMCTS3Heuristic;
            string iteration0DataPath = basePath + "/0" + "/data";
            // DISABLE LOG
            GameDataCollection.Program.CollectMaltheMCTSData(matchupsPerIteration, timeout, iteration0DataPath, settings);
            EnsembleTreeModelBuilder.Program.TrainModel(iteration0DataPath, basePath + "/0" + "/model", Int32.MaxValue, true, settings.FEATURE_SET_MODEL_TYPE);
            // RENABLE LOG
            ApplyModel()

            return;
        }
        //Console.SetOut(TextWriter.Null);
        private static void ApplyModel(string folderPath, RegressionTrainer modelType, double rSquaredScore)
        {
            Console.WriteLine("Applied new model from " + folderPath + " with R^2 score: " + rSquaredScore);
            string sourceFile = Directory.GetFiles(folderPath).First();
            File.Copy(sourceFile, "MaltheMCTS/Ensemble_Tree_Models/" + modelType, overwrite: true);
        }
    }
}
