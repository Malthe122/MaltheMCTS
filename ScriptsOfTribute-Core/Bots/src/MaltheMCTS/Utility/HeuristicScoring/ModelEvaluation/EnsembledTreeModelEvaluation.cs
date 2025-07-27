using EnsembleTreeModelBuilder;
using Microsoft.ML;
using Microsoft.ML.AutoML;
using Microsoft.ML.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleBots.src.MaltheMCTS.Utility.HeuristicScoring.ModelEvaluation
{
    public static class EnsembledTreeModelEvaluation
    {
        private static readonly MLContext mlContext = new MLContext();
        private static Dictionary<RegressionTrainer, PredictionEngine<GameStateFeatureSetCsvRow, ModelOutput>> predictionEngines;

        //static EnsembledTreeModelEvaluation()
        //{
        //    string basePath = "MaltheMCTS/Ensemble_Tree_Models/";
        //    var fastForestModel = mlContext.Model.Load(basePath + "FastForest", out var _);
        //    var fastTreeModel = mlContext.Model.Load(basePath + "FastTree", out var _);
        //    var fastTreeTweedieModel = mlContext.Model.Load(basePath + "FastTreeTweedie", out var _);
        //    predictionEngines = new Dictionary<RegressionTrainer, PredictionEngine<GameStateFeatureSetCsvRow, ModelOutput>>
        //    {
        //        [RegressionTrainer.FastForest] = mlContext.Model.CreatePredictionEngine<GameStateFeatureSetCsvRow, ModelOutput>(fastForestModel),
        //        [RegressionTrainer.FastTree] = mlContext.Model.CreatePredictionEngine<GameStateFeatureSetCsvRow, ModelOutput>(fastTreeModel),
        //        [RegressionTrainer.FastTreeTweedie] = mlContext.Model.CreatePredictionEngine<GameStateFeatureSetCsvRow, ModelOutput>(fastTreeTweedieModel),
        //        [RegressionTrainer.LightGbm] = mlContext.Model.CreatePredictionEngine<GameStateFeatureSetCsvRow, ModelOutput>(lightGbmModel),
        //    };
        //}

        public static void LoadModel(RegressionTrainer? modelType)
        {
            if (modelType == null)
            {
                return;
            }

            string basePath = "MaltheMCTS/Ensemble_Tree_Models/";

            if (predictionEngines == null)
            {
                predictionEngines = new Dictionary<RegressionTrainer, PredictionEngine<GameStateFeatureSetCsvRow, ModelOutput>>();
            }

            switch (modelType)
            {
                case RegressionTrainer.FastForest:
                    if (!predictionEngines.ContainsKey(RegressionTrainer.FastForest))
                    {
                        var fastForestModel = mlContext.Model.Load(basePath + "FastForest", out var _);
                        predictionEngines.Add(RegressionTrainer.FastForest,
                            mlContext.Model.CreatePredictionEngine<GameStateFeatureSetCsvRow, ModelOutput>(fastForestModel));
                    }
                    break;

                case RegressionTrainer.FastTree:
                    if (!predictionEngines.ContainsKey(RegressionTrainer.FastTree))
                    {
                        var fastTreeModel = mlContext.Model.Load(basePath + "FastTree", out var _);
                        predictionEngines.Add(RegressionTrainer.FastTree,
                            mlContext.Model.CreatePredictionEngine<GameStateFeatureSetCsvRow, ModelOutput>(fastTreeModel));
                    }
                    break;

                case RegressionTrainer.FastTreeTweedie:
                    if (!predictionEngines.ContainsKey(RegressionTrainer.FastTreeTweedie))
                    {
                        var fastTreeTweedieModel = mlContext.Model.Load(basePath + "FastTreeTweedie", out var _);
                        predictionEngines.Add(RegressionTrainer.FastTreeTweedie,
                            mlContext.Model.CreatePredictionEngine<GameStateFeatureSetCsvRow, ModelOutput>(fastTreeTweedieModel));
                    }
                    break;

                default:
                    throw new ArgumentException("Unexpected model type: " + modelType);
            }

        }
        public static float GetWinProbability(GameStateFeatureSetCsvRow row, RegressionTrainer modelType)
        {
            var predictionEngine = predictionEngines[modelType];
            return predictionEngine.Predict(row).WinProbability;
        }

        private class ModelOutput
        {
            [ColumnName("Score")]
            public float WinProbability { get; set; }
        }
    }
}
