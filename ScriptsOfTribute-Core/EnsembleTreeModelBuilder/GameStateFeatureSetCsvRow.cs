using ScriptsOfTribute.Board.Cards;
using ScriptsOfTribute;
using ScriptsOfTribute.Serializers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static SimpleBots.src.MaltheMCTS.Utility.HeuristicScoring.HeuristicScoring;
using Microsoft.ML.Data;

namespace EnsembleTreeModelBuilder
{
    public class GameStateFeatureSetCsvRow
    {
        [LoadColumn(0)] public int Patron_1 { get; set; }
        [LoadColumn(1)] public int Patron_2 { get; set; }
        [LoadColumn(2)] public int Patron_3 { get; set; }
        [LoadColumn(3)] public int Patron_4 { get; set; }
        [LoadColumn(4)] public int CurrentPlayerPrestige { get; set; }
        [LoadColumn(5)] public double CurrentPlayerDeck_PrestigeStrength { get; set; }
        [LoadColumn(6)] public double CurrentPlayerDeck_PowerStrength { get; set; }
        [LoadColumn(7)] public double CurrentPlayerDeck_GoldStrength { get; set; }
        [LoadColumn(8)] public double CurrentPlayerDeck_MiscStrength { get; set; }
        [LoadColumn(9)] public double CurrentPlayerDeckComboProportion { get; set; }
        [LoadColumn(10)] public double CurrentPlayerAgent_PowerStrength { get; set; }
        [LoadColumn(11)] public double CurrentPlayerAgent_GoldStrength { get; set; }
        [LoadColumn(12)] public double CurrentPlayerAgent_MiscStrength { get; set; }
        [LoadColumn(13)] public int CurrentPlayerPatronFavour { get; set; }
        [LoadColumn(14)] public int OpponentPrestige { get; set; }
        [LoadColumn(15)] public double OpponentDeck_PrestigeStrength { get; set; }
        [LoadColumn(16)] public double OpponentDeck_PowerStrength { get; set; }
        [LoadColumn(17)] public double OpponentDeck_GoldStrength { get; set; }
        [LoadColumn(18)] public double OpponentDeck_MiscStrength { get; set; }
        [LoadColumn(19)] public double OpponentAgent_PowerStrength { get; set; }
        [LoadColumn(20)] public double OpponentAgent_GoldStrength { get; set; }
        [LoadColumn(21)] public double OpponentAgent_MiscStrength { get; set; }
        [LoadColumn(22)] public int OpponentPatronFavour { get; set; }
        [LoadColumn(23), ColumnName("Label")] public double WinProbability { get; set; }
    }

}
