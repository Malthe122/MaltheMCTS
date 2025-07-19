using EnsembleTreeModelBuilder;
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
        public static double Score(SeededGameState gameState, RegressionTrainer? featureSetModelType, bool endOfTurnExclusive = true)
        {
            // The manual model (null) does not return either 0 and 1 or -1 and 1, so this logic does not apply for it
            if (featureSetModelType != null)
            {
                var winner = CheckWinner(gameState, endOfTurnExclusive);

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

            if (featureSetModelType == RegressionTrainer.LbfgsPoissonRegression || featureSetModelType == RegressionTrainer.StochasticDualCoordinateAscent)
            {
                return linearModelEvaluation(featureSet, featureSetModelType.Value);
            }


            return ModelEvaluation(featureSet, featureSetModelType);
        }

        private static double linearModelEvaluation(GameStateFeatureSet featureSet, RegressionTrainer featureSetModelType)
        {
            GameStage stage = GameStage.Early;

            var maxPrestige = Math.Max(featureSet.CurrentPlayerPrestige, featureSet.OpponentPrestige);

            if (maxPrestige > 39)
            {
                stage = GameStage.End;
            } else if (maxPrestige > 30)
            {
                stage = GameStage.Late;
            } else if (maxPrestige > 15)
            {
                stage = GameStage.Mid;
            }

            var csvFeatureSet = featureSet.ToLinearCsvRow();

            return EnsembledTreeModelEvaluation.GetWinProbability(csvFeatureSet, featureSetModelType, stage);
        }

        private static PlayerEnum CheckWinner(SeededGameState gameState, bool endOfTurnExclusiveEvaluation)
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

            if (endOfTurnExclusiveEvaluation && opponentPrestige >= 40 && opponentPrestige > currentPlayerPrestige)
            {
                return gameState.EnemyPlayer.PlayerID;
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

        private static double ModelEvaluation(GameStateFeatureSet featureSet, RegressionTrainer? featureSetModelType)
        {
            if (featureSetModelType == null)
            {
                return SimpleManualEvaluation.Evaluate(featureSet);
            }
            else
            {
                var csvFeatureSet = featureSet.ToCsvRow();
                return EnsembledTreeModelEvaluation.GetWinProbability(csvFeatureSet, featureSetModelType!.Value);
            }
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

        private static CardStrengths ScoreStrengthsInDeck(List<SerializedAgent> agents, Dictionary<PatronId, double> patronToDeckRatio, int deckSize)
        {
            var result = new CardStrengths();

            foreach (var agent in agents)
            {
                var agentCardStrength = ScoreStrengthsInDeck(agent.RepresentingCard, patronToDeckRatio[agent.RepresentingCard.Deck], deckSize) * BASE_AGENT_STRENGTH_MULTIPLIER;
                var agentStrength = agentCardStrength + agentCardStrength * AGENT_HP_VALUE_MULTIPLIER * agent.CurrentHp; // A way of contributing extra strengths to the agent, the more HP it has
                if (agent.RepresentingCard.Taunt)
                {
                    agentStrength.PrestigeStrength += agent.CurrentHp;
                }

                result += agentStrength;
            }

            return result;
        }

        private static CardStrengths ScoreStrengthsInDeck(List<Card> deck, Dictionary<PatronId, double> patronToDeckRatio)
        {
            var summedStrengths = new CardStrengths();

            foreach (var currCard in deck)
            {
                summedStrengths += ScoreStrengthsInDeck(currCard, patronToDeckRatio[currCard.Deck], deck.Count);
            }

            // FUTURE maybe this is where we need to look at draw effects afterwards
            return summedStrengths / deck.Count;
        }

        private static CardStrengths ScoreStrengthsInDeck(Card card, double patronToDeckRatio, int deckSize)
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
                    result += ScoreComplexEffectStrengthsInDeck(effect, patronToDeckRatio, deckSize);
                }
            }

            return result;
        }

        private static CardStrengths ScoreComplexEffectStrengthsInDeck(ComplexEffect effect, double patronToDeckRatio, int deckSize)
        {
            switch (effect)
            {
                case Effect:
                    return ScoreEffectStrengthsInDeck((effect as Effect)!, patronToDeckRatio, deckSize);
                case EffectComposite:
                    var effectComposite = (effect as EffectComposite)!;
                    var effect1Strengths = ScoreEffectStrengthsInDeck(null, patronToDeckRatio, deckSize); //TODO remove null
                    var effect2Strengths = ScoreEffectStrengthsInDeck(null, patronToDeckRatio, deckSize); //TODO remove null
                    return effect1Strengths + effect2Strengths;
                case EffectOr:
                    var effectOr = (effect as EffectOr)!;
                    var effectaStrengths = ScoreEffectStrengthsInDeck(null, patronToDeckRatio, deckSize); //TODO remove null
                    var effectbStrengths = ScoreEffectStrengthsInDeck(null, patronToDeckRatio, deckSize); //TODO remove null
                    // A way to give reward for both choices, but give a penalty for not being able to apply both
                    return effectaStrengths * CHOICE_WEIGHT + effectbStrengths * CHOICE_WEIGHT;
                default:
                    throw new ArgumentException("Unexpected effect type: " + effect.GetType().Name);
            }
        }

        private static CardStrengths ScoreEffectStrengthsInDeck(Effect effect, double patronToDeckRatio, int deckSize)
        {
            var result = new CardStrengths();
            switch (effect.Type)
            {
                case EffectType.ACQUIRE_TAVERN:
                case EffectType.CREATE_SUMMERSET_SACKING:
                case EffectType.DESTROY_CARD:
                case EffectType.DRAW:
                // FUTURE use overall strengths of deck if possible
                case EffectType.HEAL:
                case EffectType.KNOCKOUT:
                case EffectType.OPP_DISCARD:
                case EffectType.PATRON_CALL:
                case EffectType.REPLACE_TAVERN:
                case EffectType.RETURN_TOP:
                case EffectType.TOSS:
                    // FUTURE Do something more sophisticated with these
                    result.MiscellaneousStrength += 1;
                    break;
                case EffectType.GAIN_COIN:
                    result.GoldStrength += effect.Amount;
                    break;
                case EffectType.GAIN_POWER:
                    result.PowerStrength += effect.Amount;
                    break;
                case EffectType.GAIN_PRESTIGE:
                case EffectType.OPP_LOSE_PRESTIGE:
                    result.PrestigeStrength += effect.Amount;
                    break;
            }

            if (effect.Combo > 1)
            {
                result = result * GetComboProbability(effect, patronToDeckRatio, deckSize);
            }

            return result;
        }

        private static double GetComboProbability(Effect effect, double patronToDeckRatio, int deckSize)
        {
            // FUTURE consider replacing with bionomial calculation as this is inaccurate as every time you draw a card beside this patron, the probability of drawing
            // this patron is increased and vice versa (since you cant draw the same cards multiple times)
            double drawProbability = 5 * patronToDeckRatio; //We draw 5 cards at start of each turn
            return Math.Pow(drawProbability, effect.Combo);
        }
    }
}