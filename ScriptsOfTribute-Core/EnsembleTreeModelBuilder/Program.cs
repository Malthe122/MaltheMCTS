using CsvHelper.Configuration;
using CsvHelper;
using System.CommandLine;
using System.Globalization;
using Microsoft.ML;
using Microsoft.ML.AutoML;
using Microsoft.ML.Trainers.LightGbm;
using Tensorflow;
using System.Text;
using static Microsoft.ML.AutoML.AutoMLExperiment;

namespace EnsembleTreeModelBuilder
{
    internal class Program
    {
        public const string MODELS_FOLDER = "GeneratedModels";

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

            var ExperiementTimeOption = new Option<uint>(
               aliases: new[] { "--experiementTime", "-et" },
               description: "Time for model experiementation"
            );

            var rootCommand = new RootCommand
            {
                TrainingDataFileOption,
                ModelNameOption,
                ExperiementTimeOption
            };

            var arguments = rootCommand.Parse(args);

            rootCommand.SetHandler(
                TrainModel,
                TrainingDataFileOption, ModelNameOption, ExperiementTimeOption
            );

            await rootCommand.InvokeAsync(args);
        }

        private static void TrainModel(string trainingDataFilePath, string modelName, uint experiementTime)
        {

            Console.WriteLine("Running training on " + trainingDataFilePath + "...");

            Directory.CreateDirectory(MODELS_FOLDER + "/" + modelName);

            //CleanIntegersFromCsv(trainingDataFilePath); // Check if this is neccessary or puttin the properties as floats in class is enough

            var mlContext = new MLContext();

            var columnInference = mlContext.Auto().InferColumns(trainingDataFilePath, labelColumnName: "WinProbability", groupColumns: false);
            IDataView fullData = mlContext.Data.LoadFromTextFile<GameStateFeatureSetCsvRow>(trainingDataFilePath, columnInference.TextLoaderOptions);
            var trainTestSplit = mlContext.Data.TrainTestSplit(fullData, testFraction: 0.2);
            IDataView trainData = trainTestSplit.TrainSet;
            IDataView testData = trainTestSplit.TestSet;

            string labelName = columnInference.ColumnInformation.LabelColumnName;
            var experimentSettings = new RegressionExperimentSettings
            {
                MaxExperimentTimeInSeconds = experiementTime,
                OptimizingMetric = RegressionMetric.RSquared
            };
            // Change experiementSettings.Trainers here to limit certain models

            var experiment = mlContext.Auto().CreateRegressionExperiment(experimentSettings);
            Console.WriteLine("Running AutoML experiment...");
            var experimentResult = experiment.Execute(trainData, testData, "WinProbability");

            var bestRun = experimentResult.BestRun;
            var bestModel = bestRun.Model;
            double bestRSquared = bestRun.ValidationMetrics.RSquared;

            // Save the model to a .zip file for later use
            mlContext.Model.Save(bestModel, fullData.Schema, MODELS_FOLDER + "/" + modelName);

            StringBuilder info = new StringBuilder();
            info.AppendLine("R²:" + bestRSquared);
            info.AppendLine();
            info.AppendLine("Best run details:");
            info.AppendLine("Type: " + bestRun.TrainerName);
            //info.AppendLine(GetHyperparemeterString(bestRun.Model, bestRun.TrainerName));
            info.AppendLine("Training time: " + bestRun.RuntimeInSeconds);
            info.AppendLine("RSquared: " + bestRun.ValidationMetrics.RSquared);
            info.AppendLine("Model to string: " + bestRun.Model.ToString());
            File.WriteAllText(MODELS_FOLDER + "/" + modelName + "/" + "Details.txt", info.ToString());

            Console.WriteLine("Best model saved in " + MODELS_FOLDER + modelName);
        }

        private static void CleanIntegersFromCsv(string trainingDataFilePath)
        {
            var lines = File.ReadAllLines(trainingDataFilePath);
            var updatedLines = lines.Select(line =>
            {
                var values = line.Split(';');

                for (int i = 0; i < values.Length; i++)
                {
                    if (int.TryParse(values[i], out int intValue))
                    {
                        values[i] = intValue.ToString("0.0", CultureInfo.InvariantCulture);
                    }
                }

                return string.Join(";", values);
            });

            File.WriteAllLines(trainingDataFilePath, updatedLines);

            Console.WriteLine("CSV file cleaned successfully!");
        }

        //private static List<GameStateFeatureSetCsvRow> LoadCsvData(string filePath)
        //{
        //    using var reader = new StreamReader(filePath);
        //    using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = true });

        //    return new List<GameStateFeatureSetCsvRow>(csv.GetRecords<GameStateFeatureSetCsvRow>());
        //}
    }
}
