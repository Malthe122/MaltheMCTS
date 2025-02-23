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

    /// <summary>
    /// Only used when SimulateMultipleTurns is disabled. It is a copy of this node, but representing the current score/visits of the node if end_turn is played, but without
    /// affecting the state with the card draws that happens on end_turn, since with this feature disabled, we do not want this to be part of our simulations.
    /// </summary>
    private Node? endNode;

    public Node(SeededGameState gameState, List<Move> possibleMoves, MaltheMCTS bot)
    {
        GameState = gameState;
        PossibleMoves = Utility.GetUniqueMoves(possibleMoves);
        MoveToChildNode = new Dictionary<Move, Node>();
        ApplyInstantMoves();
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
                else if ((Bot.Settings.INCLUDE_PLAY_MOVE_CHANCE_NODES && currMove.IsNonDeterministic())
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
                return Utility.UseBestMCTS3Heuristic(GameState, false);
            case ScoringMethod.RolloutTurnsCompletionsThenHeuristic:
                return RolloutTillTurnsEndThenHeuristic(Bot.Settings.ROLLOUT_TURNS_BEFORE_HEURISTIC);
            case ScoringMethod.MaltheScoring:
                return HeuristicScoring.Score(GameState, true);
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
                PossibleMoves = Utility.GetUniqueMoves(possibleMoves);
                ApplyInstantMoves();
                break;
            }
        }

        GameStateHash = GameState.GenerateHash();
    }
}
