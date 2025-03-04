using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static SimpleBots.src.MaltheMCTS.Utility.HeuristicScoring.HeuristicScoring;

namespace SimpleBots.src.MaltheMCTS.Utility.HeuristicScoring
{
    public struct GameStateFeatureSet
    {
        public int CurrentPlayerPrestige;
        public int CurrentPlayerPower;
        public CardStrengths CurrentPlayerDeckStrengths;
        public double CurrentPlayerDeckComboProportion;
        public CardStrengths CurrentPlayerAgentStrengths;
        public int CurrentPlayerPatronFavour;
        public int OpponentPrestige;
        public CardStrengths OpponentDeckStrengths;
        public CardStrengths OpponentAgentStrengths;
        public int OpponentPatronFavour;
    }
}
