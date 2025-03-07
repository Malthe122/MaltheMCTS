using CsvHelper.Configuration;
using CsvHelper;
using System.CommandLine;
using System.Globalization;
using SimpleBots.src.MaltheMCTS.Utility.HeuristicScoring;

namespace EnsembleTreeModelBuilder
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var TrainingDataFileOption = new Option<string?>(
               aliases: new[] { "--trainingDataFile", "-t" },
               description: "File containing training data to use for model"
            );

            var ModelNameOption = new Option<string?>(
               aliases: new[] { "--modelName", "-m" },
               description: "Name for saving the built model"
            );

            var rootCommand = new RootCommand
            {
                TrainingDataFileOption,
                ModelNameOption,
            };

            var arguments = rootCommand.Parse(args);

            rootCommand.SetHandler(
                TrainModel,
                TrainingDataFileOption, ModelNameOption
            );

            await rootCommand.InvokeAsync(args);

        }

        private static void TrainModel(string trainingDataFilePath, string modelName)
        {
            var trainingFeatureSets = LoadCsvData(trainingDataFilePath);
        }

        private static List<GameStateFeatureSet> LoadCsvData(string filePath)
        {
            using var reader = new StreamReader(filePath);
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = true });

            return new List<GameStateFeatureSet>(csv.GetRecords<GameStateFeatureSet>());
        }
    }
}
