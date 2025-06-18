//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using Microsoft.ML.Data;

//namespace EnsembleTreeModelBuilder
//{
//    /// <summary>
//    /// I had to turn everything into float, since Auto.ML.Net kept crashing on all other types, including Ints, with some non-interpretable error logs
//    /// </summary>
//    public class GameStateLinearFeatureSet
//    {
//        public int CurrentPlayerPrestige { get; set; }
//        public double CurrentPlayerDeck_PrestigeStrength { get; set; }
//        public double CurrentPlayerDeck_PowerStrength { get; set; }
//        public double CurrentPlayerDeck_GoldStrength { get; set; }
//        public double CurrentPlayerDeck_MiscStrength { get; set; }
//        public double CurrentPlayerDeckComboProportion { get; set; }
//        public double CurrentPlayerAgent_PowerStrength { get; set; }
//        public double CurrentPlayerAgent_GoldStrength { get; set; }
//        public double CurrentPlayerAgent_MiscStrength { get; set; }
//        public bool CurrentPlayerPatronFavour_0 { get; set; }
//        public bool CurrentPlayerPatronFavour_1 { get; set; }
//        public bool CurrentPlayerPatronFavour_2 { get; set; }
//        public bool CurrentPlayerPatronFavour_3 { get; set; }
//        public int OpponentPrestige { get; set; }
//        public double OpponentDeck_PrestigeStrength { get; set; }
//        public double OpponentDeck_PowerStrength { get; set; }
//        public double OpponentDeck_GoldStrength { get; set; }
//        public double OpponentDeck_MiscStrength { get; set; }
//        public double OpponentAgent_PowerStrength { get; set; }
//        public double OpponentAgent_GoldStrength { get; set; }
//        public double OpponentAgent_MiscStrength { get; set; }
//        public bool OpponentPatronFavour_0 { get; set; }
//        public bool OpponentPatronFavour_1 { get; set; }
//        public bool OpponentPatronFavour_2 { get; set; }
//        public bool OpponentPatronFavour_3 { get; set; }

//        public double? WinProbability { get; set; }
//    }

//}
