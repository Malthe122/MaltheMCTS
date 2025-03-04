using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static SimpleBots.src.MaltheMCTS.Utility.HeuristicScoring.HeuristicScoring;

namespace SimpleBots.src.MaltheMCTS.Utility.HeuristicScoring
{
    public static class SimpleManualEvaluation
    {
        // For now these numbers are just made by gut feeling
        const double MISCELLANEOUS_MULTIPLIER = 0.25;
        const double DECK_MULTIPLIER = 3;
        const double BASE_PATRON_VALUE = 1.5;

        public static double Evaluate(GameStateFeatureSet featureSet)
        {
            int maxPrestige = Math.Max(featureSet.CurrentPlayerPrestige, featureSet.OpponentPrestige);
            double lateGameMultiplier = double.Max(maxPrestige / 40.0, 0.1); // 40 is the number where prestige starts being a win condition
            double earlyGameMultiplier = 1 - lateGameMultiplier;
            earlyGameMultiplier = double.Max(earlyGameMultiplier, 0.1);

            double currentPlayerPrestigeValue = featureSet.CurrentPlayerPrestige * lateGameMultiplier;
            double opponentPrestigeValue = featureSet.OpponentPrestige * lateGameMultiplier;

            double currentPlayerDeckValue = GetDeckValue(featureSet.CurrentPlayerDeckStrengths, lateGameMultiplier, earlyGameMultiplier);
            double opponentDeckValue = GetDeckValue(featureSet.OpponentDeckStrengths, lateGameMultiplier, earlyGameMultiplier);

            double currentPlayerAgentValue = GetAgentValue(featureSet.CurrentPlayerAgentStrengths, lateGameMultiplier, earlyGameMultiplier);
            double opponentAgentValue = GetAgentValue(featureSet.OpponentAgentStrengths, lateGameMultiplier, earlyGameMultiplier);

            double currentPlayerPatronValue = Math.Pow(BASE_PATRON_VALUE, featureSet.CurrentPlayerPatronFavour);
            double opponentPatronValue = Math.Pow(BASE_PATRON_VALUE, featureSet.OpponentPatronFavour);

            var currentPlayerValue = currentPlayerPrestigeValue + currentPlayerDeckValue + currentPlayerAgentValue + currentPlayerPatronValue;
            var opponentValue = opponentPrestigeValue + opponentDeckValue + opponentAgentValue + opponentPatronValue;

            return currentPlayerValue - opponentValue;
        }

        private static double GetAgentValue(CardStrengths agentStrengths, double lateGameMultiplier, double earlyGameMultiplier)
        {
            var prestigeValue = (agentStrengths.PrestigeStrength + agentStrengths.PowerStrength) * lateGameMultiplier;
            var goldValue = agentStrengths.GoldStrength * earlyGameMultiplier;
            var miscValue = agentStrengths.MiscellaneousStrength * MISCELLANEOUS_MULTIPLIER;

            return prestigeValue + goldValue + miscValue;
        }

        private static double GetDeckValue(CardStrengths deckStrengths, double lateGameMultiplier, double earlyGameMultiplier)
        {
            var prestigeValue = (deckStrengths.PrestigeStrength + deckStrengths.PowerStrength) * lateGameMultiplier;
            var goldValue = deckStrengths.GoldStrength * earlyGameMultiplier;
            var miscValue = deckStrengths.MiscellaneousStrength * MISCELLANEOUS_MULTIPLIER;


            return (prestigeValue + goldValue + miscValue) * DECK_MULTIPLIER * earlyGameMultiplier; // decks are more important early in the game, while near the end focus is on grinding prestige immediatly
        }
    }
}
