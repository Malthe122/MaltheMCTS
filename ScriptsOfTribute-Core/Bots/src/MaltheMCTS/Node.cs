using ScriptsOfTribute;
using ScriptsOfTribute.Board.Cards;
using ScriptsOfTribute.Serializers;
using SimpleBots.src.MaltheMCTS.Utility.HeuristicScoring;
using System.Linq;

namespace MaltheMCTS;

public class Node
{
    public Dictionary<Move, Node> MoveToChildNode;
    public int VisitCount = 0;
    public double TotalScore = 0;
    public int GameStateHash { get; private set; }
    public SeededGameState GameState { get; private set; }
    public List<Move> PossibleMoves;

    internal MaltheMCTS Bot;

    private List<UniqueCard> CardsInHandRanked;
    private List<UniqueCard> CardsInCooldownRanked;
    private List<UniqueCard> CardsInTavernRanked;
    private List<UniqueCard> CardsInDrawPileRanked;
    private List<UniqueCard> CardsPlayedRanked;

    /// <summary>
    /// Only used when SimulateMultipleTurns is disabled. It is a copy of this node, but representing the current score/visits of the node if end_turn is played, but without
    /// affecting the state with the card draws that happens on end_turn, since with this feature disabled, we do not want this to be part of our simulations.
    /// </summary>
    private Node? endNode;

    public Node(SeededGameState gameState, List<Move> possibleMoves, MaltheMCTS bot)
    {
        GameState = gameState;
        PossibleMoves = possibleMoves;
        Bot = bot;
        ApplyInstantMoves();
        FilterMoves();
        MoveToChildNode = new Dictionary<Move, Node>();
    }

    public virtual void Visit(out double score, HashSet<Node> visitedNodes)
    {

        if (visitedNodes.Contains(this))
        {
            score = Score();
            TotalScore += score;
            VisitCount++;
            return;
        }

        visitedNodes.Add(this);

        var playerId = GameState.CurrentPlayer.PlayerID;

        if (GameState.GameEndState == null)
        {
            if (VisitCount == 0)
            {
                ApplyInstantMoves();
                score = Score();
            }
            else if (PossibleMoves.Count > MoveToChildNode.Count)
            {
                var expandedChild = Expand();
                expandedChild.Visit(out score, visitedNodes);
            }
            else
            {
                var selectedChild = Select();
                selectedChild.Visit(out score, visitedNodes);

                if (selectedChild.GameState.CurrentPlayer.PlayerID != playerId)
                {
                    score *= -1; // this assumes the score is representing a winrate in a zero-sum-game format
                }
            }
        }
        else
        {
            score = Score();
        }

        TotalScore += score;
        VisitCount++;
    }


    internal Node Expand()
    {
        foreach (var currMove in PossibleMoves)
        {
            Node newChild = null;

            if (!MoveToChildNode.Keys.Any(m => m.IsIdentical(currMove)))
            {
                if (!Bot.Settings.SIMULATE_MULTIPLE_TURNS && currMove.Command == CommandEnum.END_TURN)
                {
                    newChild = new EndNode(GameState, PossibleMoves, Bot);
                }
                else if ((Bot.Settings.INCLUDE_PLAY_MOVE_CHANCE_NODES && currMove.IsStochastic(GameState))
                    || Bot.Settings.INCLUDE_END_TURN_CHANCE_NODES && currMove.Command == CommandEnum.END_TURN)
                {
                    newChild = new ChanceNode(GameState, this, currMove, Bot);
                }
                else
                {
                    ulong randomSeed = (ulong)Utility.Rng.Next();
                    var (newGameState, newPossibleMoves) = GameState.ApplyMove(currMove, randomSeed);
                    newChild = Utility.FindOrBuildNode(newGameState, this, newPossibleMoves, Bot);
                }

                if (newChild != null &&
                !Bot.Settings.SIMULATE_MULTIPLE_TURNS &&
                newChild.PossibleMoves.Count == 1 &&
                newChild.PossibleMoves[0].Command == CommandEnum.END_TURN)
                {
                    newChild = new EndNode(GameState, PossibleMoves, Bot);
                }

                MoveToChildNode.Add(currMove, newChild);
                return newChild;
            }
        }

        throw new Exception("Expand was unexpectedly called on a node that was fully expanded");
    }

    internal double Score()
    {
        switch (Bot.Settings.CHOSEN_SCORING_METHOD)
        {
            case ScoringMethod.Rollout:
                return Rollout();
            case ScoringMethod.BestMCTS3Heuristic:
                return Utility.UseBestMCTS3Heuristic(GameState, false, Bot.Settings.SIMULATE_MULTIPLE_TURNS);
            case ScoringMethod.RolloutTurnsCompletionsThenHeuristic:
                return RolloutTillTurnsEndThenHeuristic(Bot.Settings.ROLLOUT_TURNS_BEFORE_HEURISTIC);
            case ScoringMethod.MaltheScoring:
                // TODO add rollout to end of turn before scoring
                return HeuristicScoring.Score(GameState, Bot.Settings.FEATURE_SET_MODEL_TYPE);
            default:
                throw new NotImplementedException("Tried to applied non-implemented scoring method: " + Bot.Settings.CHOSEN_SCORING_METHOD);
        }
    }

    private double RolloutTillTurnsEndThenHeuristic(int turnsToComplete) //TODO fix, so that rollout till end of turn, does not end turn
    {
        int rolloutTurnsCompleted = 0;
        var rolloutPlayer = GameState.CurrentPlayer;
        var rolloutGameState = GameState;
        var rolloutPossibleMoves = PossibleMoves;

        while (rolloutTurnsCompleted < turnsToComplete && rolloutGameState.GameEndState == null)
        {
            if (Bot.Settings.FORCE_DELAY_TURN_END_IN_ROLLOUT)
            {
                if (rolloutPossibleMoves.Count > 1)
                {
                    rolloutPossibleMoves.RemoveAll(Move => Move.Command == CommandEnum.END_TURN);
                }
            }

            var chosenIndex = Utility.Rng.Next(rolloutPossibleMoves.Count);
            var moveToMake = rolloutPossibleMoves[chosenIndex];

            var (newGameState, newPossibleMoves) = rolloutGameState.ApplyMove(moveToMake);

            if (newGameState.CurrentPlayer != rolloutPlayer)
            {
                rolloutTurnsCompleted++;
                rolloutPlayer = newGameState.CurrentPlayer;
            }

            rolloutGameState = newGameState;
            rolloutPossibleMoves = newPossibleMoves;
        }

        var stateScore = Utility.UseBestMCTS3Heuristic(rolloutGameState, true);

        if (GameState.CurrentPlayer != rolloutGameState.CurrentPlayer)
        {
            stateScore *= -1;
        }

        return stateScore;
    }

    internal double Rollout()
    {
        double result = 0;
        var rolloutGameState = GameState;
        var rolloutPlayerId = rolloutGameState.CurrentPlayer.PlayerID;
        var rolloutPossibleMoves = new List<Move>(PossibleMoves);

        // TODO also apply the playing obvious moves in here, possibly
        while (rolloutGameState.GameEndState == null)
        {
            if (Bot.Settings.FORCE_DELAY_TURN_END_IN_ROLLOUT)
            {
                if (rolloutPossibleMoves.Count > 1)
                {
                    rolloutPossibleMoves.RemoveAll(Move => Move.Command == CommandEnum.END_TURN);
                }
            }
            var chosenIndex = Utility.Rng.Next(rolloutPossibleMoves.Count);
            var moveToMake = rolloutPossibleMoves[chosenIndex];

            var (newGameState, newPossibleMoves) = rolloutGameState.ApplyMove(moveToMake);
            rolloutGameState = newGameState;
            rolloutPossibleMoves = Utility.RemoveDuplicateMoves(newPossibleMoves);
        }

        if (rolloutGameState.GameEndState.Winner != PlayerEnum.NO_PLAYER_SELECTED)
        {
            if (rolloutGameState.GameEndState.Winner == rolloutPlayerId)
            {
                result += 1;
            }
            else
            {
                result -= 1;
            }
        }

        return result;
    }

    internal virtual Node Select()
    {
        double maxConfidence = -double.MaxValue;
        var highestConfidenceChild = MoveToChildNode.First().Value;

        foreach (var childNode in MoveToChildNode.Values)
        {
            double confidence = childNode.GetConfidenceScore(VisitCount);
            if (confidence > maxConfidence)
            {
                maxConfidence = confidence;
                highestConfidenceChild = childNode;
            }
        }

        return highestConfidenceChild;
    }

    /// <param name="parentVisitCount"> Must be supplied here as Nodes does not have a single fixed parent becuase of tree-reusal</param>
    /// <returns></returns>
    public double GetConfidenceScore(int parentVisitCount)
    {
        switch (Bot.Settings.CHOSEN_SELECTION_METHOD)
        {
            case SelectionMethod.UCT:
                double exploitation = TotalScore / VisitCount;
                double exploration = Bot.Settings.UCT_EXPLORATION_CONSTANT * Math.Sqrt(Math.Log(parentVisitCount) / VisitCount);
                return exploitation + exploration;
            case SelectionMethod.Custom:
                return TotalScore - VisitCount;
            default:
                return 0;
        }
    }

    internal void ApplyInstantMoves()
    {
        foreach (var currMove in PossibleMoves)
        {
            if (currMove.IsInstantPlay())
            {
                (GameState, var possibleMoves) = GameState.ApplyMove(currMove, (ulong)Utility.Rng.Next());
                PossibleMoves = possibleMoves;
                FilterMoves();
                ApplyInstantMoves();
                break;
            }
        }

        CardsInHandRanked = null;
        CardsInCooldownRanked = null;
        CardsInTavernRanked = null;
        CardsInDrawPileRanked = null;
        CardsPlayedRanked = null;
        GameStateHash = GameState.GenerateHash();
    }

    private void FilterMoves()
    {
        PossibleMoves = Utility.RemoveDuplicateMoves(PossibleMoves);

        #region additionalFiltering
        switch (GameState.BoardState)
        {
            case ScriptsOfTribute.Board.CardAction.BoardState.CHOICE_PENDING:
                switch (GameState.PendingChoice!.ChoiceFollowUp)
                {
                    case ChoiceFollowUp.ENACT_CHOSEN_EFFECT:
                    case ChoiceFollowUp.ACQUIRE_CARDS:
                    case ChoiceFollowUp.REFRESH_CARDS:
                    case ChoiceFollowUp.TOSS_CARDS:
                    case ChoiceFollowUp.KNOCKOUT_AGENTS:
                    case ChoiceFollowUp.COMPLETE_HLAALU:
                    case ChoiceFollowUp.COMPLETE_PELLIN:
                    case ChoiceFollowUp.COMPLETE_PSIJIC:
                    case ChoiceFollowUp.REPLACE_CARDS_IN_TAVERN:
                        break;
                    case ChoiceFollowUp.DESTROY_CARDS:
                        var cardsInHandAndPlayed = GameState.CurrentPlayer.Played.Concat(GameState.CurrentPlayer.Hand);
                        SetBewildermentGoldChoiceMoves(cardsInHandAndPlayed);
                        break;
                    case ChoiceFollowUp.DISCARD_CARDS:
                        SetBewildermentGoldChoiceMoves(GameState.CurrentPlayer.Hand);
                        break;
                    case ChoiceFollowUp.COMPLETE_TREASURY:
                        cardsInHandAndPlayed = GameState.CurrentPlayer.Played.Concat(GameState.CurrentPlayer.Hand);
                        SetBewildermentGoldChoiceMoves(cardsInHandAndPlayed);
                        break;
                }
                break;
            case ScriptsOfTribute.Board.CardAction.BoardState.NORMAL:
                // Limit to play all cards before buying from tavern or activating patrons
                bool canPlayCards = GameState.CurrentPlayer.Hand.Count > 0;
                if (canPlayCards)
                {
                    PossibleMoves = PossibleMoves.Where(m => m.Command == CommandEnum.PLAY_CARD).ToList();
                }
                break;
            case ScriptsOfTribute.Board.CardAction.BoardState.START_OF_TURN_CHOICE_PENDING:
                switch (GameState.PendingChoice!.ChoiceFollowUp)
                {
                    case ChoiceFollowUp.DISCARD_CARDS:
                        var cardsHandAndPlayed = GameState.CurrentPlayer.Played.Concat(GameState.CurrentPlayer.Hand);
                        SetBewildermentGoldChoiceMoves(cardsHandAndPlayed);
                        break;
                    default:
                        throw new NotImplementedException("Unexpected choice follow up: " + GameState.PendingChoice!.ChoiceFollowUp);
                }
                break;
            // Complete treasury seems to be a patron choice, so not sure that the complete treasury enum value is for
            case ScriptsOfTribute.Board.CardAction.BoardState.PATRON_CHOICE_PENDING:
                var InHandAndPlayed = GameState.CurrentPlayer.Played.Concat(GameState.CurrentPlayer.Hand);
                SetBewildermentGoldChoiceMoves(InHandAndPlayed);
                break;
        }
        #endregion

        if (Bot.Settings.CHOICE_BRANCH_LIMIT != null && PossibleMoves.Count > Bot.Settings.CHOICE_BRANCH_LIMIT)
        {
            switch (GameState.BoardState)
            {
                case ScriptsOfTribute.Board.CardAction.BoardState.CHOICE_PENDING:
                    switch (GameState.PendingChoice!.ChoiceFollowUp)
                    {
                        case ChoiceFollowUp.ENACT_CHOSEN_EFFECT:
                            Console.WriteLine("UNEXPECTED BRANCH LIMIT VIOLATION (enact chosen effect)");
                            break;
                        case ChoiceFollowUp.ACQUIRE_CARDS:
                            if (CardsInTavernRanked == null)
                            {
                                CardsInTavernRanked = Utility.RankCardsInGameState(GameState, GameState.TavernAvailableCards);
                            }
                            // Aquire in this patch, always is a maximum of 1 card
                            var maxPrice = PossibleMoves.Max(m => {
                                var move = (MakeChoiceMoveUniqueCard)m;
                                return move.Choices.Count > 0 ? move.Choices[0].Cost : 0;
                            });
                            var allowedCards = CardsInTavernRanked.Where(c => c.Cost <= maxPrice);
                            var topTavernCards = allowedCards.Take(Bot.Settings.CHOICE_BRANCH_LIMIT!.Value - 1);
                            PossibleMoves = PossibleMoves.Where(m =>
                                (m as MakeChoiceMoveUniqueCard).Choices.Count == 0
                                || topTavernCards.Any(c => (m as MakeChoiceMoveUniqueCard).Choices[0].CommonId == c.CommonId)
                                ).ToList();
                            break;
                        case ChoiceFollowUp.DESTROY_CARDS:
                            if (CardsPlayedRanked == null) // In SoT, the destroy also allows to destroy from hand, but to assist bot, i exclude this, cause its almost best to play the card first
                            {
                                CardsPlayedRanked = Utility.RankCardsInGameState(GameState, GameState.CurrentPlayer.Played);
                            }
                            int maxAmount = PossibleMoves.Max(m => (m as MakeChoiceMoveUniqueCard).Choices.Count);
                            if (maxAmount == 1)
                            {
                                var worstPlayedPileCards = CardsPlayedRanked.TakeLast(Bot.Settings.CHOICE_BRANCH_LIMIT!.Value - 1);
                                PossibleMoves = PossibleMoves.Where(m =>
                                (m as MakeChoiceMoveUniqueCard).Choices.Count == 0
                                || worstPlayedPileCards.Any(c => (m as MakeChoiceMoveUniqueCard).Choices[0].CommonId == c.CommonId))
                                .ToList();
                            }
                            else // Here the possible destroy amount is 2, since thats the max in the patch
                            {
                                PossibleMoves = Utility.GetRankedCardCombinationMoves(PossibleMoves, CardsPlayedRanked.AsEnumerable().Reverse().ToList(), Bot.Settings.CHOICE_BRANCH_LIMIT!.Value, true);
                            }
                            break;
                        case ChoiceFollowUp.DISCARD_CARDS:
                            // Discard in this patch is always 1 card
                            if (CardsInHandRanked == null)
                            {
                                CardsInHandRanked = Utility.RankCardsInGameState(GameState, GameState.CurrentPlayer.Hand.ToList());
                            }
                            var bottumHandCards = CardsInHandRanked.TakeLast(Bot.Settings.CHOICE_BRANCH_LIMIT!.Value).ToList();
                            PossibleMoves = PossibleMoves.Where(m => 
                                    bottumHandCards.Any(c => (m as MakeChoiceMoveUniqueCard).Choices[0].CommonId == c.CommonId))
                                .ToList();
                            break;
                        case ChoiceFollowUp.REFRESH_CARDS: //Means moving cards from cooldown to top of drawpile
                            //if (CardsInCooldownRanked == null)
                            //{
                            //    CardsInCooldownRanked = Utility.RankCardsInGameState(GameState, GameState.CurrentPlayer.CooldownPile);
                            //}
                            // Skips this. As its bit more complex. Excludes empty moves in SoT (unlike ToT, as far as i can see) refresh requires a minimum of 1
                            // But can also allow more than 2
                            break;
                        case ChoiceFollowUp.TOSS_CARDS:
                        // Not included in this patch
                        case ChoiceFollowUp.KNOCKOUT_AGENTS:
                            // Theoretically it could make sense to leave agents up to stop opponent from playing even stronger agents, but i see this as purely theoretical and
                            // not something that actually happens in games, so to optimize the MCTS-search, i exclude any moves that does not knockout the maximum amount of agents
                            int allowedAmount = PossibleMoves.Max(m => (m as MakeChoiceMoveUniqueCard).Choices.Count);
                            int knockoutCount = Math.Min(GameState.EnemyPlayer.Agents.Count, allowedAmount);
                            PossibleMoves = PossibleMoves.Where(m => (m as MakeChoiceMoveUniqueCard).Choices.Count == knockoutCount).ToList();
                            // FUTURE consider checking branch limit here too (but other method cant be used, since these a serializedAgents not uniqueCard), but branching factor likely wont get
                            // excessive here
                            break;
                        case ChoiceFollowUp.COMPLETE_HLAALU:
                            Console.WriteLine("UNEXPECTED BRANCH LIMIT VIOLATION (COMPLETE_HLAALU)");
                            break;
                        case ChoiceFollowUp.COMPLETE_PELLIN:
                            Console.WriteLine("UNEXPECTED BRANCH LIMIT VIOLATION (COMPLETE_PELLIN)");
                            break;
                        case ChoiceFollowUp.COMPLETE_PSIJIC:
                            Console.WriteLine("UNEXPECTED BRANCH LIMIT VIOLATION (COMPLETE_PSIJIC)");
                            break;
                        case ChoiceFollowUp.COMPLETE_TREASURY:
                            if (CardsPlayedRanked == null) // In SoT, the destroy also allows to destroy from hand, but to assist bot, i exclude this, cause its almost best to play the card first
                            {
                                CardsPlayedRanked = Utility.RankCardsInGameState(GameState, GameState.CurrentPlayer.Played);
                            }
                            // Treasury is just a single destroy
                            var worstPlayedCards = CardsPlayedRanked.TakeLast(Bot.Settings.CHOICE_BRANCH_LIMIT!.Value - 1);
                            PossibleMoves = PossibleMoves.Where(m =>
                            worstPlayedCards.Any(c => (m as MakeChoiceMoveUniqueCard).Choices[0].CommonId == c.CommonId)
                            || (m as MakeChoiceMoveUniqueCard).Choices.Count == 0)
                            .ToList();
                            break;
                        case ChoiceFollowUp.REPLACE_CARDS_IN_TAVERN:
                            // Not sure here how to make some good logic. Instead, just makes some random moves available
                            var indexes = new HashSet<int>();
                            while (indexes.Count < Bot.Settings.CHOICE_BRANCH_LIMIT!.Value)
                            {
                                indexes.Add(Utility.Rng.Next(PossibleMoves.Count));
                            }
                            PossibleMoves = indexes.Select(i => PossibleMoves[i]).ToList();
                            break;
                    }
                    break;
                case ScriptsOfTribute.Board.CardAction.BoardState.NORMAL:
                    // Here i probably do not want to limit
                    break;
                case ScriptsOfTribute.Board.CardAction.BoardState.START_OF_TURN_CHOICE_PENDING:
                    switch (GameState.PendingChoice!.ChoiceFollowUp)
                    {
                        case ChoiceFollowUp.DISCARD_CARDS: // Always only 1 in this patch
                            if (CardsInHandRanked == null)
                            {
                                CardsInHandRanked = Utility.RankCardsInGameState(GameState, GameState.CurrentPlayer.Hand);
                            }
                            var worstCards = CardsInHandRanked.TakeLast(Bot.Settings.CHOICE_BRANCH_LIMIT!.Value);
                            PossibleMoves = PossibleMoves.Where(m => 
                                    worstCards.Any(c => (m as MakeChoiceMoveUniqueCard).Choices[0].CommonId == c.CommonId))
                                .ToList();
                            break;
                        default:
                            Console.WriteLine("UNKNOWN choice type: " + GameState.PendingChoice!.ChoiceFollowUp);
                            break;
                    }
                    break;
                // Complete treasury seems to be a patron choice, so not sure that the complete treasury enum value is for
                case ScriptsOfTribute.Board.CardAction.BoardState.PATRON_CHOICE_PENDING:
                    if (CardsPlayedRanked == null) // In SoT, the destroy also allows to destroy from hand, but to assist bot, i exclude this, cause its almost best to play the card first
                    {
                        CardsPlayedRanked = Utility.RankCardsInGameState(GameState, GameState.CurrentPlayer.Played);
                    }
                    // Treasury is just a single destroy
                    var bottumPlayedCards = CardsPlayedRanked.TakeLast(Bot.Settings.CHOICE_BRANCH_LIMIT!.Value - 1);
                    PossibleMoves = PossibleMoves.Where(m =>
                    bottumPlayedCards.Any(c => (m as MakeChoiceMoveUniqueCard).Choices[0].CommonId == c.CommonId)
                    || (m as MakeChoiceMoveUniqueCard).Choices.Count == 0)
                    .ToList();
                    break;
            }
        }
        PossibleMoves = Utility.RemoveDuplicateMoves(PossibleMoves);
    }

    private void SetBewildermentGoldChoiceMoves(IEnumerable<UniqueCard> cardPool)
    {
        int maxAmount = PossibleMoves.Max(m => (m as MakeChoiceMoveUniqueCard).Choices.Count);
        var bewilderments = cardPool.Count(c => c.CommonId == CardId.BEWILDERMENT);
        if (bewilderments > 0)
        {
            if (bewilderments >= maxAmount)
            {
                PossibleMoves.RemoveAll(m => !(m as MakeChoiceMoveUniqueCard).Choices.All(m => m.CommonId == CardId.BEWILDERMENT));
            }
            else
            {
                PossibleMoves.RemoveAll(m => !(m as MakeChoiceMoveUniqueCard).Choices.Any(m => m.CommonId == CardId.BEWILDERMENT));
            }
        }

        int remainingAmount = maxAmount - bewilderments;

        if (remainingAmount > 0)
        {
            var gold = cardPool.Count(c => c.CommonId == CardId.GOLD);
            if (gold > 0)
            {
                PossibleMoves.RemoveAll(m => !(m as MakeChoiceMoveUniqueCard).Choices.Any(c => c.CommonId == CardId.GOLD));
            }
        }
    }
}
