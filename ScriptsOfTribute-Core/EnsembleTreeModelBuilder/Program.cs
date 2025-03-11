using CsvHelper.Configuration;
using CsvHelper;
using System.CommandLine;
using System.Globalization;
using SimpleBots.src.MaltheMCTS.Utility.HeuristicScoring;
using static System.Runtime.InteropServices.JavaScript.JSType;
using Microsoft.ML;
using Microsoft.ML.AutoML;
using Microsoft.ML.Trainers.LightGbm;
using Tensorflow;
using System.Text;
using HQL_BOT;
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

            CleanIntegersFromCsv(trainingDataFilePath);

            var mlContext = new MLContext();

            //Console.WriteLine("Loading data from: '" + trainingDataFilePath + "'...");
            //var dataSets = mlContext.Data.LoadFromTextFile<GameStateFeatureSetCsvRow>(
            //path: trainingDataFilePath, hasHeader: true, separatorChar: ';');
            //Console.WriteLine("Loaded training data with " + dataSets.Schema.Count + " collumns");

            var columnInference = mlContext.Auto().InferColumns(trainingDataFilePath, labelColumnName: "WinProbability", groupColumns: false);
            IDataView fullData = mlContext.Data.LoadFromTextFile<GameStateFeatureSetCsvRow>(trainingDataFilePath, columnInference.TextLoaderOptions);

            var trainTestSplit = mlContext.Data.TrainTestSplit(fullData, testFraction: 0.2);
            IDataView trainData = trainTestSplit.TrainSet;
            IDataView testData = trainTestSplit.TestSet;

            string labelName = columnInference.ColumnInformation.LabelColumnName;
            var pipeline = mlContext.Auto().Featurizer(trainData, columnInformation: columnInference.ColumnInformation)
                .Append(mlContext.Auto().Regression(
                    labelColumnName: labelName,
                    // Here i can allow only one type, e.g. FastForest if i want faster inference time in exchange for lower accuracy
                    useFastForest: true,
                    useFastTree: true,
                    useLbfgsPoissonRegression: true,
                    useSdca: true,
                    useLgbm: true
            ));

            var experiementSettings = new AutoMLExperimentSettings
            {
                MaxExperimentTimeInSeconds = experiementTime,
            };

            var experiment = mlContext.Auto().CreateExperiment(experiementSettings);
            experiment.SetPipeline(pipeline)
                      .SetRegressionMetric(RegressionMetric.RSquared, labelColumn: labelName)
                      .SetDataset(trainData, testData);

            Console.WriteLine("Running AutoML experiment...");
            var experimentResult = experiment.RunAsync().GetAwaiter().GetResult();
            var bestModel = experimentResult.Model;
            double bestRSquared = experimentResult.Metric;  // Best R² on validation (test) set

            // Save the model to a .zip file for later use
            mlContext.Model.Save(bestModel, trainData.Schema, MODELS_FOLDER + modelName + "/" + modelName + ".zip");

            string bestPipeline = pipeline.ToString(experimentResult.TrialSettings.Parameter);
            StringBuilder info = new StringBuilder();
            info.AppendLine("R²:" + bestRSquared);
            info.AppendLine();
            info.AppendLine("Pipeline details:");
            info.AppendLine(bestPipeline);
            File.WriteAllText(MODELS_FOLDER + modelName + "/" + "Details.txt", info.ToString());

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
