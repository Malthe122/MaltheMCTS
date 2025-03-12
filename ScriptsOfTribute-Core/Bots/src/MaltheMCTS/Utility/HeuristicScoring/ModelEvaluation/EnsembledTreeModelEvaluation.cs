using Microsoft.ML;
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
        private static readonly ITransformer trainedModel;
        private static readonly PredictionEngine<GameStateFeatureSetCsvRow, ModelOutput> predictionEngine;

        static EnsembledTreeModelEvaluation()
        {
            string modelPath = "MaltheMCTS_Model";
            trainedModel = mlContext.Model.Load(modelPath, out var _);
            predictionEngine = mlContext.Model.CreatePredictionEngine<GameStateFeatureSetCsvRow, ModelOutput>(trainedModel);

        }

        public static float GetWinProbability(GameStateFeatureSetCsvRow row)
        {
            return predictionEngine.Predict(row).WinProbability;
        }

        private class ModelOutput
        {
            [ColumnName("Score")]
            public float WinProbability { get; set; }
        }
    }
}
