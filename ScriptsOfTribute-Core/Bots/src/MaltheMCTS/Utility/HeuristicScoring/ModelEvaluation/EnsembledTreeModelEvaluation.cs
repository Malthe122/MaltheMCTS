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


        }
        public static float GetWinProbability(GameStateFeatureSetCsvRow row, RegressionTrainer modelType)
        {
            var predictionEngine = predictionEngines[modelType];
            return predictionEngine.Predict(row).WinProbability;
        }

        public static PredictionEngine<GameStateFeatureSetCsvRow, ModelOutput>? GetPredictionEngine(RegressionTrainer? modelType)
        {
            if (modelType == null)
            {
                return null;
            }

            string basePath = "MaltheMCTS/Ensemble_Tree_Models/";

            switch (modelType)
            {
                case RegressionTrainer.FastForest:
                    var fastForestModel = mlContext.Model.Load(basePath + "FastForest", out var _);
                    return mlContext.Model.CreatePredictionEngine<GameStateFeatureSetCsvRow, ModelOutput>(fastForestModel);
                case RegressionTrainer.FastTree:
                    var fastTreeModel = mlContext.Model.Load(basePath + "FastTree", out var _);
                    return mlContext.Model.CreatePredictionEngine<GameStateFeatureSetCsvRow, ModelOutput>(fastTreeModel);
                case RegressionTrainer.FastTreeTweedie:
                    var fastTreeTweedieModel = mlContext.Model.Load(basePath + "FastTreeTweedie", out var _);
                    return mlContext.Model.CreatePredictionEngine<GameStateFeatureSetCsvRow, ModelOutput>(fastTreeTweedieModel);
                default:
                    throw new ArgumentException("Unexpected model type: " + modelType);
            }
        }

        public static PredictionEngine<GameStateFeatureSetCsvRow, ModelOutput>? GetPredictionEngine(string modelPath)
        {
            var model = mlContext.Model.Load(modelPath, out var _);
            return mlContext.Model.CreatePredictionEngine<GameStateFeatureSetCsvRow, ModelOutput>(model);
        }

        public class ModelOutput
        {
            [ColumnName("Score")]
            public float WinProbability { get; set; }
        }
    }
}
