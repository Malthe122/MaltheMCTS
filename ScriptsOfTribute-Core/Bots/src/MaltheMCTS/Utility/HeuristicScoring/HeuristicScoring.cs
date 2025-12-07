using EnsembleTreeModelBuilder;
using MathNet.Numerics.Distributions;
using Microsoft.ML;
using Microsoft.ML.AutoML;
using ScriptsOfTribute;
using ScriptsOfTribute.Board.Cards;
using ScriptsOfTribute.Serializers;
using SimpleBots.src.MaltheMCTS.Utility.HeuristicScoring.ModelEvaluation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static SimpleBots.src.MaltheMCTS.Utility.HeuristicScoring.ModelEvaluation.EnsembledTreeModelEvaluation;

namespace SimpleBots.src.MaltheMCTS.Utility.HeuristicScoring
{
    public static class HeuristicScoring
    {
        private const double BASE_AGENT_STRENGTH_MULTIPLIER = 1;
        private const double AGENT_HP_VALUE_MULTIPLIER = 0.1;
        private const double CHOICE_WEIGHT = 0.75;

        /// <summary>
        /// To lower the amount of variables (hand strengths, patron moves available, coins, power, whether agents have been activated) the model needs to process,
        /// this model scores states just before ending turn
        /// </summary>
        public static double Score(SeededGameState gameState, PredictionEngine<GameStateFeatureSetCsvRow, ModelOutput>? predictionEngine)
        {
            // The manual model (null) does not return either 0 and 1 or -1 and 1, so this logic does not apply for it
            if (predictionEngine != null)
            {
                var winner = CheckWinner(gameState);

                if (winner == gameState.CurrentPlayer.PlayerID)
                {
                    return 1;
                }
                else if (winner == gameState.EnemyPlayer.PlayerID)
                {
                    return 0; // Consider if i should measure score as win probability (0-1) or zero sum score
                }
            }

            var featureSet = FeatureSetUtility.BuildFeatureSet(gameState);

            return ModelEvaluation(featureSet, predictionEngine);
        }

        private static PlayerEnum CheckWinner(SeededGameState gameState)
        {
            int currentPlayerPrestige = gameState.CurrentPlayer.Prestige;
            int opponentPrestige = gameState.EnemyPlayer.Prestige;

            if (currentPlayerPrestige >= 80)
            {
                return gameState.CurrentPlayer.PlayerID;
            }

            int opponentTaunt = gameState.EnemyPlayer.Agents.Where(a => a.RepresentingCard.Taunt).Sum(a => a.CurrentHp);
            int power = gameState.CurrentPlayer.Power;

            if (power - opponentTaunt + currentPlayerPrestige >= 80)
            {
                return gameState.CurrentPlayer.PlayerID;
            }

            int patronCount = 0;

            foreach (var patron in gameState.PatronStates.All)
            {
                if (patron.Value == gameState.CurrentPlayer.PlayerID)
                {
                    patronCount++;
                }
            }

            if (patronCount >= 4)
            {
                return gameState.CurrentPlayer.PlayerID;
            }

            return PlayerEnum.NO_PLAYER_SELECTED;
        }

        private static double ModelEvaluation(GameStateFeatureSet featureSet, PredictionEngine<GameStateFeatureSetCsvRow, ModelOutput> predictionEngine)
        {
            var csvFeatureSet = featureSet.ToCsvRow();
            return predictionEngine.Predict(csvFeatureSet).WinProbability;
        }

        private static Dictionary<PatronId, double> GetPatronRatios(List<Card> deck, List<PatronId> patrons)
        {
            var patronToAmount = new Dictionary<PatronId, int>();
            var patronToDeckRatio = new Dictionary<PatronId, double>();

            foreach (var patron in patrons)
            {
                patronToAmount.Add(patron, 0);
            }

            foreach (var currCard in deck)
            {
                patronToAmount[currCard.Deck]++;
            }

            foreach (var currPair in patronToAmount)
            {
                patronToDeckRatio.Add(currPair.Key, currPair.Value / deck.Count);
            }

            return patronToDeckRatio;
        }

        private static CardStrengths ScoreStrengthsInDeck(List<SerializedAgent> agents, Dictionary<PatronId, int> patronCardsInDeck, int deckSize, double availableMultiplier, double hpMultiplier, double tauntMultiplier, double choiceWeight)
        {
            var result = new CardStrengths();

            foreach (var agent in agents)
            {
                var agentCardStrength = ScoreStrengthsInDeck(agent.RepresentingCard, patronCardsInDeck[agent.RepresentingCard.Deck], deckSize, choiceWeight);
                CardStrengths agentStrength = new CardStrengths();
                if (!agent.Activated)
                {
                    agentStrength += agentCardStrength * availableMultiplier;
                }

                agentStrength += agentCardStrength * agent.CurrentHp * hpMultiplier;

                if(agent.RepresentingCard.Taunt)
                {
                    agentStrength += agentCardStrength * agent.CurrentHp * tauntMultiplier;
                }

                result += agentStrength;
            }

            return result;
        }

        private static CardStrengths ScoreStrengthsInDeck(List<Card> deck, Dictionary<PatronId, int> patronCardsInDeck, double choiceWeight)
        {
            var summedStrengths = new CardStrengths();

            foreach (var currCard in deck)
            {
                summedStrengths += ScoreStrengthsInDeck(currCard, patronCardsInDeck[currCard.Deck], deck.Count, choiceWeight);
            }

            return summedStrengths / deck.Count;
        }

        public static CardStrengths ScoreStrengthsInDeck(Card card, int patronCardsInDeck, int deckSize, double choiceWeight)
        {
            var result = new CardStrengths();
            foreach (var effect in card.Effects)
            {
                if (effect == null)
                {
                    continue;
                }
                else
                {
                    var uniqueEffect = effect.MakeUniqueCopy(card.CreateUniqueCopy()); // FUTURE refactor to simply use left and right on effect if it gets readable, instead of creating a unique instance of the effect
                    result += ScoreComplexEffectStrengthsInDeck(uniqueEffect, patronCardsInDeck, deckSize, choiceWeight);
                }
            }

            return result;
        }

        /// <summary>
        /// FUTURE refactor to not use unique effect, if effect definitions gets right and left readable
        /// </summary>
        private static CardStrengths ScoreComplexEffectStrengthsInDeck(UniqueComplexEffect effect, int patronCardsInDeck, int deckSize, double choiceWeight)
        {
            switch (effect)
            {
                case Effect:
                    return ScoreEffectStrengthsInDeck((effect as UniqueEffect)!, patronCardsInDeck, deckSize);
                case EffectComposite:
                    var effectComposite = (effect as UniqueEffectComposite)!;
                    var effect1Strengths = ScoreEffectStrengthsInDeck(effectComposite.GetLeft(), patronCardsInDeck, deckSize);
                    var effect2Strengths = ScoreEffectStrengthsInDeck(effectComposite.GetRight(), patronCardsInDeck, deckSize);
                    return effect1Strengths + effect2Strengths;
                case EffectOr:
                    var effectOr = (effect as UniqueEffectOr)!;
                    var effectaStrengths = ScoreEffectStrengthsInDeck(effectOr.GetLeft(), patronCardsInDeck,deckSize);
                    var effectbStrengths = ScoreEffectStrengthsInDeck(effectOr.GetRight(), patronCardsInDeck,deckSize);
                    // A way to give reward for both choices, but give a penalty for not being able to apply both
                    return effectaStrengths * choiceWeight + effectbStrengths * choiceWeight;
                default:
                    throw new ArgumentException("Unexpected effect type: " + effect.GetType().Name);
            }
        }

        private static CardStrengths ScoreEffectStrengthsInDeck(Effect effect, int patronCardsInDeck, int deckSize)
        {
            var result = new CardStrengths();
            switch (effect.Type)
            {
                case EffectType.GAIN_COIN:
                    result.GoldStrength += effect.Amount;
                    break;
                case EffectType.GAIN_POWER:
                    result.PowerStrength += effect.Amount;
                    break;
                case EffectType.GAIN_PRESTIGE: //TODO consider splitting
                case EffectType.OPP_LOSE_PRESTIGE:
                    result.PrestigeStrength += effect.Amount;
                    break;
                case EffectType.REPLACE_TAVERN:
                    result.ReplaceTavernStrength += effect.Amount;
                    break;
                case EffectType.ACQUIRE_TAVERN:
                    result.AquireTavernStrenth += effect.Amount; // TODO debug how amount works here. Amount of cards, or amount of allowed gold cost for card
                    break;
                case EffectType.DESTROY_CARD:
                    result.DestroyCardStrength += effect.Amount;
                    break;
                case EffectType.DRAW:
                    result.DrawStrength += effect.Amount;
                    break;
                case EffectType.OPP_DISCARD:
                    result.OpponentDiscardStrength += effect.Amount;
                    break;
                case EffectType.RETURN_TOP:
                    result.ReturnTopStrength += effect.Amount;
                    break;
                case EffectType.RETURN_AGENT_TOP:
                    result.ReturnAgentTopStrenth += effect.Amount;
                    break;
                case EffectType.TOSS:
                    result.TossStrength += effect.Amount;
                    break;
                case EffectType.KNOCKOUT:
                    result.KnockoutStrength += effect.Amount;
                    break;
                case EffectType.PATRON_CALL:
                    result.IncreasedPatronCallStrength += effect.Amount;
                    break;
                case EffectType.CREATE_SUMMERSET_SACKING:
                    result.SummersetSackingStrength += effect.Amount;
                    break;
                case EffectType.HEAL:
                    result.HealStrength += effect.Amount;
                    break;
                case EffectType.KNOCKOUT_ALL:
                    result.KnockoutAllStrength += 1;
                    break;
                case EffectType.DONATE:
                    result.DonateStrength += effect.Amount;
                    break;
            }

            if (effect.Combo > 1)
            {
                result = result * GetComboProbability(effect, patronCardsInDeck, deckSize);
            }

            return result;
        }

        private static double GetComboProbability(Effect effect, int matchingPatronCardsInDeck, int deckSize)
        {
            // TODO test if gamerunner now allows C# bots to use libraries. Otherwise implement this logic locally 
            // TODO debug if this match is correct
            var hypergeometric = new Hypergeometric(deckSize, matchingPatronCardsInDeck, 5);
            return hypergeometric.CumulativeDistribution(effect.Combo);
        }
    }
}