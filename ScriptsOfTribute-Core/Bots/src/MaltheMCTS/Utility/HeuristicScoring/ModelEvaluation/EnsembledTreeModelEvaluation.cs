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
    internal static class EnsembledTreeModelEvaluation
    {
        private static readonly MLContext mlContext = new MLContext();
        private static readonly Dictionary<RegressionTrainer, PredictionEngine<GameStateFeatureSetCsvRow, ModelOutput>> predictionEngines;

        static EnsembledTreeModelEvaluation()
        {
            string basePath = "MaltheMCTS/Ensemble_Tree_Models/";

            var fastForestModel = mlContext.Model.Load(basePath + "FastForest", out var _);
            var fastTreeModel = mlContext.Model.Load(basePath + "FastTree", out var _);
            var fastTreeTweedieModel = mlContext.Model.Load(basePath + "FastTreeTweedie", out var _);
            var lbfgsPoissonModel = mlContext.Model.Load(basePath + "LbfgsPoissonRegression", out var _);
            var lightGbmModel = mlContext.Model.Load(basePath + "LightGbm", out var _);
            var sdcaModel = mlContext.Model.Load(basePath + "StochasticDualCoordinateAscent", out var _);

            predictionEngines = new Dictionary<RegressionTrainer, PredictionEngine<GameStateFeatureSetCsvRow, ModelOutput>>
            {
                [RegressionTrainer.FastForest] = mlContext.Model.CreatePredictionEngine<GameStateFeatureSetCsvRow, ModelOutput>(fastForestModel),
                [RegressionTrainer.FastTree] = mlContext.Model.CreatePredictionEngine<GameStateFeatureSetCsvRow, ModelOutput>(fastTreeModel),
                [RegressionTrainer.FastTreeTweedie] = mlContext.Model.CreatePredictionEngine<GameStateFeatureSetCsvRow, ModelOutput>(fastTreeTweedieModel),
                [RegressionTrainer.LbfgsPoissonRegression] = mlContext.Model.CreatePredictionEngine<GameStateFeatureSetCsvRow, ModelOutput>(lbfgsPoissonModel),
                [RegressionTrainer.LightGbm] = mlContext.Model.CreatePredictionEngine<GameStateFeatureSetCsvRow, ModelOutput>(lightGbmModel),
                [RegressionTrainer.StochasticDualCoordinateAscent] = mlContext.Model.CreatePredictionEngine<GameStateFeatureSetCsvRow, ModelOutput>(sdcaModel)
            };
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
