using MaltheMCTS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static MaltheMCTS.HeuristicScoring;

namespace SimpleBots.src.MaltheMCTS.Utility
{
    public static class SimpleManualEvaluation
    {
        // For now these numbers are just made by gut feeling
        const double MISCELLANEOUS_MULTIPLIER = 0.25;
        const double DECK_MULTIPLIER = 3;
        const double BASE_PATRON_VALUE = 1.5;

        public static double Evaluate(int currentPlayerPrestige,
            int currentPlayerPower,
            int currentPlayerCoins,
            CardStrengths currentPlayerDeckStrengths,
            CardStrengths currentPlayerAgentStrengths,
            int currentPlayerPatronFavour,
            int opponentPrestige,
            CardStrengths opponentDeckStrengths,
            CardStrengths opponentAgentStrengths,
            int opponentPatronFavour)
        {
            int maxPrestige = Math.Max(currentPlayerPrestige, opponentPrestige);
            double lateGameMultiplier = double.Max(maxPrestige / 40, 0.1); // 40 is the number where prestige starts being a win condition
            double earlyGameMultiplier = 1 - lateGameMultiplier;
            earlyGameMultiplier = double.Max(earlyGameMultiplier, 0.1);

            double currentPlayerResourceValue = ((currentPlayerPrestige + currentPlayerPower) * lateGameMultiplier) + currentPlayerCoins * earlyGameMultiplier;
            double opponentResourceValue = (opponentPrestige * lateGameMultiplier);
            
            double currentPlayerDeckValue = GetDeckValue(currentPlayerDeckStrengths, lateGameMultiplier, earlyGameMultiplier);
            double opponentDeckValue = GetDeckValue(opponentDeckStrengths, lateGameMultiplier, earlyGameMultiplier);

            double currentPlayerAgentValue = GetAgentValue(currentPlayerAgentStrengths, lateGameMultiplier, earlyGameMultiplier);
            double opponentAgentValue = GetAgentValue(opponentAgentStrengths, lateGameMultiplier, earlyGameMultiplier);

            double currentPlayerPatronValue = Math.Pow(1.5, currentPlayerPatronFavour);
            double opponentPatronValue = Math.Pow(1.5, opponentPatronFavour);

            var currentPlayerValue = currentPlayerResourceValue + currentPlayerDeckValue + currentPlayerAgentValue + currentPlayerPatronValue;
            var opponentValue = opponentResourceValue + opponentDeckValue + opponentAgentValue + opponentPatronValue;

            return currentPlayerValue - opponentValue;
        }

        private static double GetAgentValue(CardStrengths agentStrengths, double lateGameMultiplier, double earlyGameMultiplier)
        {
            var prestigeValue = (agentStrengths.PrestigeStrength + agentStrengths.PowerStrength) * lateGameMultiplier;
            var goldValue = agentStrengths.GoldStrength * earlyGameMultiplier;
            var miscValue = agentStrengths.MiscellaneousStrength * MISCELLANEOUS_MULTIPLIER;

            return (prestigeValue + goldValue + miscValue);
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
