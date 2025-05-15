using System.CommandLine;
using Microsoft.ML;
using Microsoft.ML.AutoML;
using System.Text;
using Microsoft.ML.Data;
using Microsoft.ML.Trainers.FastTree;
using Microsoft.ML.Trainers.LightGbm;
using Microsoft.ML.Trainers;

namespace EnsembleTreeModelBuilder
{
    internal class Program
    {
        public const string MODELS_FOLDER = "GeneratedModels";

        private static DataViewSchema dataViewSchema;
        private static ColumnInferenceResults columnInferenceResults;

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
               description: "Time for model experiementation. Time for EACH experiment, if running multiple"
            );

            var fullIndividualBenchmarksOption = new Option<bool>(
                aliases: new[] { "--fullIndividual", "-fi" },
                description: "Flag indicating that an individual experiement will be run for each different algorithm with an output model for each",
                getDefaultValue: () => false
            );

            var rootCommand = new RootCommand
            {
                TrainingDataFileOption,
                ModelNameOption,
                ExperiementTimeOption,
                fullIndividualBenchmarksOption
            };

            var arguments = rootCommand.Parse(args);

            rootCommand.SetHandler(
                TrainModel,
                TrainingDataFileOption, ModelNameOption, ExperiementTimeOption, fullIndividualBenchmarksOption
            );

            await rootCommand.InvokeAsync(args);
        }

        private static void TrainModel(string trainingDataPath, string experiementFolder, uint experiementTime, bool individualExperiementsPerModel)
        {

            Console.WriteLine("Running training on " + trainingDataPath + "...");

            Directory.CreateDirectory(MODELS_FOLDER + "/" + experiementFolder);
            Directory.CreateDirectory(MODELS_FOLDER + "/" + experiementFolder + "/early");
            Directory.CreateDirectory(MODELS_FOLDER + "/" + experiementFolder + "/mid");
            Directory.CreateDirectory(MODELS_FOLDER + "/" + experiementFolder + "/late");
            Directory.CreateDirectory(MODELS_FOLDER + "/" + experiementFolder + "/end");

            var mlContext = new MLContext();

            var etmTrainingDataPath = trainingDataPath + "/for_etm.csv";
            var columnInference = mlContext.Auto().InferColumns(etmTrainingDataPath, labelColumnName: "WinProbability", groupColumns: false);
            IDataView fullData = mlContext.Data.LoadFromTextFile<GameStateFeatureSetCsvRow>(etmTrainingDataPath, columnInference.TextLoaderOptions);
            var trainTestSplit = mlContext.Data.TrainTestSplit(fullData, testFraction: 0.2);
            IDataView trainData = trainTestSplit.TrainSet;
            IDataView testData = trainTestSplit.TestSet;

            dataViewSchema = trainData.Schema;
            columnInferenceResults = columnInference;            

            string labelName = columnInference.ColumnInformation.LabelColumnName;
            if (individualExperiementsPerModel)
            {
                RunIndividualExperiementsPerETMModel(experiementFolder, experiementTime, mlContext, fullData, trainData, testData);

                IDataView fullEarlyData = mlContext.Data.LoadFromTextFile<GameStateLinearFeatureSetCsvRow>(trainingDataPath + "/for_linear/" + "/earlyGame.csv", columnInference.TextLoaderOptions);
                var earlyTrainTestSplit = mlContext.Data.TrainTestSplit(fullEarlyData, testFraction: 0.2);
                IDataView fullMidData = mlContext.Data.LoadFromTextFile<GameStateLinearFeatureSetCsvRow>(trainingDataPath + "/for_linear/" + "/midGame.csv", columnInference.TextLoaderOptions);
                var midTrainTestSplit = mlContext.Data.TrainTestSplit(fullMidData, testFraction: 0.2);
                IDataView fullLateData = mlContext.Data.LoadFromTextFile<GameStateLinearFeatureSetCsvRow>(trainingDataPath + "/for_linear/" + "/lateGame.csv", columnInference.TextLoaderOptions);
                var lateTrainTestSplit = mlContext.Data.TrainTestSplit(fullLateData, testFraction: 0.2);
                IDataView fullEndData = mlContext.Data.LoadFromTextFile<GameStateLinearFeatureSetCsvRow>(trainingDataPath + "/for_linear/" + "/endGame.csv", columnInference.TextLoaderOptions);
                var endTrainTestSplit = mlContext.Data.TrainTestSplit(fullEndData, testFraction: 0.2);
                RunIndividualExperiementPerLinearModel(experiementFolder, experiementTime, mlContext, fullEarlyData, earlyTrainTestSplit, "early");
                RunIndividualExperiementPerLinearModel(experiementFolder, experiementTime, mlContext, fullMidData, midTrainTestSplit, "mid");
                RunIndividualExperiementPerLinearModel(experiementFolder, experiementTime, mlContext, fullLateData, lateTrainTestSplit, "late");
                RunIndividualExperiementPerLinearModel(experiementFolder, experiementTime, mlContext, fullEndData, endTrainTestSplit, "end");

            }
            else
            {
                RunMostPreciseModelExperiement(experiementFolder, experiementTime, mlContext, fullData, trainData, testData);
            }
        }

        private static void RunIndividualExperiementsPerETMModel(string modelName, uint experiementTime, MLContext mlContext, IDataView fullData, IDataView trainData, IDataView testData)
        {
            RunIndividualExperiement(modelName, experiementTime, mlContext, fullData, trainData, testData, RegressionTrainer.FastForest);
            RunIndividualExperiement(modelName, experiementTime, mlContext, fullData, trainData, testData, RegressionTrainer.FastTree);
            RunIndividualExperiement(modelName, experiementTime, mlContext, fullData, trainData, testData, RegressionTrainer.FastTreeTweedie);
            RunIndividualExperiement(modelName, experiementTime, mlContext, fullData, trainData, testData, RegressionTrainer.LightGbm);
            //RunIndividualExperiement(experiementFolderName, experiementTime, mlContext, fullData, trainData, testData, RegressionTrainer.LbfgsPoissonRegression);
            //RunIndividualExperiement(experiementFolderName, experiementTime, mlContext, fullData, trainData, testData, RegressionTrainer.StochasticDualCoordinateAscent);
        }

        private static void RunIndividualExperiementPerLinearModel(string experiementFolder, uint experiementTime, MLContext mlContext, IDataView fullData, DataOperationsCatalog.TrainTestData trainTestSplit, string stage)
        {
            IDataView trainData = trainTestSplit.TrainSet;
            IDataView testData = trainTestSplit.TestSet;

            RunIndividualExperiement(experiementFolder + "/" + stage, experiementTime, mlContext, fullData, trainData, testData, RegressionTrainer.LbfgsPoissonRegression);
            RunIndividualExperiement(experiementFolder + "/" + stage, experiementTime, mlContext, fullData, trainData, testData, RegressionTrainer.StochasticDualCoordinateAscent);
        }

        private static void RunIndividualExperiement(string experiementFolderName, uint experiementTime, MLContext mlContext, IDataView fullData, IDataView trainData, IDataView testData, RegressionTrainer modelType)
        {
            var experimentSettings = new RegressionExperimentSettings
            {
                MaxExperimentTimeInSeconds = experiementTime,
                OptimizingMetric = RegressionMetric.RSquared
            };
            experimentSettings.Trainers.Clear();
            experimentSettings.Trainers.Add(modelType);

            var experiment = mlContext.Auto().CreateRegressionExperiment(experimentSettings);
            Console.WriteLine("Running AutoML experiment...");
            var experimentResult = experiment.Execute(trainData, testData, "WinProbability");

            var bestRun = experimentResult.BestRun;
            var bestModel = bestRun.Model;
            double bestRSquared = bestRun.ValidationMetrics.RSquared;

            // Save the model to a .zip file for later use
            mlContext.Model.Save(bestModel, fullData.Schema, MODELS_FOLDER + "/" + experiementFolderName + "/" + modelType + "_model");

            StringBuilder info = new StringBuilder();
            info.AppendLine("R²:" + bestRSquared);
            info.AppendLine();
            info.AppendLine("Best run details:");
            info.AppendLine("Type: " + bestRun.TrainerName);
            info.AppendLine("Training time: " + bestRun.RuntimeInSeconds);
            info.AppendLine("RSquared: " + bestRun.ValidationMetrics.RSquared);
            info.AppendLine("Model to string: " + bestRun.Model.ToString());

            info.AppendLine(bestRun.TrainerName + " details:\n");

            switch (modelType)
            {
                case RegressionTrainer.FastForest:
                    var model = (TransformerChain<ITransformer>)bestRun.Model;
                    var fastForest = model
                        .OfType<RegressionPredictionTransformer<FastForestRegressionModelParameters>>()
                        .First().Model;
                    AddEnsembleTreeModelInfo(info, fastForest.TrainedTreeEnsemble);
                    break;
                case RegressionTrainer.FastTree:
                case RegressionTrainer.FastTreeTweedie:
                    var fastTree = ((TransformerChain<ITransformer>)bestRun.Model)
                           .OfType<RegressionPredictionTransformer<FastTreeRegressionModelParameters>>()
                           .First().Model;
                    AddEnsembleTreeModelInfo(info, fastTree.TrainedTreeEnsemble);
                    break;
                case RegressionTrainer.LightGbm:
                    var lightGbm = ((TransformerChain<ITransformer>)bestRun.Model)
                        .OfType<RegressionPredictionTransformer<LightGbmRegressionModelParameters>>()
                        .First().Model;
                    AddEnsembleTreeModelInfo(info, lightGbm.TrainedTreeEnsemble);
                    break;
                case RegressionTrainer.LbfgsPoissonRegression:
                    var lbfgs = ((TransformerChain<ITransformer>)bestRun.Model)
                            .OfType<RegressionPredictionTransformer<PoissonRegressionModelParameters>>()
                            .First().Model;
                    AddPoissonRegressionInfo(info, lbfgs);
                    break;
                case RegressionTrainer.StochasticDualCoordinateAscent:
                    var sdca = ((TransformerChain<ITransformer>)bestRun.Model)
                        .OfType<RegressionPredictionTransformer<LinearRegressionModelParameters>>()
                        .First().Model;
                    AddSdcaModelInfo(info, sdca);
                    break;
            }

            File.WriteAllText(MODELS_FOLDER + "/" + experiementFolderName + "/" + modelType + "_Details.txt", info.ToString());
            Console.WriteLine("Best model saved in " + MODELS_FOLDER + "/" + experiementFolderName + "/" + modelType);
        }

        private static void RunMostPreciseModelExperiement(string modelName, uint experiementTime, MLContext mlContext, IDataView fullData, IDataView trainData, IDataView testData)
        {
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
            mlContext.Model.Save(bestModel, fullData.Schema, MODELS_FOLDER + "/" + modelName + "/" + "Model_" + modelName);

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

            Console.WriteLine("Best model saved in " + MODELS_FOLDER + "/" + modelName);
        }

        private static void AddEnsembleTreeModelInfo(StringBuilder info, QuantileRegressionTreeEnsemble ensemble)
        {
            info.AppendLine("Total Trees: " + ensemble.Trees.Count);

            var leaves = ensemble.Trees.Select(t => t.NumberOfLeaves).ToList();
            var nodes = ensemble.Trees.Select(t => t.NumberOfNodes).ToList();

            info.AppendLine($"Leaves per Tree - Min: {leaves.Min()}, Max: {leaves.Max()}, Avg: {leaves.Average():0.00}");
            info.AppendLine($"Nodes per Tree - Min: {nodes.Min()}, Max: {nodes.Max()}, Avg: {nodes.Average():0.00}");
            info.AppendLine("Tree Weights (first 5): " + string.Join(", ", ensemble.TreeWeights.Take(5)) + (ensemble.TreeWeights.Count > 5 ? ", ..." : ""));
            info.AppendLine("Bias: " + ensemble.Bias);
        }

        private static void AddEnsembleTreeModelInfo(StringBuilder info, RegressionTreeEnsemble ensemble)
        {
            info.AppendLine("Total Trees: " + ensemble.Trees.Count);

            var leaves = ensemble.Trees.Select(t => t.NumberOfLeaves).ToList();
            var nodes = ensemble.Trees.Select(t => t.NumberOfNodes).ToList();

            info.AppendLine($"Leaves per Tree - Min: {leaves.Min()}, Max: {leaves.Max()}, Avg: {leaves.Average():0.00}");
            info.AppendLine($"Nodes per Tree - Min: {nodes.Min()}, Max: {nodes.Max()}, Avg: {nodes.Average():0.00}");
            info.AppendLine("Tree Weights (first 5): " + string.Join(", ", ensemble.TreeWeights.Take(5)) + (ensemble.TreeWeights.Count > 5 ? ", ..." : ""));
            info.AppendLine("Bias: " + ensemble.Bias);
        }

        private static void AddSdcaModelInfo(StringBuilder info, LinearRegressionModelParameters model)
        {
            info.AppendLine($"Bias: {model.Bias}");
            info.AppendLine($"Weight Count: {model.Weights.Count}");

            var numericColumnNames = columnInferenceResults.TextLoaderOptions.Columns
                .Where(c => c.DataKind == DataKind.Single || c.DataKind == DataKind.Double)
                .Select(c => c.Name)
                .ToList();

            for (int i = 0; i < model.Weights.Count; i++)
            {
                var name = i < numericColumnNames.Count ? numericColumnNames[i] : $"Feature_{i}";
                var weight = model.Weights[i];
                info.AppendLine($"{name}: {weight:0.000}");
            }
        }

        private static void AddPoissonRegressionInfo(StringBuilder info, PoissonRegressionModelParameters model)
        {
            info.AppendLine($"Bias: {model.Bias}");
            info.AppendLine($"Weight Count: {model.Weights.Count}");

            var numericColumnNames = columnInferenceResults.TextLoaderOptions.Columns
                .Where(c => c.DataKind == DataKind.Single || c.DataKind == DataKind.Double)
                .Select(c => c.Name)
                .ToList();

            for (int i = 0; i < model.Weights.Count; i++)
            {
                var name = i < numericColumnNames.Count ? numericColumnNames[i] : $"Feature_{i}";
                var weight = model.Weights[i];
                info.AppendLine($"{name}: {weight:0.000}");
            }
        }
    }
}
