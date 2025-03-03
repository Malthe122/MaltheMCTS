using ScriptsOfTribute;
using ScriptsOfTribute.Board.Cards;
using ScriptsOfTribute.Serializers;
using SimpleBots.src.MaltheMCTS.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MaltheMCTS
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
        public static double Score(SeededGameState gameState, bool useManualModel)
        {
            double score = 0;

            // base resources
            //TODO custom check for win condition, then just return 1 or -1 (For both prestige and patrons)
            int currentPlayerPrestige = gameState.CurrentPlayer.Prestige + gameState.CurrentPlayer.Power;
            int opponentPrestige = gameState.EnemyPlayer.Prestige;
            // decks (and combo synergies)
            var currentPlayerCompleteDeck = new List<Card>();
            currentPlayerCompleteDeck.AddRange(gameState.CurrentPlayer.Hand);
            currentPlayerCompleteDeck.AddRange(gameState.CurrentPlayer.DrawPile);
            currentPlayerCompleteDeck.AddRange(gameState.CurrentPlayer.Played);
            currentPlayerCompleteDeck.AddRange(gameState.CurrentPlayer.CooldownPile);
            currentPlayerCompleteDeck.AddRange(gameState.CurrentPlayer.Agents.Where(a => a.RepresentingCard.Type != CardType.CONTRACT_AGENT).Select(a => a.RepresentingCard));
            var currentPlayerPatronToDeckRatio = GetPatronRatios(currentPlayerCompleteDeck, gameState.Patrons);
            var currentPlayerDeckStrengths = ScoreStrengthsInDeck(currentPlayerCompleteDeck, currentPlayerPatronToDeckRatio);

            // To allow model to value putting combo cards into your deck before they have an effect
            double currentPlayerDeckComboProportion = currentPlayerCompleteDeck.Where(c => c.Deck != PatronId.TREASURY).Count() / currentPlayerCompleteDeck.Count;

            var opponentCompleteDeck = new List<Card>();
            opponentCompleteDeck.AddRange(gameState.EnemyPlayer.DrawPile);
            opponentCompleteDeck.AddRange(gameState.EnemyPlayer.Played);
            opponentCompleteDeck.AddRange(gameState.EnemyPlayer.CooldownPile);
            opponentCompleteDeck.AddRange(gameState.EnemyPlayer.Agents.Where(a => a.RepresentingCard.Type != CardType.CONTRACT_AGENT).Select(a => a.RepresentingCard));
            var opponentPatronToDeckRatio = GetPatronRatios(opponentCompleteDeck, gameState.Patrons);
            var opponentDeckStrengths = ScoreStrengthsInDeck(opponentCompleteDeck, opponentPatronToDeckRatio);

            // agents
            var currentPlayerAgentStrengths = ScoreStrengthsInDeck(gameState.CurrentPlayer.Agents, currentPlayerPatronToDeckRatio, currentPlayerCompleteDeck.Count);
            var opponentAgentStrengths = ScoreStrengthsInDeck(gameState.EnemyPlayer.Agents, opponentPatronToDeckRatio, opponentCompleteDeck.Count);

            // patrons. Maybe needs to be more sophisticated. Right now, does not look of benefit of having specific patron favours, but just the amount 
            int currentPlayerPatronFavour = 0;
            int opponentPatronFavour = 0;

            foreach (var patron in gameState.Patrons)
            {
                var favouredPlayer = gameState.PatronStates.GetFor(patron);

                if (favouredPlayer == gameState.CurrentPlayer.PlayerID)
                {
                    currentPlayerPatronFavour++;
                }
                else if (favouredPlayer == gameState.EnemyPlayer.PlayerID)
                {
                    opponentPatronFavour++;
                }
            }

            return ModelEvaluation(currentPlayerPrestige, currentPlayerDeckStrengths, currentPlayerDeckComboProportion, currentPlayerAgentStrengths, currentPlayerPatronFavour,
                                    opponentPrestige, opponentDeckStrengths, opponentAgentStrengths, opponentPatronFavour, useManualModel);
        }

        private static double ModelEvaluation(
            int currentPlayerPrestige,
            CardStrengths currentPlayerDeckStrengths,
            CardStrengths currentPlayerAgentStrengths,
            int currentPlayerPatronFavour,
            double currentPlayerDeckComboProportion,
            int opponentPrestige,
            CardStrengths opponentDeckStrengths,
            CardStrengths opponentAgentStrengths,
            int opponentPatronFavour,
            bool useManualModel)
        {
            if (useManualModel)
            {
                return SimpleManualEvaluation.Evaluate(currentPlayerPrestige, currentPlayerDeckStrengths, currentPlayerDeckComboProportion, currentPlayerAgentStrengths, currentPlayerPatronFavour,
                                    opponentPrestige, opponentDeckStrengths, opponentAgentStrengths, opponentPatronFavour);
            }
            else
            {
                // TODO implement model
                throw new NotImplementedException("model not yet implemented");
            }
        }

        private static Dictionary<PatronId, double> GetPatronRatios(List<Card> deck, List<PatronId> patrons)
        {
            var patronToAmount = new Dictionary<PatronId, int>(); // todo Check if Bewilderment has the creating PatronId and whether it does contribute to combo effects
            var patronToDeckRatio = new Dictionary<PatronId, double>();

            foreach(var patron in patrons)
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
                var agentStrength = agentCardStrength + (agentCardStrength * AGENT_HP_VALUE_MULTIPLIER * agent.CurrentHp); // A way of contributing extra strengths to the agent, the more HP it has
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

            // TODO maybe this is where we need to look at draw effects afterwards

            return summedStrengths / deck.Count();
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
                    var effect1Strengths = ScoreEffectStrengthsInDeck(effectComposite._right, patronToDeckRatio, deckSize);
                    var effect2Strengths = ScoreEffectStrengthsInDeck(effectComposite._left, patronToDeckRatio, deckSize);
                    return effect1Strengths + effect2Strengths;
                case EffectOr:
                    var effectOr = (effect as EffectOr)!;
                    var effectaStrengths = ScoreEffectStrengthsInDeck(effectOr._right, patronToDeckRatio, deckSize);
                    var effectbStrengths = ScoreEffectStrengthsInDeck(effectOr._left, patronToDeckRatio, deckSize);
                    // A way to give reward for both choices, but give a penalty for not being able to apply both
                    return (effectaStrengths * CHOICE_WEIGHT) + (effectbStrengths *CHOICE_WEIGHT);
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
            // FUTURE replace with bionomial calculation as this is inaccurate as every time you draw a card beside this patron, the probability of drawing this patron is increased and vice versa (since you cant draw the same cards multiple times)
            double drawProbability = 5 * patronToDeckRatio; //We draw 5 cards at start of each turn
            return Math.Pow(drawProbability, effect.Combo);
        }

        public struct CardStrengths
        {
            public double PrestigeStrength = 0;
            public double PowerStrength = 0;
            public double GoldStrength = 0;
            public double MiscellaneousStrength = 0;
            // Consider if i should consider other overall properties than these four
            public CardStrengths()
            {
            }

            public static CardStrengths operator +(CardStrengths a, CardStrengths b)
            {
                var prestigeStrength = a.PrestigeStrength + b.PrestigeStrength;
                var powerStrength = a.PowerStrength + b.PowerStrength;
                var goldStrength = a.GoldStrength + b.GoldStrength;
                var miscellaneousStrength = a.MiscellaneousStrength + b.MiscellaneousStrength;
                return new CardStrengths()
                {
                    PrestigeStrength = prestigeStrength,
                    PowerStrength = powerStrength,
                    GoldStrength = goldStrength,
                    MiscellaneousStrength = miscellaneousStrength
                };
            }

            public static CardStrengths operator *(CardStrengths a, double multiplier)
            {
                return new CardStrengths()
                {
                    PrestigeStrength = a.PrestigeStrength * multiplier,
                    PowerStrength = a.PowerStrength * multiplier,
                    GoldStrength = a.GoldStrength * multiplier,
                    MiscellaneousStrength = a.MiscellaneousStrength * multiplier
                };
            }

            public static CardStrengths operator /(CardStrengths a, int divisor)
            {
                return new CardStrengths()
                {
                    PrestigeStrength = a.PrestigeStrength / divisor,
                    PowerStrength = a.PowerStrength / divisor,
                    GoldStrength = a.GoldStrength / divisor,
                    MiscellaneousStrength = a.MiscellaneousStrength / divisor
                };
            }
        }
    }
}