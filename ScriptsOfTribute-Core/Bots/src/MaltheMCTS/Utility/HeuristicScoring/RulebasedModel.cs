using MaltheMCTS;
using ScriptsOfTribute;
using ScriptsOfTribute.Board.Cards;
using ScriptsOfTribute.Serializers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static SimpleBots.src.MaltheMCTS.Utility.HeuristicScoring.HeuristicScoring;

namespace SimpleBots.src.MaltheMCTS.Utility.HeuristicScoring
{
    public static class RulebasedModel
    {
        public static double Score(SeededGameState gameState, RuleBasedModelSettings settings)
        {
            int maxPrestige = Math.Max(gameState.CurrentPlayer.Prestige, gameState.EnemyPlayer.Prestige);
            double lateGameMultiplier = double.Max(maxPrestige / 40.0, 0.1); // 40 is the number where prestige starts being a win condition
            double earlyGameMultiplier = 1 - lateGameMultiplier;
            earlyGameMultiplier = double.Max(earlyGameMultiplier, 0.1);

            var earlyScore = GetScore(gameState, settings.EarlyGameWeights, settings.StaticWeights) * earlyGameMultiplier;
            var lateScore = GetScore(gameState, settings.LateGameWeights, settings.StaticWeights) * lateGameMultiplier;

            return earlyScore + lateScore;

            //double currentPlayerPrestigeValue = featureSet.CurrentPlayerPrestige * lateGameMultiplier;
            //double opponentPrestigeValue = featureSet.OpponentPrestige * lateGameMultiplier;

            //double currentPlayerDeckValue = GetDeckValue(featureSet.CurrentPlayerDeckStrengths, lateGameMultiplier, earlyGameMultiplier);
            //double opponentDeckValue = GetDeckValue(featureSet.OpponentDeckStrengths, lateGameMultiplier, earlyGameMultiplier);

            //double currentPlayerAgentValue = GetAgentValue(featureSet.CurrentPlayerAgentStrenths, lateGameMultiplier, earlyGameMultiplier);
            //double opponentAgentValue = GetAgentValue(featureSet.CurrentPlayerAgentStrenths, lateGameMultiplier, earlyGameMultiplier);

            //double currentPlayerPatronValue = Math.Pow(BASE_PATRON_VALUE, featureSet.CurrentPlayerPatronFavour);
            //double opponentPatronValue = Math.Pow(BASE_PATRON_VALUE, featureSet.OpponentPatronFavour);

            //var currentPlayerValue = currentPlayerPrestigeValue + currentPlayerDeckValue + currentPlayerAgentValue + currentPlayerPatronValue;
            //var opponentValue = opponentPrestigeValue + opponentDeckValue + opponentAgentValue + opponentPatronValue;

            //return currentPlayerValue - opponentValue;
        }

        private static double GetScore(SeededGameState gameState, StageWeights stageWeights, StaticWeights staticWeights)
        {
            var currentPlayerDeck = gameState.CurrentPlayer.GetCompleteDeck();
            var deckSize = currentPlayerDeck.Count;
            var patronRatios = FeatureSetUtility.GetPatronRatios(currentPlayerDeck, gameState.Patrons);


            //public double GoldWeight = 1;
            //public double PowerWeight = 1;
            //public double PrestigeWeight = 1;
            //public double PatronCallsWeight = 1;
            //public double DeckComboProportionWeight = 1;
            //public double HandWeight = 1;
            //public double DeckWeight = 1;
            //public double BoardAgentsWeight = 1;
            //public double AvailableBoardAgentsWeight = 1;
            //public double AgentHPWeight = 1;
            //public double AgentCardWeight = 1;
            //public double DrawpileWeight = 1;

            //public double OnePatronFavourWeight = 1;
            //public double TwoPatronFavourWeight = 1;
            //public double ThreePatronFavorWeight = 1;
            //public double OpponentDiscardsWeight = 1;
            //public double WinWeight = 1;

            var resourceValue =
                (
                gameState.CurrentPlayer.Coins * stageWeights.GoldWeight
                + gameState.CurrentPlayer.Power * stageWeights.PowerWeight
                + gameState.CurrentPlayer.Prestige * stageWeights.PrestigeWeight
                + gameState.CurrentPlayer.PatronCalls * stageWeights.PatronCallsWeight
                )
                -
                (
                gameState.EnemyPlayer.Prestige * stageWeights.PrestigeWeight
                );


            
            var currentPlayerDeckComboProportion = ((double)currentPlayerDeck.Where(c => c.Deck != PatronId.TREASURY).Count()) / currentPlayerDeck.Count;
            var comboDeckProportianValue = currentPlayerDeckComboProportion * stageWeights.DeckComboProportionWeight;

            var boardAgentsValue = GetAgentsValue(gameState.CurrentPlayer.Agents, patronRatios, deckSize, stageWeights, staticWeights) * stageWeights.AvailableBoardAgentWeight;

            var handValue = GetDeckValue(gameState.CurrentPlayer.Hand, patronRatios, stageWeights, staticWeights) * stageWeights.HandWeight;
            // TODO add opponent deck value, as we can add curses to their deck (and maybe others)
            var deckValue = GetDeckValue(currentPlayerDeck, patronRatios, stageWeights, staticWeights) * stageWeights.DeckWeight;
            var drawPileWeight = GetDeckValue(gameState.CurrentPlayer.DrawPile, patronRatios, stageWeights, staticWeights) * stageWeights.DrawpileWeight;
            var cooldownValue = GetDeckValue(gameState.CurrentPlayer.CooldownPile, patronRatios, stageWeights, staticWeights) * stageWeights.CoolDownWeight;

            // TOOD add opponent patron favour
            double patronFavourValue = 0;
            switch (gameState.GetPatronFavourCount(gameState.CurrentPlayer.PlayerID))
            {
                case 0:
                    break;
                case 1:
                    patronFavourValue = stageWeights.OnePatronFavourWeight;
                    break;
                case 2:
                    patronFavourValue = stageWeights.TwoPatronFavourWeight;
                    break;
                case 3:
                    patronFavourValue = stageWeights.ThreePatronFavourWeight;
                    break;
            }

            throw new NotImplementedException(); // TODO make
        }

        private static double GetAgentsValue(List<SerializedAgent> agents, Dictionary<PatronId, double> patronRatios, int deckSize, StageWeights stageWeights, StaticWeights staticWeights)
        {
            double value = 0;

            foreach (SerializedAgent agent in agents)
            {
                value += GetAgentValue(agent, patronRatios, deckSize, stageWeights, staticWeights);
            }

            return value;
        }

        public static double GetAgentValue(SerializedAgent agent, Dictionary<PatronId, double> patronRatios, int deckSize, StageWeights stageWeights, StaticWeights staticWeights)
        {
            var agentStrengths = ScoreStrengthsInDeck(agent.RepresentingCard, patronRatios[agent.RepresentingCard.Deck], deckSize, staticWeights.ChoiceWeight);

            var baseValue = GetStrengthsValue(agentStrengths, stageWeights.CardWeights);
            var tauntValue = agent.RepresentingCard.Taunt ? agent.CurrentHp * stageWeights.AgentTauntWeight : 0;
            var hpValue = baseValue * agent.CurrentHp * stageWeights.AgentHPWeight;

            return baseValue + tauntValue + hpValue;
        }

        private static double GetStrengthsValue(CardStrengths strengths, CardStrengths weights)
        {
            return
                strengths.AquireTavernStrenth * weights.AquireTavernStrenth
                + strengths.DestroyCardStrength * weights.DestroyCardStrength
                + strengths.DonateStrength * weights.DonateStrength
                + strengths.DrawStrength * weights.DrawStrength
                + strengths.GoldStrength * weights.GoldStrength
                + strengths.HealStrength * weights.HealStrength
                + strengths.IncreasedPatronCallStrength * weights.IncreasedPatronCallStrength
                + strengths.KnockoutAllStrength * weights.KnockoutAllStrength
                + strengths.KnockoutStrength * weights.KnockoutStrength
                + strengths.OpponentDiscardStrength * weights.OpponentDiscardStrength
                + strengths.PowerStrength * weights.PowerStrength
                + strengths.PrestigeStrength * weights.PrestigeStrength
                + strengths.ReplaceTavernStrength * weights.ReplaceTavernStrength
                + strengths.ReturnAgentTopStrenth * weights.ReturnAgentTopStrenth
                + strengths.ReturnTopStrength * weights.ReturnTopStrength
                + strengths.SummersetSackingStrength * weights.SummersetSackingStrength
                + strengths.TossStrength * weights.TossStrength; //TODO consider doing reflected foreach on properties instead
        }

        private static double GetDeckValue(List<UniqueCard> cards, Dictionary<PatronId, double> patronRatios, StageWeights stageWeights, StaticWeights staticWeights)
        {
            //var prestigeValue = (deckStrengths.PrestigeStrength + deckStrengths.PowerStrength) * lateGameMultiplier;
            //var goldValue = deckStrengths.GoldStrength * earlyGameMultiplier;
            //var miscValue = deckStrengths.MiscellaneousStrength * MISCELLANEOUS_MULTIPLIER;


            //return (prestigeValue + goldValue + miscValue) * DECK_MULTIPLIER * earlyGameMultiplier; // decks are more important early in the game, while near the end focus is on grinding prestige immediatly
            throw new NotImplementedException(); // TODO make
        }

        private static double GetCardValue(CardStrengths cardStrengths, StageWeights stageWeights, StaticWeights staticWeights)
        {
            throw new NotImplementedException(); // TODO make
        }

        //private static double GetRawValue(CardStrengths strengths, RuleBasedModelSettings ruleBasedModelSettings)
        //{
        //    var value = 0;

        //    0 + strengths.AquireTavernStrenth * ruleBasedModelSettings.
        //}

    }

    public struct RuleBasedModelSettings
    {
        public RuleBasedModelSettings() { }
        // TODO move some weights about card evaluation here, and use it when calculating feature set
        public StageWeights EarlyGameWeights = new StageWeights();
        public StageWeights LateGameWeights = new StageWeights();
        public StaticWeights StaticWeights = new StaticWeights();
    }

    public struct StaticWeights
    {
        public StaticWeights() { 
        }
        public double ChoiceWeight = 1.5;
    }

    public struct StageWeights
    {
        public StageWeights()
        {
        }
        public double GoldWeight = 1;
        public double PowerWeight = 1;
        public double PrestigeWeight = 1;
        public double PatronCallsWeight = 1;
        public double DeckComboProportionWeight = 1;
        public double HandWeight = 1;
        public double DeckWeight = 1;
        public double CoolDownWeight = 1;
        public double BoardAgentsWeight = 1;
        public double AvailableBoardAgentWeight = 1;
        public double AgentTauntWeight = 1;
        public double AgentHPWeight = 1;
        public double AgentCardWeight = 1;
        public double DrawpileWeight = 1; // TODO make for played, draw, cooldown etc.
        //public double KnownTopWeight = 1; Decided not to keep this for now, as it makes any state where any known card is put on top better even if its a bad card
        public double OnePatronFavourWeight = 1;
        public double TwoPatronFavourWeight = 1;
        public double ThreePatronFavourWeight = 1;
        public double OpponentDiscardsWeight = 1;
        public double WinWeight = 1;
        public CardStrengths CardWeights = new CardStrengths()
        {
            AquireTavernStrenth = 1,
            DestroyCardStrength = 1,
            DonateStrength = 1,
            DrawStrength = 1,
            GoldStrength = 1,
            HealStrength = 1,
            IncreasedPatronCallStrength = 1,
            KnockoutAllStrength = 1,
            KnockoutStrength = 1,
            OpponentDiscardStrength = 1,
            PowerStrength = 1,
            PrestigeStrength = 1,
            ReplaceTavernStrength = 1,
            ReturnAgentTopStrenth = 1,
            ReturnTopStrength = 1,
            SummersetSackingStrength = 1,
            TossStrength = 1,
        };
    }
}
