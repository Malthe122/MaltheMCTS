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
    internal static class EnsembledTreeModelEvaluation
    {
        private static readonly MLContext mlContext = new MLContext();
        private static readonly Dictionary<RegressionTrainer, PredictionEngine<GameStateFeatureSetCsvRow, ModelOutput>> PREDICTION_ENGINES;

        /// <summary>
        /// Temp quick fix variable for benchmarking fast forest engines against each other
        /// </summary>
        private static readonly PredictionEngine<GameStateFeatureSetCsvRow, ModelOutput> NEW_FAST_FOREST_PREDICTION_ENGINE;

        private static readonly Dictionary<(RegressionTrainer, GameStage), PredictionEngine<GameStateLinearFeatureSetCsvRow, ModelOutput>> LINEAR_PREDICTION_ENGINES;

        static EnsembledTreeModelEvaluation()
        {
            string basePath = "MaltheMCTS/Ensemble_Tree_Models/";

            var fastForestModel = mlContext.Model.Load(basePath + "FastForest", out var _);
            var fastTreeModel = mlContext.Model.Load(basePath + "FastTree", out var _);
            var fastTreeTweedieModel = mlContext.Model.Load(basePath + "FastTreeTweedie", out var _);
            //var lbfgsPoissonModel = mlContext.Model.Load(basePath + "LbfgsPoissonRegression", out var _);
            var lightGbmModel = mlContext.Model.Load(basePath + "LightGbm", out var _);
            //var sdcaModel = mlContext.Model.Load(basePath + "StochasticDualCoordinateAscent", out var _);

            PREDICTION_ENGINES = new Dictionary<RegressionTrainer, PredictionEngine<GameStateFeatureSetCsvRow, ModelOutput>>
            {
                [RegressionTrainer.FastForest] = mlContext.Model.CreatePredictionEngine<GameStateFeatureSetCsvRow, ModelOutput>(fastForestModel),
                [RegressionTrainer.FastTree] = mlContext.Model.CreatePredictionEngine<GameStateFeatureSetCsvRow, ModelOutput>(fastTreeModel),
                [RegressionTrainer.FastTreeTweedie] = mlContext.Model.CreatePredictionEngine<GameStateFeatureSetCsvRow, ModelOutput>(fastTreeTweedieModel),
                //[RegressionTrainer.LbfgsPoissonRegression] = mlContext.Model.CreatePredictionEngine<GameStateFeatureSetCsvRow, ModelOutput>(lbfgsPoissonModel),
                [RegressionTrainer.LightGbm] = mlContext.Model.CreatePredictionEngine<GameStateFeatureSetCsvRow, ModelOutput>(lightGbmModel),
                //[RegressionTrainer.StochasticDualCoordinateAscent] = mlContext.Model.CreatePredictionEngine<GameStateFeatureSetCsvRow, ModelOutput>(sdcaModel)
            };
            
            // Temp quick fix stuff should be deleted
            var NEW_FAST_FOREST = mlContext.Model.Load(basePath + "FastForest_new", out var _);
            NEW_FAST_FOREST_PREDICTION_ENGINE = mlContext.Model.CreatePredictionEngine<GameStateFeatureSetCsvRow, ModelOutput>(NEW_FAST_FOREST);


            var lbfgsPoissonEarlyModel = mlContext.Model.Load(basePath + "/early/LbfgsPoissonRegression", out var _);
            var lbfgsPoissonMidModel = mlContext.Model.Load(basePath + "/mid/LbfgsPoissonRegression", out var _);
            var lbfgsPoissonLateModel = mlContext.Model.Load(basePath + "/late/LbfgsPoissonRegression", out var _);
            var lbfgsPoissonEndModel = mlContext.Model.Load(basePath + "/end/LbfgsPoissonRegression", out var _);

            var sdcaEarlyModel = mlContext.Model.Load(basePath + "/early/StochasticDualCoordinateAscent", out var _);
            var sdcaMidModel = mlContext.Model.Load(basePath + "/mid/StochasticDualCoordinateAscent", out var _);
            var sdcaLateModel = mlContext.Model.Load(basePath + "/late/StochasticDualCoordinateAscent", out var _);
            var sdcaEndModel = mlContext.Model.Load(basePath + "/end/StochasticDualCoordinateAscent", out var _);


            LINEAR_PREDICTION_ENGINES = new Dictionary<(RegressionTrainer, GameStage), PredictionEngine<GameStateLinearFeatureSetCsvRow, ModelOutput>>
            {
                [(RegressionTrainer.LbfgsPoissonRegression, GameStage.Early)] = mlContext.Model.CreatePredictionEngine<GameStateLinearFeatureSetCsvRow, ModelOutput>(lbfgsPoissonEarlyModel),
                [(RegressionTrainer.LbfgsPoissonRegression, GameStage.Mid)] = mlContext.Model.CreatePredictionEngine<GameStateLinearFeatureSetCsvRow, ModelOutput>(lbfgsPoissonMidModel),
                [(RegressionTrainer.LbfgsPoissonRegression, GameStage.Late)] = mlContext.Model.CreatePredictionEngine<GameStateLinearFeatureSetCsvRow, ModelOutput>(lbfgsPoissonLateModel),
                [(RegressionTrainer.LbfgsPoissonRegression, GameStage.End)] = mlContext.Model.CreatePredictionEngine<GameStateLinearFeatureSetCsvRow, ModelOutput>(lbfgsPoissonEndModel),

                [(RegressionTrainer.StochasticDualCoordinateAscent, GameStage.Early)] = mlContext.Model.CreatePredictionEngine<GameStateLinearFeatureSetCsvRow, ModelOutput>(sdcaEarlyModel),
                [(RegressionTrainer.StochasticDualCoordinateAscent, GameStage.Mid)] = mlContext.Model.CreatePredictionEngine<GameStateLinearFeatureSetCsvRow, ModelOutput>(sdcaMidModel),
                [(RegressionTrainer.StochasticDualCoordinateAscent, GameStage.Late)] = mlContext.Model.CreatePredictionEngine<GameStateLinearFeatureSetCsvRow, ModelOutput>(sdcaLateModel),
                [(RegressionTrainer.StochasticDualCoordinateAscent, GameStage.End)] = mlContext.Model.CreatePredictionEngine<GameStateLinearFeatureSetCsvRow, ModelOutput>(sdcaEndModel),
            };


        }

        public static float GetWinProbability(GameStateFeatureSetCsvRow row, RegressionTrainer modelType)
        {
            var predictionEngine = PREDICTION_ENGINES[modelType];
            return predictionEngine.Predict(row).WinProbability;
        }

        /// <summary>
        /// Temp quick fix method should be deleted
        public static float GetNewForestWinProbability(GameStateFeatureSetCsvRow row)
        {
            return NEW_FAST_FOREST_PREDICTION_ENGINE.Predict(row).WinProbability;
        }

        public static float GetWinProbability(GameStateLinearFeatureSetCsvRow row, RegressionTrainer modelType, GameStage stage)
        {
            var linearPredictionEngine = LINEAR_PREDICTION_ENGINES[(modelType, stage)];
            return linearPredictionEngine.Predict(row).WinProbability;
        }

        private class ModelOutput
        {
            [ColumnName("Score")]
            public float WinProbability { get; set; }
        }
    }
}
