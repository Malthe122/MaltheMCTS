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
            var deckPatronCardCounts = FeatureSetUtility.GetPatronCardCounts(currentPlayerDeck, gameState.Patrons);
            var opponentDeck = gameState.EnemyPlayer.GetCompleteDeck();
            var opponentDeckSize = opponentDeck.Count;
            var opponentDeckPatronCardCounts = FeatureSetUtility.GetPatronCardCounts(opponentDeck, gameState.Patrons);


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
            
            var boardAgentsValue = GetAgentsValue(gameState.CurrentPlayer.Agents, deckPatronCardCounts, deckSize, stageWeights, staticWeights) * stageWeights.BoardAgentsWeight;
            var opponentAgentsValue = GetAgentsValue(gameState.EnemyPlayer.Agents, opponentDeckPatronCardCounts, opponentDeckSize, stageWeights, staticWeights) * stageWeights.BoardAgentsWeight;

            var handValue = GetDeckValue(gameState.CurrentPlayer.Hand, deckPatronCardCounts, deckSize, stageWeights, staticWeights) * stageWeights.HandWeight;

            var deckValue = GetDeckValue(currentPlayerDeck, deckPatronCardCounts, deckSize, stageWeights, staticWeights) * stageWeights.DeckWeight;
            // As it is possible to affect opponents deck (through curses and maybe others)
            var opponentDeckValue = GetDeckValue(gameState.EnemyPlayer.GetCompleteDeck(), deckPatronCardCounts, opponentDeckSize, stageWeights, staticWeights) * stageWeights.DeckWeight;

            var drawPileValue = GetDeckValue(gameState.CurrentPlayer.DrawPile, deckPatronCardCounts, deckSize, stageWeights, staticWeights) * stageWeights.DrawpileWeight;
            var cooldownValue = GetDeckValue(gameState.CurrentPlayer.CooldownPile, deckPatronCardCounts, deckSize, stageWeights, staticWeights) * stageWeights.CoolDownWeight;

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
                default:
                    throw new Exception("Unexpexted Patron Favour count: " + gameState.GetPatronFavourCount(gameState.CurrentPlayer.PlayerID));
            }

            double opponentPatronFavourValue = 0;
            switch (gameState.GetPatronFavourCount(gameState.EnemyPlayer.PlayerID))
            {
                case 0:
                    break;
                case 1:
                    opponentPatronFavourValue = stageWeights.OnePatronFavourWeight;
                    break;
                case 2:
                    opponentPatronFavourValue = stageWeights.TwoPatronFavourWeight;
                    break;
                case 3:
                    opponentPatronFavourValue = stageWeights.ThreePatronFavourWeight;
                    break;
                default:
                    throw new Exception("Unexpexted Patron Favour count: " + gameState.GetPatronFavourCount(gameState.EnemyPlayer.PlayerID));
            }

            // TODO make for remaining features

            return
                (resourceValue + comboDeckProportianValue + boardAgentsValue + handValue + deckValue + drawPileValue + cooldownValue + patronFavourValue)
                -
                (opponentAgentsValue + opponentDeckValue + opponentPatronFavourValue);
        }

        private static double GetAgentsValue(List<SerializedAgent> agents, Dictionary<PatronId, int> patronCardCounts, int deckSize, StageWeights stageWeights, StaticWeights staticWeights)
        {
            double value = 0;

            foreach (SerializedAgent agent in agents)
            {
                value += GetAgentValue(agent, patronCardCounts, deckSize, stageWeights, staticWeights);
            }

            return value;
        }

        public static double GetAgentValue(SerializedAgent agent, Dictionary<PatronId, int> patronCardCounts, int deckSize, StageWeights stageWeights, StaticWeights staticWeights)
        {
            var agentStrengths = ScoreStrengthsInDeck(agent.RepresentingCard, patronCardCounts[agent.RepresentingCard.Deck], deckSize, staticWeights.ChoiceWeight);

            var baseValue = GetStrengthsValue(agentStrengths, stageWeights.CardWeights);
            var tauntValue = agent.RepresentingCard.Taunt ? agent.CurrentHp * stageWeights.ActiveAgent_TauntWeight : 0;
            var hpValue = baseValue * agent.CurrentHp * stageWeights.ActiveAgent_HPWeight;
            var activeValue = agent.Activated ? 0 : baseValue * stageWeights.AvailableBoardAgentWeight;

            return baseValue + tauntValue + hpValue + activeValue;
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

        private static double GetDeckValue(List<UniqueCard> cards, Dictionary<PatronId, int> patronCardCounts, int deckSize, StageWeights stageWeights, StaticWeights staticWeights)
        {
            double totalCardValue = 0;
            foreach (var card in cards)
            {
                totalCardValue += GetCardValue(card, patronCardCounts[card.Deck], deckSize, stageWeights, staticWeights);
            }

            return totalCardValue / deckSize;
        }

        private static double GetCardValue(Card card, int matchingPatronCardsInDeck, int deckSize, StageWeights stageWeights, StaticWeights staticWeights)
        {
            var strengths = HeuristicScoring.ScoreStrengthsInDeck(card, matchingPatronCardsInDeck, deckSize, staticWeights.ChoiceWeight);
            var strengthsValue = GetStrengthsValue(strengths, stageWeights.CardWeights);
            if (card.Type == CardType.AGENT)
            {
                var agentBonusValue = strengthsValue * stageWeights.AgentCardWeight;
                var hpBonusValue = strengthsValue * card.HP * stageWeights.AgentHPWeight;
                var tauntBonusValue = card.HP * stageWeights.AgentTauntWeight;
                strengthsValue += agentBonusValue + hpBonusValue + tauntBonusValue;
            }
            return strengthsValue;
        }
    }

    public struct RuleBasedModelSettings
    {
        public RuleBasedModelSettings() { }
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
        public double ActiveAgent_TauntWeight = 1;
        public double ActiveAgent_HPWeight = 1;
        public double AgentCardWeight = 1;
        public double AgentTauntWeight = 1;
        public double AgentHPWeight = 1;
        public double DrawpileWeight = 1;
        //public double KnownTopWeight = 1; Decided not to keep this for now, as it makes any state where any known card is put on top better even if its a bad card.
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
