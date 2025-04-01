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
    private List<UniqueCard> CardsInHandAndPlayedRanked;

    /// <summary>
    /// Only used when SimulateMultipleTurns is disabled. It is a copy of this node, but representing the current score/visits of the node if end_turn is played, but without
    /// affecting the state with the card draws that happens on end_turn, since with this feature disabled, we do not want this to be part of our simulations.
    /// </summary>
    private Node? endNode;

    public Node(SeededGameState gameState, List<Move> possibleMoves, MaltheMCTS bot)
    {
        GameState = gameState;
        FilterMoves();
        MoveToChildNode = new Dictionary<Move, Node>();
        Bot = bot;
        ApplyInstantMoves();
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

        // TODO look at this one after the branchlimit one
        #region additionalFiltering
        //if (Bot.Settings.ADDITiONAL_SELECTION_MOVE_FILTERING)
        //{
        //    switch (GameState.BoardState)
        //    {
        //        case ScriptsOfTribute.Board.CardAction.BoardState.CHOICE_PENDING:
        //            switch (GameState.PendingChoice!.ChoiceFollowUp)
        //            {
        //                case ChoiceFollowUp.ENACT_CHOSEN_EFFECT:
        //                case ChoiceFollowUp.ACQUIRE_CARDS:
        //                    Console.WriteLine("WHAT IS THIS?");
        //                    break;
        //                case ChoiceFollowUp.DESTROY_CARDS:
        //                case ChoiceFollowUp.DISCARD_CARDS:
        //                    //int weakCardAmount = (uniqueMoves).Max(x => (x as MakeChoiceMove<UniqueCard>)!.Choices.Count);
        //                    //if (CardsInDrawPileRanked == null)
        //                    //{
        //                    //    RankCardsInHand();
        //                    //}
        //                    //PossibleMoves = FindWeakestHandCollection(availableMoves, weakCardAmount, branchLimit.Value);
        //                    //return;
        //                    break;
        //                case ChoiceFollowUp.REFRESH_CARDS: //Means moving cards from cooldown to top of drawpile
        //                                                   // TODO, consider if i should not use all refreshes if cooldown is weak
        //                                                   //int strongCardAmount = (uniqueMoves).Max(x => (x as MakeChoiceMove<UniqueCard>)!.Choices.Count);
        //                                                   //PossibleMoves = FindStrongestCardCollections(availableMoves, strongCardAmount, branchLimit.Value);
        //                    break;
        //                case ChoiceFollowUp.TOSS_CARDS:
        //                case ChoiceFollowUp.KNOCKOUT_AGENTS:
        //                case ChoiceFollowUp.COMPLETE_HLAALU:
        //                case ChoiceFollowUp.COMPLETE_PELLIN:
        //                case ChoiceFollowUp.COMPLETE_PSIJIC:
        //                case ChoiceFollowUp.COMPLETE_TREASURY:
        //                    Console.WriteLine("Unexpected branch limit exceeded");
        //                    break;
        //                case ChoiceFollowUp.REPLACE_CARDS_IN_TAVERN:
        //                    // Not sure here how to make some good logic. Instead, just makes some random moves available
        //                    var indexes = new HashSet<int>();
        //                    while (indexes.Count < branchLimit)
        //                    {
        //                        indexes.Add(Utility.Rng.Next(possibleMoves.Count));
        //                    }
        //                    availableMoves = indexes.Select(i => possibleMoves[i]).ToList();
        //                    break;
        //            }
        //            break;
        //        case ScriptsOfTribute.Board.CardAction.BoardState.NORMAL:
        //            Console.WriteLine("Here i probably do not want to limit");
        //            break;
        //        case ScriptsOfTribute.Board.CardAction.BoardState.START_OF_TURN_CHOICE_PENDING:
        //            switch (gameState.PendingChoice!.ChoiceFollowUp)
        //            {
        //                case ChoiceFollowUp.DISCARD_CARDS:
        //                    int weakCardAmount = (uniqueMoves).Max(x => (x as MakeChoiceMove<UniqueCard>)!.Choices.Count);
        //                    availableMoves = FindWeakestCardCollections(availableMoves, weakCardAmount);
        //                    break;
        //                default:
        //                    Console.WriteLine("UNKNOWN choice type: " + gameState.PendingChoice!.ChoiceFollowUp);
        //                    break;
        //            }
        //            break;
        //        case ScriptsOfTribute.Board.CardAction.BoardState.PATRON_CHOICE_PENDING:
        //            Console.WriteLine("UNEXPECTED");
        //            break;
        //    }
        //}
        #endregion

        if (Bot.Settings.STANDARD_BRANCH_LIMIT != null && PossibleMoves.Count > Bot.Settings.STANDARD_BRANCH_LIMIT)
        {
            Console.WriteLine("EXCEEDED BRANCH LIMIT");
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
                                CardsInTavernRanked = Utility.RankCardsInGameState(GameState, GameState.TavernAvailableCards.ToList());
                            }
                            // Aquire in this patch, always is a maximum of 1 card
                            var topTavernCards = CardsInTavernRanked.Take(Bot.Settings.STANDARD_BRANCH_LIMIT!.Value - 1);
                            PossibleMoves = PossibleMoves.Where(m =>
                                topTavernCards.Contains((m as MakeChoiceMoveUniqueCard).Choices[0])
                                || (m as MakeChoiceMoveUniqueCard).Choices.Count == 0)
                                .ToList();
                            break;
                        case ChoiceFollowUp.DESTROY_CARDS:
                            if (CardsPlayedRanked == null)
                            {
                                CardsPlayedRanked = Utility.RankCardsInGameState(GameState, GameState.CurrentPlayer.Played.ToList());
                            }
                            int maxAmount = PossibleMoves.Max(m => (m as MakeChoiceMoveUniqueCard).Choices.Count);
                            if (maxAmount == 1)
                            {
                                var bottumPlayedCards = CardsPlayedRanked.TakeLast(Bot.Settings.STANDARD_BRANCH_LIMIT!.Value - 1);
                                PossibleMoves = PossibleMoves.Where(m =>
                                bottumPlayedCards.Contains((m as MakeChoiceMoveUniqueCard).Choices[0])
                                || (m as MakeChoiceMoveUniqueCard).Choices.Count == 0)
                                .ToList();
                            }
                            else // Here the possible destroy amount is 2, since thats the max in the patch
                            {
                                switch (Bot.Settings.STANDARD_BRANCH_LIMIT!.Value)
                                {
                                    case 1:
                                    // Add the choice with the 2 worst cards
                                    case 2:
                                    // Add the choices with the 2 worst cards and with the worst card
                                    case 3:
                                    // Add the choices with the 2 worst cards and with the worst card and with no cards
                                    default: // 4 or more
                                        int singleChoiceMoveCount = Bot.Settings.STANDARD_BRANCH_LIMIT!.Value / 2;
                                        int twoChoicesMoveCount = Bot.Settings.STANDARD_BRANCH_LIMIT!.Value / 2;

                                        if (singleChoiceMoveCount + twoChoicesMoveCount == Bot.Settings.STANDARD_BRANCH_LIMIT) // I want the non-destroying move to be available
                                        {
                                            singleChoiceMoveCount -= 1; // Lowering this rather than twoChoice, since twoChoice is generally the best move
                                        }

                                        var bottumPlayedCards = CardsPlayedRanked.TakeLast(singleChoiceMoveCount);
                                        var singleChoiceMoves = PossibleMoves.Where(m =>
                                        (m as MakeChoiceMoveUniqueCard).Choices.Count == 1
                                        && bottumPlayedCards.Contains((m as MakeChoiceMoveUniqueCard).Choices[0]));
                                        // TODO find the logic for picking [0,1], [0,2] [1,2] etc. and Add the two choice moves here
                                        break;
                                }
                            }
                            break;
                        case ChoiceFollowUp.DISCARD_CARDS:
                            // Discard in this patch is always 1 card
                            if (CardsInHandRanked == null)
                            {
                                CardsInHandRanked = Utility.RankCardsInGameState(GameState, GameState.CurrentPlayer.Hand.ToList());
                            }
                            var bottumHandCards = CardsInHandRanked.TakeLast(Bot.Settings.STANDARD_BRANCH_LIMIT!.Value).ToList();
                            PossibleMoves = PossibleMoves.Where(m => bottumHandCards.Contains((m as MakeChoiceMoveUniqueCard).Choices[0])).ToList();
                            break;
                        case ChoiceFollowUp.REFRESH_CARDS: //Means moving cards from cooldown to top of drawpile
                                                           // TODO, same complicated logic here as in destroy cards
                                                           //int strongCardAmount = (uniqueMoves).Max(x => (x as MakeChoiceMove<UniqueCard>)!.Choices.Count);
                                                           //availableMoves = FindStrongestCardCollections(availableMoves, strongCardAmount, branchLimit);
                            break;
                        case ChoiceFollowUp.TOSS_CARDS:
                        // TODO same complicated logic here
                        case ChoiceFollowUp.KNOCKOUT_AGENTS: // the possible amount is always 2 (if opponent has 2 more agents) in this patch
                            // Theoretically it could make sense to leave agents up to stop opponent from playing even stronger agents, but i see this as purely theoretical and
                            // not something that actually happens in games, so to optimize the MCTS-search, i excludea ny moves that does not knockout the maximum amount of agents
                            int knockoutCount = Math.Min(GameState.EnemyPlayer.Agents.Count, 2);
                            PossibleMoves = PossibleMoves.Where(m => (m as MakeChoiceMoveUniqueCard).Choices.Count == knockoutCount).ToList();
                            // TODO same logic as for destroy cards
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
                            if (CardsInHandAndPlayedRanked == null)
                            {
                                var cards = new List<UniqueCard>();
                                cards.AddRange(GameState.CurrentPlayer.Hand.ToList());
                                cards.AddRange(GameState.CurrentPlayer.Played.ToList());
                                CardsInHandAndPlayedRanked = Utility.RankCardsInGameState(GameState, cards);
                            }
                            break;
                        case ChoiceFollowUp.REPLACE_CARDS_IN_TAVERN:
                            // Not sure here how to make some good logic. Instead, just makes some random moves available
                            var indexes = new HashSet<int>();
                            while (indexes.Count < Bot.Settings.STANDARD_BRANCH_LIMIT!.Value)
                            {
                                indexes.Add(Utility.Rng.Next(PossibleMoves.Count));
                            }
                            PossibleMoves = indexes.Select(i => PossibleMoves[i]).ToList();
                            break;
                    }
                    break;
                case ScriptsOfTribute.Board.CardAction.BoardState.NORMAL:
                    // Here i probably do not want to limit
                    Console.WriteLine("EXCEEDED BRANCH LIMIT AT NORMAL BOARDSTATE");
                    break;
                case ScriptsOfTribute.Board.CardAction.BoardState.START_OF_TURN_CHOICE_PENDING:
                    switch (GameState.PendingChoice!.ChoiceFollowUp)
                    {
                        case ChoiceFollowUp.DISCARD_CARDS:
                            // TODO add discard logic from above here too
                            break;
                        default:
                            Console.WriteLine("UNKNOWN choice type: " + GameState.PendingChoice!.ChoiceFollowUp);
                            break;
                    }
                    break;
                case ScriptsOfTribute.Board.CardAction.BoardState.PATRON_CHOICE_PENDING:
                    Console.WriteLine("UNEXPECTED BRANCH LIMIT EXCEEDED: PATRON CHOICE PENDING");
                    break;
            }
        }
    }
}
