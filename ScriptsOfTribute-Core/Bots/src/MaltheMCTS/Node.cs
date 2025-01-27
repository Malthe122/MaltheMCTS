using ScriptsOfTribute;
using ScriptsOfTribute.Serializers;

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

    public Node(SeededGameState gameState, List<Move> possibleMoves, MaltheMCTS bot)
    {
        GameState = gameState;
        PossibleMoves = Utility.GetUniqueMoves(possibleMoves);
        MoveToChildNode = new Dictionary<Move, Node>();
        ApplyAllDeterministicAndObviousMoves();
        Bot = bot;
    }

    public virtual void Visit(out double score, HashSet<Node> visitedNodes)
    {

        if (visitedNodes.Contains(this)) {
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
                ApplyAllDeterministicAndObviousMoves();
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
            if (!MoveToChildNode.Keys.Any(m => m.IsIdentical(currMove)))
            {
                if ((Bot.Params.INCLUDE_PLAY_MOVE_CHANCE_NODES && currMove.IsNonDeterministic())
                    || Bot.Params.INCLUDE_END_TURN_CHANCE_NODES && currMove.Command == CommandEnum.END_TURN)
                {
                    var newChild = new ChanceNode(GameState, this, currMove, Bot);
                    MoveToChildNode.Add(currMove, newChild);
                    return newChild;
                }
                else
                {
                    ulong randomSeed = (ulong)Utility.Rng.Next();
                    var (newGameState, newPossibleMoves) = GameState.ApplyMove(currMove, randomSeed);
                    var newChild = Utility.FindOrBuildNode(newGameState, this, newPossibleMoves, Bot);
                    MoveToChildNode.Add(currMove, newChild);
                    return newChild;
                }
            }
        }

        throw new Exception("Expand was unexpectedly called on a node that was fully expanded");
    }

    private double Score()
    {
        switch (Bot.Params.CHOSEN_SCORING_METHOD)
        {
            case ScoringMethod.Rollout:
                return Rollout();
            case ScoringMethod.Heuristic:
                return Utility.UseBestMCTS3Heuristic(GameState, false);
            case ScoringMethod.RolloutTurnsCompletionsThenHeuristic:
                return RolloutTillTurnsEndThenHeuristic(Bot.Params.ROLLOUT_TURNS_BEFORE_HEURSISTIC);
            default:
                throw new NotImplementedException("Tried to applied non-implemented scoring method: " + Bot.Params.CHOSEN_SCORING_METHOD);
        }
    }

    private double RolloutTillTurnsEndThenHeuristic(int turnsToComplete)
    {
        int rolloutTurnsCompleted = 0;
        var rolloutPlayer = GameState.CurrentPlayer;
        var rolloutGameState = GameState;
        var rolloutPossibleMoves = PossibleMoves;

        while (rolloutTurnsCompleted < turnsToComplete && rolloutGameState.GameEndState == null)
        {
            if (Bot.Params.FORCE_DELAY_TURN_END_IN_ROLLOUT)
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

        for (int i = 0; i < Bot.Params.NUMBER_OF_ROLLOUTS; i++)
        {
            // TODO also apply the playing obvious moves in here, possibly
            while (rolloutGameState.GameEndState == null)
            {
                if (Bot.Params.FORCE_DELAY_TURN_END_IN_ROLLOUT)
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
                rolloutPossibleMoves = Utility.GetUniqueMoves(newPossibleMoves);
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
        switch (Bot.Params.CHOSEN_EVALUATION_METHOD)
        {
            case EvaluationMethod.UCT:
                double exploitation = TotalScore / VisitCount;
                double exploration = Bot.Params.UCT_EXPLORATION_CONSTANT * Math.Sqrt(Math.Log(parentVisitCount) / VisitCount);
                return exploitation + exploration;
            case EvaluationMethod.Custom:
                return TotalScore - VisitCount;
            default:
                return 0;
        }
    }

    internal void ApplyAllDeterministicAndObviousMoves()
    {
        foreach (var currMove in PossibleMoves)
        {
            if (currMove.Command == CommandEnum.PLAY_CARD)
            {
                if (Utility.OBVIOUS_ACTION_PLAYS.Contains(((SimpleCardMove)currMove).Card.CommonId))
                {
                    // TODO consider if some of the choice cards are also obvious moves, since the choice will be a new move
                    // or how to handle this issue
                    (GameState, var possibleMoves) = GameState.ApplyMove(currMove, (ulong)Utility.Rng.Next());
                    PossibleMoves = Utility.GetUniqueMoves(possibleMoves);
                    ApplyAllDeterministicAndObviousMoves();
                    break;
                }
            }
            else if (currMove.Command == CommandEnum.ACTIVATE_AGENT)
            {
                if (Utility.OBVIOUS_AGENT_EFFECTS.Contains(((SimpleCardMove)currMove).Card.CommonId))
                {
                    (GameState, var possibleMoves) = GameState.ApplyMove(currMove, (ulong)Utility.Rng.Next());
                    PossibleMoves = Utility.GetUniqueMoves(possibleMoves);
                    ApplyAllDeterministicAndObviousMoves();
                    break;
                }
            }
        }

        GameStateHash = GameState.GenerateHash();
    }
}
