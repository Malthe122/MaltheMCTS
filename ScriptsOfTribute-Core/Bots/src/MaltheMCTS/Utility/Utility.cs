using Bots;
using ExternalHeuristic;
using ScriptsOfTribute;
using ScriptsOfTribute.Board.Cards;
using ScriptsOfTribute.Serializers;
using SimpleBots.src.MaltheMCTS.Utility.HeuristicScoring;

namespace MaltheMCTS;

public static class Utility
{
    /// <summary>
    /// Calculated average from 500,800 evaluations on a version of AauBot that only evaluated states at the end of turns
    /// </summary>
    private const double average_bestmcts3_heuristic_end_of_turn_score = 0.35039018318061976f;
    /// <summary>
    /// Calculated average from 11_193_363 states appearing in games of RandomBot playing versus RandomBot and 45_886_781 states generated from AauBot playing versus AauBot
    /// </summary>
    private const double average_bestmcts3_heuristic_score = 0.4855746429f;
    public static Random Rng = new Random();

    public static readonly List<CardId> RANDOM_EFFECT_CARDS = new List<CardId>();

    public static readonly List<CardId> INSTANT_EFFECT_PLAY_CARDS = new List<CardId>();
    public static List<CardId> PRIMITIVE_CARD_RANKING = new List<CardId>();

    internal static void CategorizeCards()
    {
        foreach (var card in GlobalCardDatabase.Instance.AllCards)
        {
            // Effect 0 is play/activation effect
            // FUTURE handle combos
            if (card.Effects[0].IsStochastic())
            {
                RANDOM_EFFECT_CARDS.Add(card.CommonId);
            }

            if (card.Effects.All(e =>
            {
                return e.IsInstantPlayEffect();
            }))
            {
                INSTANT_EFFECT_PLAY_CARDS.Add(card.CommonId);
            }
        }
    }

    public static double UseBestMCTS3Heuristic(SeededGameState gameState, bool onlyEndOfTurns, bool normalize = true)
    {

        GameStrategy strategy;

        var currentPlayer = gameState.CurrentPlayer;
        int cardCount = currentPlayer.Hand.Count + currentPlayer.CooldownPile.Count + currentPlayer.DrawPile.Count;
        int points = gameState.CurrentPlayer.Prestige;
        if (points >= 27 || gameState.EnemyPlayer.Prestige >= 30)
        {
            strategy = new GameStrategy(cardCount, GamePhase.LateGame);
        }
        else if (points <= 10 && gameState.EnemyPlayer.Prestige <= 13)
        {
            strategy = new GameStrategy(cardCount, GamePhase.EarlyGame);
        }
        else
        {
            strategy = new GameStrategy(cardCount, GamePhase.MidGame);
        }

        var result = strategy.Heuristic(gameState);

        if (normalize)
        {
            return NormalizeBestMCTS3Score(result, onlyEndOfTurns);
        }

        return result;
    }

    /// <summary>
    /// For normalizing BestMCTS3 heuristic score into a -1 - 1 value, using knowledge of average BestMCTS3 score
    /// This is needed to be able to treat the game like a zero-sum-game with this heuristic that was made for the
    /// BestMCTS3 that treated the game like a planning problem of a single turn rather than a zero-sum-game
    /// </summary>
    private static double NormalizeBestMCTS3Score(double score, bool onlyEndOfTurns)
    {
        if (onlyEndOfTurns)
        {
            if (score < average_bestmcts3_heuristic_end_of_turn_score)
            {
                return (score - average_bestmcts3_heuristic_end_of_turn_score) / average_bestmcts3_heuristic_end_of_turn_score;
            }
            else
            {
                return (score - average_bestmcts3_heuristic_end_of_turn_score) / (1 - average_bestmcts3_heuristic_end_of_turn_score);
            }
        }
        else
        {
            if (score < average_bestmcts3_heuristic_score)
            {
                return (score - average_bestmcts3_heuristic_score) / average_bestmcts3_heuristic_score;
            }
            else
            {
                return (score - average_bestmcts3_heuristic_score) / (1 - average_bestmcts3_heuristic_score);
            }
        }
    }

    public static Node FindOrBuildNode(SeededGameState seededGameState, Node parent, List<Move> possibleMoves, MaltheMCTS bot)
    {
        var result = new Node(seededGameState, possibleMoves, bot);

        if (bot.Settings.REUSE_TREE)
        {
            if (bot.NodeGameStateHashMap.ContainsKey(result.GameStateHash))
            {
                Node equalNode = null;
                try
                {
                    equalNode = bot.NodeGameStateHashMap[result.GameStateHash].SingleOrDefault(node => node.GameState.IsIdentical(result.GameState));
                }
                catch (Exception e)
                {
                    var error = "Somehow two identical states were both added to hashmap.\n";
                    error += "State hashes:\n";
                    bot.NodeGameStateHashMap[result.GameStateHash].ToList().ForEach(n => { error += n.GameStateHash + "\n"; });
                    error += "Full states:\n";
                    bot.NodeGameStateHashMap[result.GameStateHash].ToList().ForEach(n => n.GameState.Log());
                }

                if (equalNode != null)
                {
                    result = equalNode;
                }
                else
                {
                    bot.NodeGameStateHashMap[result.GameStateHash].Add(result);
                }
            }
            else
            {
                bot.NodeGameStateHashMap.Add(result.GameStateHash, new List<Node>() { result });
            }
        }

        return result;
    }

    /// <summary>
    /// Since we reuse identical states, our move will not be identical to the move in the official gamestate, since although gamestates are logically identical
    /// we might have a specific card on hand with ID 1 in our gamestate, while the official gamestate has an identical card in our hand but with a different id.
    /// Becuase of this, we need to find the offical move that is logically identcal to our move
    /// </summary>
    internal static Move FindOfficialMove(Move move, List<Move> possibleMoves)
    {
        return possibleMoves.First(m => m.IsIdentical(move));
    }

    internal static float SaveDivision(int arg1, int arg2)
    {
        if (arg1 == 0 || arg2 == 0)
        {
            return 0;
        }

        return arg1 / arg2;
    }

    /// <summary>
    /// SoT framework handles moves equal moves like different moves if they refer to different card ids of the same type. I consider
    /// playing the same card (with different ids) as identical moves, since their impact on the game is 100 % identical
    /// </summary>
    public static List<Move> RemoveDuplicateMoves(List<Move> moves)
    {
        var uniqueMoves = new List<Move>();
        foreach (var currMove in moves)
        {
            if (!uniqueMoves.Any(m => m.IsIdentical(currMove)))
            {
                uniqueMoves.Add(currMove);
            }
        }

        return uniqueMoves;
    }

    public static List<UniqueCard> RankCardsInGameState(SeededGameState gameState, IEnumerable<UniqueCard> cards)
    {
        // Add hashsets with common ids, so calculation only needs to be done ones for each type
        var rankedCardTypes = new Dictionary<CardId, double>();
        var completeDeck = GetCurrentPlayerCompleteDeck(gameState);
        var patronRatios = FeatureSetUtility.GetPatronRatios(completeDeck, gameState.Patrons);

        var orderedCards = cards.OrderByDescending(c =>
        {
            if (rankedCardTypes.ContainsKey(c.CommonId))
            {
                return rankedCardTypes[c.CommonId];
            }
            else
            {
                var score = CardStrengthsToScore(FeatureSetUtility.ScoreStrengthsInDeck(c, patronRatios[c.Deck], completeDeck.Count));
                if(c.Deck != PatronId.TREASURY)
                {
                    score += 0.1 * patronRatios[c.Deck]; // To favor cards that fit into the deck, if their effects are equal
                }
                rankedCardTypes.Add(c.CommonId, score);
                return score;
            }
        });

        return orderedCards.ToList();
    }

    private static List<Card> GetCurrentPlayerCompleteDeck(SeededGameState gameState)
    {
        var currentPlayerCompleteDeck = new List<Card>();
        currentPlayerCompleteDeck.AddRange(gameState.CurrentPlayer.Hand);
        currentPlayerCompleteDeck.AddRange(gameState.CurrentPlayer.DrawPile);
        currentPlayerCompleteDeck.AddRange(gameState.CurrentPlayer.Played);
        currentPlayerCompleteDeck.AddRange(gameState.CurrentPlayer.CooldownPile);
        currentPlayerCompleteDeck.AddRange(gameState.CurrentPlayer.Agents.Where(a => a.RepresentingCard.Type != CardType.CONTRACT_AGENT).Select(a => a.RepresentingCard));

        return currentPlayerCompleteDeck;
    }

    private static double CardStrengthsToScore(CardStrengths cardStrengths)
    {
        // TODO maybe there should be some logic here that prefers power and prestige later in the game and coins early in the game
        return cardStrengths.GoldStrength
            + cardStrengths.MiscellaneousStrength
            + cardStrengths.PowerStrength
            + cardStrengths.PrestigeStrength;
    }

    /// <returns>
    /// combinationAmount amount of combination in the following order using the rankedList:
    /// 1. [0, 1]
    /// 2. [0, 2]
    /// 3. [1, 2]
    /// 4. [0, 3]
    /// 5. [1, 3]
    /// 6. [2, 3]
    /// etc.
    /// Unless includeSingle and/or includeEmpty flags are enabled
    /// </returns>
    public static List<Move> GetRankedCardCombinationMoves(List<Move> moves, List<UniqueCard> rankedCardList, int moveCount, bool includeEmptyAndSingleChoice)
    {
        var result = new List<Move>();

        if (includeEmptyAndSingleChoice)
        {
            switch (moveCount)
            {
                case 1: // Just best combination of two cards
                    var topCards = rankedCardList.Take(2).ToList();
                    result = new List<Move>() {
                                            moves.First(m =>
                                            {
                                                var move = (m as MakeChoiceMoveUniqueCard);
                                                return move!.Choices.Any(c => topCards[0].CommonId == c.CommonId)
                                                && move.Choices.Any(c => topCards[1].CommonId == c.CommonId);
                                            })};
                    break;
                case 2: // Best combination of two cards and best choice of 1 card only
                    topCards = rankedCardList.Take(2).ToList();
                    result = new List<Move>() {
                                            moves.First(m =>
                                            {
                                                var move = (m as MakeChoiceMoveUniqueCard);
                                                return move!.Choices.Any(c => topCards[0].CommonId == c.CommonId)
                                                && move.Choices.Any(c => topCards[1].CommonId == c.CommonId);
                                            })};
                    var singleChoiceMove = moves.First(m =>
                    {
                        var move = m as MakeChoiceMoveUniqueCard;
                        return move.Choices.Count == 1
                            && move.Choices[0].CommonId == rankedCardList[0].CommonId;
                    });
                    result.Add(singleChoiceMove);
                    break;
                case 3: // Best combination of two cards, best choice of 1 card only and no choice
                    topCards = rankedCardList.Take(2).ToList();
                    result = new List<Move>() {
                                            moves.First(m =>
                                            {
                                                var move = (m as MakeChoiceMoveUniqueCard);
                                                return move!.Choices.Any(c => topCards[0].CommonId == c.CommonId)
                                                && move.Choices.Any(c => topCards[1].CommonId == c.CommonId);
                                            })};
                    singleChoiceMove = moves.First(m =>
                    {
                        var move = m as MakeChoiceMoveUniqueCard;
                        return move.Choices.Count == 1
                            && move.Choices[0].CommonId == rankedCardList[0].CommonId;
                    });
                    result.Add(singleChoiceMove);
                    var emptyMove = moves.First(m => (m as MakeChoiceMoveUniqueCard).Choices.Count == 0);
                    break;
                default: // 4 or more
                    int singleChoiceMoveCount = moveCount / 2;
                    int twoChoicesMoveCount = moveCount / 2;

                    if (singleChoiceMoveCount + twoChoicesMoveCount == moveCount) // I need it to be 1 lower, so there is room the the no-choice move
                    {
                        singleChoiceMoveCount -= 1; // Lowering this rather than twoChoice, since twoChoice is generally the best move
                    }

                    var noChoiceMove = moves.FirstOrDefault(m => (m as MakeChoiceMoveUniqueCard).Choices.Count == 0);

                    topCards = rankedCardList.TakeLast(singleChoiceMoveCount).ToList();
                    var singleChoiceMoves = moves.Where(m =>
                    (m as MakeChoiceMoveUniqueCard).Choices.Count == 1
                    && topCards.Any(c => (m as MakeChoiceMoveUniqueCard).Choices[0].CommonId == c.CommonId));

                    var twoChoiceMoves = GetBestTwoChoiceMoves(moves, rankedCardList, twoChoicesMoveCount);

                    result = singleChoiceMoves.Concat(twoChoiceMoves).ToList();

                    //Quick fix for when the additional filtering has removed the empty move. In reality another move should be added instead
                    //To follow the set branching size, but this is a quick fix, making sure that we do not try to play null move.
                    //This only happens in edge cases (e.g when bot has the chance to destroy curses and additional filter, removes
                    //the possibility to destroy nothing
                    if (noChoiceMove != null)
                    {
                        result.Add(noChoiceMove);
                    }
                    
                    break;
            }

            return result;
        }
        else
        {
            return GetBestTwoChoiceMoves(moves, rankedCardList, moveCount);
        }
    }

    private static List<Move> GetBestTwoChoiceMoves(List<Move> moves, List<UniqueCard> rankedCardList, int moveCount)
    {
        var result = new List<Move>();

        for (int j = 1; j < rankedCardList.Count && result.Count < moveCount; j++)
        {
            for (int i = 0; i < j && result.Count < moveCount; i++)
            {
                var move = moves.First(m =>
                {
                    var move = m as MakeChoiceMoveUniqueCard;
                    return move!.Choices.Any(c => c.CommonId == rankedCardList[i].CommonId)
                    && move!.Choices.Any(c => c.CommonId == rankedCardList[j].CommonId);
                });
                result.Add(move);
            }
        }

        return result;
    }

}
