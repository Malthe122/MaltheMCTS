using Bots;
using ScriptsOfTribute;
using ScriptsOfTribute.Board.Cards;
using ScriptsOfTribute.Serializers;
using SimpleBots.src.MaltheMCTS.Utility.HeuristicScoring;
using System.Linq;

namespace MaltheMCTS;

public class Node
{
    public Dictionary<Move, Edge> MoveToChildNode;
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
        PossibleMoves = Utility.RemoveDuplicateMoves(possibleMoves, gameState);
        Bot = bot;
        if (bot.Settings.MANUAL_FILTERING)
        {
            FilterMoves();
        }
        MoveToChildNode = new Dictionary<Move, Edge>();
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
                score = Score();
            }
            else if (PossibleMoves.Count > MoveToChildNode.Count)
            {
                if (Bot.Settings.MANUAL_FILTERING)
                {
                    var expandedEdge = Expand();
                    score = expandedEdge.Visit(visitedNodes);
                }
                else
                {
                    var childrenCandidates = new Dictionary<Move, Edge>();
                    foreach (var move in PossibleMoves)
                    {
                        Edge newEdge;
                        if (!Bot.Settings.SIMULATE_MULTIPLE_TURNS && move.Command == CommandEnum.END_TURN)
                        {
                            var newEndNode = new EndNode(GameState, PossibleMoves, Bot);
                            newEdge = new Edge(newEndNode, 0);
                        }
                        else
                        {
                            (var newState, var newMoves) = GameState.ApplyMove(move);
                            var newNode = Utility.FindOrBuildNode(newState, this, newMoves, Bot);
                            newEdge = new Edge(newNode, 0);
                        }

                        newEdge.Visit(visitedNodes);
                        childrenCandidates.Add(move, newEdge);
                    }

                    var newChildren = childrenCandidates.OrderByDescending(n => n.Value.Child.TotalScore / n.Value.Child.VisitCount)
                    .Take(Bot.Settings.BRANCH_LIMIT!.Value)
                    .ToList();

                    var totalChildrenScore = newChildren.Sum(c => c.Value.Child.TotalScore / c.Value.Child.VisitCount);
                    score = totalChildrenScore / newChildren.Count;
                }
            }
            else
            {
                var selectedEdge = Select();
                selectedEdge.Child.Visit(out score, visitedNodes);
                selectedEdge.VisitCount++;

                if (selectedEdge.Child.GameState.CurrentPlayer.PlayerID != playerId)
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


    internal Edge Expand()
    {
        // TODO refactor this to use my new idea

        //foreach (var currMove in PossibleMoves)
        //{
        //    Node newChild = null;

        //    if (!MoveToChildNode.Keys.Any(m => m.IsIdentical(currMove, GameState)))
        //    {
        //        if (!Bot.Settings.SIMULATE_MULTIPLE_TURNS && currMove.Command == CommandEnum.END_TURN)
        //        {
        //            newChild = new EndNode(GameState, PossibleMoves, Bot);
        //        }
        //        else if ((Bot.Settings.INCLUDE_PLAY_MOVE_CHANCE_NODES && currMove.IsStochastic(GameState))
        //            || Bot.Settings.INCLUDE_END_TURN_CHANCE_NODES && currMove.Command == CommandEnum.END_TURN)
        //        {
        //            newChild = new ChanceNode(GameState, this, currMove, Bot);
        //        }
        //        else
        //        {
        //            ulong randomSeed = (ulong)Utility.Rng.Next();
        //            var (newGameState, newPossibleMoves) = GameState.ApplyMove(currMove, randomSeed);
        //            newChild = Utility.FindOrBuildNode(newGameState, this, newPossibleMoves, Bot);
        //        }

        //        if (newChild != null &&
        //        !Bot.Settings.SIMULATE_MULTIPLE_TURNS &&
        //        newChild.PossibleMoves.Count == 1 &&
        //        newChild.PossibleMoves[0].Command == CommandEnum.END_TURN)
        //        {
        //            newChild = new EndNode(GameState, PossibleMoves, Bot);
        //        }

        //        var newEdge = new Edge(newChild, 0);
        //        MoveToChildNode.Add(currMove, newEdge);
        //        return newEdge;
        //    }
        //}

        throw new Exception("Expand was unexpectedly called on a node that was fully expanded");
    }

    internal double Score()
    {
        switch (Bot.Settings.CHOSEN_SCORING_METHOD)
        {
            case ScoringMethod.Rollout:
                return Rollout();
            case ScoringMethod.ManualModelScoring:
                return RulebasedModel.Score(GameState, Bot.RulebasedModelSettings);
            case ScoringMethod.LightGbmScoring:
                // Trying without this and with more features instead to represent changes during turn
                //var gameState = RollOutTillEndOfTurn(); 
                return HeuristicScoring.Score(GameState, Bot.PredictionEngine);
            default:
                throw new NotImplementedException("Tried to applied non-implemented scoring method: " + Bot.Settings.CHOSEN_SCORING_METHOD);
        }
    }

    private SeededGameState RollOutTillEndOfTurn()
    {
        var rolloutPossibleMoves = PossibleMoves.ToList();
        var gameState = GameState;

        while (gameState.GameEndState != null && (rolloutPossibleMoves.Count > 1 || rolloutPossibleMoves[0].Command != CommandEnum.END_TURN))
        {
            if (Bot.Settings.FORCE_DELAY_TURN_END_IN_ROLLOUT)
            {
                rolloutPossibleMoves.RemoveAll(m => m.Command == CommandEnum.END_TURN);
            }

            var chosenIndex = Utility.Rng.Next(rolloutPossibleMoves.Count);
            var randomMove = rolloutPossibleMoves[chosenIndex];

            if (randomMove.Command == CommandEnum.END_TURN)
            {
                return gameState;
            }
            else
            {
                (gameState, rolloutPossibleMoves) = gameState.ApplyMove(randomMove);
            }
        }

        return gameState;
    }

    internal double Rollout()
    {
        double result = 0;
        var rolloutGameState = GameState;
        var rolloutPlayerId = rolloutGameState.CurrentPlayer.PlayerID;
        var rolloutPossibleMoves = new List<Move>(PossibleMoves);

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
            rolloutPossibleMoves = Utility.RemoveDuplicateMoves(newPossibleMoves, GameState);
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

    internal virtual Edge Select()
    {
        double maxConfidence = -double.MaxValue;
        var highestConfidenceChild = MoveToChildNode.First().Value;

        foreach (var childEdge in MoveToChildNode.Values)
        {
            double confidence = GetConfidenceScore(childEdge);
            if (confidence > maxConfidence)
            {
                maxConfidence = confidence;
                highestConfidenceChild = childEdge;
            }
        }

        return highestConfidenceChild;
    }

    public double GetConfidenceScore(Edge edge)
    {
        switch (Bot.Settings.CHOSEN_SELECTION_METHOD)
        {
            case SelectionMethod.UCT:
                if (Bot.Settings.UPDATED_TREE_REUSE)
                {
                    var simulatedTotalScore = (edge.Child.TotalScore / edge.Child.VisitCount) * edge.VisitCount;
                    double exploitation = simulatedTotalScore / edge.VisitCount;
                    double exploration = Bot.Settings.UCT_EXPLORATION_CONSTANT * Math.Sqrt(Math.Log(VisitCount) / edge.VisitCount);
                    return exploitation + exploration;
                }
                else
                {
                    double exploitation = edge.Child.TotalScore / edge.Child.VisitCount;
                    double exploration = Bot.Settings.UCT_EXPLORATION_CONSTANT * Math.Sqrt(Math.Log(VisitCount) / edge.Child.VisitCount);
                    return exploitation + exploration;
                }
            case SelectionMethod.Custom:
                return TotalScore - VisitCount;
            default:
                return 0;
        }
    }

    private void FilterMoves()
    {
        PossibleMoves = Utility.RemoveDuplicateMoves(PossibleMoves, GameState);
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

    public class Edge
    {
        public Node Child;
        public int VisitCount;

        public Edge(Node child, int visitCount)
        {
            Child = child;
            VisitCount = visitCount;
        }

        public double Visit(HashSet<Node> visitedNodes)
        {
            VisitCount++;
            double score;
            Child.Visit(out score, visitedNodes);
            return score;
        }
    }
}

//List<Node> newChildren = new List<Node>();
//                foreach(var move in PossibleMoves)
//                {
//                    (var newState, var newMoves) = GameState.ApplyMove(move);
//                    var newNode = new Node(newState, newMoves, Bot);
//newNode.Visit(out _, visitedNodes);
//                    newChildren.Add(newNode);
//                }