using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.ML.Data;

namespace EnsembleTreeModelBuilder
{
    /// <summary>
    /// I had to turn everything into float, since Auto.ML.Net kept crashing on all other types, including Ints, with some non-interpretable error logs
    /// </summary>
    public class GameStateFeatureSetCsvRow
    {
        /// <summary>
        /// Patron ids are no longer part of the feature set used by the models, but still want to record it for the option to create different
        /// models for different patron combinations
        /// </summary>
        /*[LoadColumn(0)]*/ public float Patron_1 { get; set; }
        /*[LoadColumn(1)]*/ public float Patron_2 { get; set; }
        /*[LoadColumn(2)]*/ public float Patron_3 { get; set; }
        /*[LoadColumn(3)]*/ public float Patron_4 { get; set; }
        [LoadColumn(4)] public float CurrentPlayerPrestige { get; set; }
        [LoadColumn(5)] public float CurrentPlayerDeck_PrestigeStrength { get; set; }
        [LoadColumn(6)] public float CurrentPlayerDeck_PowerStrength { get; set; }
        [LoadColumn(7)] public float CurrentPlayerDeck_GoldStrength { get; set; }
        [LoadColumn(8)] public float CurrentPlayerDeck_MiscStrength { get; set; }
        [LoadColumn(9)] public float CurrentPlayerDeckComboProportion { get; set; }
        [LoadColumn(10)] public float CurrentPlayerAgent_PowerStrength { get; set; }
        [LoadColumn(11)] public float CurrentPlayerAgent_GoldStrength { get; set; }
        [LoadColumn(12)] public float CurrentPlayerAgent_MiscStrength { get; set; }
        [LoadColumn(13)] public float CurrentPlayerPatronFavour { get; set; }
        [LoadColumn(14)] public float OpponentPrestige { get; set; }
        [LoadColumn(15)] public float OpponentDeck_PrestigeStrength { get; set; }
        [LoadColumn(16)] public float OpponentDeck_PowerStrength { get; set; }
        [LoadColumn(17)] public float OpponentDeck_GoldStrength { get; set; }
        [LoadColumn(18)] public float OpponentDeck_MiscStrength { get; set; }
        [LoadColumn(19)] public float OpponentAgent_PowerStrength { get; set; }
        [LoadColumn(20)] public float OpponentAgent_GoldStrength { get; set; }
        [LoadColumn(21)] public float OpponentAgent_MiscStrength { get; set; }
        [LoadColumn(22)] public float OpponentPatronFavour { get; set; }
        [LoadColumn(23)] public float WinProbability { get; set; }
    }

}
