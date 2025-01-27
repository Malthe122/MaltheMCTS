using System.Diagnostics;
using ScriptsOfTribute;
using ScriptsOfTribute.AI;
using ScriptsOfTribute.Board;
using ScriptsOfTribute.Serializers;

namespace MaltheMCTS;

public class MaltheMCTS : AI
{
    private Node? rootNode;
    public Dictionary<int, List<Node>> NodeGameStateHashMap = new Dictionary<int, List<Node>>();
    public MCTSHyperparameters? Params { get; set; }

    public override void PregamePrepare()
    {
        if (Params == null)
        {
            Params = new MCTSHyperparameters();
        }
        // Console.WriteLine(Params);
        base.PregamePrepare();
        rootNode = null;
        NodeGameStateHashMap = new Dictionary<int, List<Node>>();
    }

    public override void GameEnd(EndGameState state, FullGameState? finalBoardState)
    {
        Console.WriteLine("@@@ Game ended because of " + state.Reason + " @@@");
        Console.WriteLine("@@@ Winner was " + state.Winner + " @@@");

        if (state.Reason == GameEndReason.INCORRECT_MOVE) {
            string errorMessage = state.Winner == PlayerEnum.PLAYER1 ? PlayerEnum.PLAYER2.ToString() : PlayerEnum.PLAYER1.ToString() + " played illegal move\n";
                errorMessage += "Environment was:\n";
                errorMessage += "ITERATION_COMPLETION_MILLISECONDS_BUFFER: " + Params.ITERATION_COMPLETION_MILLISECONDS_BUFFER + "\n";
                errorMessage += "UCT_EXPLORATION_CONSTANT: " + Params.UCT_EXPLORATION_CONSTANT + "\n";
                errorMessage += "FORCE_DELAY_TURN_END_IN_ROLLOUT: " + Params.FORCE_DELAY_TURN_END_IN_ROLLOUT + "\n";
                errorMessage += "INCLUDE_PLAY_MOVE_CHANCE_NODES: " + Params.INCLUDE_PLAY_MOVE_CHANCE_NODES + "\n";
                errorMessage += "INCLUDE_END_TURN_CHANCE_NODES: " + Params.INCLUDE_END_TURN_CHANCE_NODES + "\n";
                errorMessage += "CHOSEN_SCORING_METHOD: " + Params.CHOSEN_SCORING_METHOD + "\n";
                errorMessage += "ROLLOUT_TURNS_BEFORE_HEURSISTIC: " + Params.ROLLOUT_TURNS_BEFORE_HEURSISTIC + "\n";
                errorMessage += "EQUAL_CHANCE_NODE_DISTRIBUTION: " + Params.EQUAL_CHANCE_NODE_DISTRIBUTION + "\n";
                errorMessage += "REUSE_TREE: " + Params.REUSE_TREE + "\n";
                errorMessage += "Additional context:\n" + state.AdditionalContext; 

                SaveErrorLog(errorMessage);
        }
    }

    public override Move Play(GameState gameState, List<Move> possibleMoves, TimeSpan remainingTime)
    {
        try
        {
            var obviousMove = FindObviousMove(possibleMoves);
            if (obviousMove != null)
            {
                return obviousMove;
            }

            if (possibleMoves.Count == 1)
            {
                return possibleMoves[0];
            }

            ulong randomSeed = (ulong)Utility.Rng.Next();
            var seededGameState = gameState.ToSeededGameState(randomSeed);

            var rootNode = Utility.FindOrBuildNode(seededGameState, null, possibleMoves, this);

            if (Params.ITERATIONS > 0)
            {
                for (int i = 0; i < Params.ITERATIONS; i++)
                {
                    rootNode.Visit(out double score, new HashSet<Node>());
                }
            }
            else
            {
                var moveTimer = new Stopwatch();
                moveTimer.Start();
                int estimatedRemainingMovesInTurn = EstimateRemainingMovesInTurn(gameState, possibleMoves);
                double millisecondsForMove = (remainingTime.TotalMilliseconds / estimatedRemainingMovesInTurn) - Params.ITERATION_COMPLETION_MILLISECONDS_BUFFER;
                while (moveTimer.ElapsedMilliseconds < millisecondsForMove)
                {
                    // var iterationTimer = new Stopwatch();
                    // iterationTimer.Start();
                    // iterationCounter++;
                    rootNode.Visit(out double score, new HashSet<Node>());
                    // iterationTimer.Stop();
                    // Console.WriteLine("Iteration took: " + iterationTimer.ElapsedMilliseconds + " milliseconds");
                }
            }

            if (rootNode.MoveToChildNode.Count == 0)
            {
                // Console.WriteLine("NO TIME FOR CALCULATING MOVE@@@@@@@@@@@@@@@");
                return possibleMoves[0];
            }

            var bestMove = rootNode.MoveToChildNode
                .OrderByDescending(moveNodePair => (moveNodePair.Value.TotalScore / moveNodePair.Value.VisitCount))
                .FirstOrDefault()
                .Key;

            if (!CheckMoveLegality(bestMove.Move, rootNode, gameState, possibleMoves)) {
                string errorMessage = "Tried to play illegal move\n";
                errorMessage += "Environment was:\n";
                errorMessage += "ITERATION_COMPLETION_MILLISECONDS_BUFFER: " + Params.ITERATION_COMPLETION_MILLISECONDS_BUFFER + "\n";
                errorMessage += "UCT_EXPLORATION_CONSTANT: " + Params.UCT_EXPLORATION_CONSTANT + "\n";
                errorMessage += "FORCE_DELAY_TURN_END_IN_ROLLOUT: " + Params.FORCE_DELAY_TURN_END_IN_ROLLOUT + "\n";
                errorMessage += "INCLUDE_PLAY_MOVE_CHANCE_NODES: " + Params.INCLUDE_PLAY_MOVE_CHANCE_NODES + "\n";
                errorMessage += "INCLUDE_END_TURN_CHANCE_NODES: " + Params.INCLUDE_END_TURN_CHANCE_NODES + "\n";
                errorMessage += "CHOSEN_SCORING_METHOD: " + Params.CHOSEN_SCORING_METHOD + "\n";
                errorMessage += "ROLLOUT_TURNS_BEFORE_HEURSISTIC: " + Params.ROLLOUT_TURNS_BEFORE_HEURSISTIC + "\n";
                errorMessage += "EQUAL_CHANCE_NODE_DISTRIBUTION: " + Params.EQUAL_CHANCE_NODE_DISTRIBUTION + "\n";
                errorMessage += "REUSE_TREE: " + Params.REUSE_TREE + "\n";

                SaveErrorLog(errorMessage);
            }

            return Utility.FindOfficialMove(bestMove.Move, possibleMoves);
        }
        catch (Exception e)
        {
            Console.WriteLine("Something went wrong while trying to compute move. Playing random move instead. Exception:");
            Console.WriteLine("Message: " + e.Message);
            Console.WriteLine("Stacktrace: " + e.StackTrace);
            Console.WriteLine("Data: " + e.Data);
            if (e.InnerException != null)
            {
                Console.WriteLine("Inner excpetion: " + e.InnerException.Message);
                Console.WriteLine("Inner stacktrace: " + e.InnerException.StackTrace);
            }

            var errorMessage = "Something went wrong while trying to compute move. Playing random move instead. Exception:" + "\n" ;
            errorMessage += "Message: " + e.Message + "\n";
            errorMessage += "Stacktrace: " + e.StackTrace + "\n";
            errorMessage += "Data: " + e.Data + "\n";
            if (e.InnerException != null)
            {
                errorMessage += "Inner excpetion: " + e.InnerException.Message + "\n";
                errorMessage += "Inner stacktrace: " + e.InnerException.StackTrace + "\n";
            }

            errorMessage += "Environment was:\n";
                errorMessage += "ITERATION_COMPLETION_MILLISECONDS_BUFFER: " + Params.ITERATION_COMPLETION_MILLISECONDS_BUFFER + "\n";
                errorMessage += "UCT_EXPLORATION_CONSTANT: " + Params.UCT_EXPLORATION_CONSTANT + "\n";
                errorMessage += "FORCE_DELAY_TURN_END_IN_ROLLOUT: " + Params.FORCE_DELAY_TURN_END_IN_ROLLOUT + "\n";
                errorMessage += "INCLUDE_PLAY_MOVE_CHANCE_NODES: " + Params.INCLUDE_PLAY_MOVE_CHANCE_NODES + "\n";
                errorMessage += "INCLUDE_END_TURN_CHANCE_NODES: " + Params.INCLUDE_END_TURN_CHANCE_NODES + "\n";
                errorMessage += "CHOSEN_SCORING_METHOD: " + Params.CHOSEN_SCORING_METHOD + "\n";
                errorMessage += "ROLLOUT_TURNS_BEFORE_HEURSISTIC: " + Params.ROLLOUT_TURNS_BEFORE_HEURSISTIC + "\n";
                errorMessage += "EQUAL_CHANCE_NODE_DISTRIBUTION: " + Params.EQUAL_CHANCE_NODE_DISTRIBUTION + "\n";
                errorMessage += "REUSE_TREE: " + Params.REUSE_TREE + "\n";

            SaveErrorLog(errorMessage);
            return possibleMoves[0];
        }
    }

    private void SaveErrorLog(string errorMessage)
    {
        var filePath = "Error.txt";

        string directoryPath = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        using (var writer = new StreamWriter(filePath, true))
        {
            writer.Write("\n");
            writer.Write(errorMessage);
        }
    }

    private bool CheckMoveLegality(Move moveToCheck, Node rootNode, GameState officialGameState, List<Move> officialPossiblemoves)
    {
        if (!officialPossiblemoves.Any(move => move.IsIdentical(moveToCheck)))
        {
            Console.WriteLine("----- ABOUT TO PERFORM ILLEGAL MOVE -----");
            Console.WriteLine("Our state:");
            rootNode?.GameState.Log();
            Console.WriteLine("Actual state:");
            officialGameState.ToSeededGameState((ulong)Utility.Rng.Next()).Log();
            Console.WriteLine("@@@@ Trying to play move:");
            moveToCheck.Log();
            Console.WriteLine("@@@@@@@ But available moves were:");
            officialPossiblemoves.ForEach(m => m.Log());
            Console.WriteLine("@@@@@@ But we thought moves were:");
            rootNode.PossibleMoves.ForEach(m => m.Move.Log());

            return false;
        }

        return true;
    }

    private int EstimateRemainingMovesInTurn(GameState inputState, List<Move> inputPossibleMoves)
    {
        return EstimateRemainingMovesInTurn(inputState.ToSeededGameState((ulong)Utility.Rng.Next()), inputPossibleMoves);
    }

    private int EstimateRemainingMovesInTurn(SeededGameState inputState, List<Move> inputPossibleMoves)
    {

        var possibleMoves = new List<Move>(inputPossibleMoves);

        if (possibleMoves.Count == 1 && possibleMoves[0].Command == CommandEnum.END_TURN)
        {
            return 0;
        }

        possibleMoves.RemoveAll(x => x.Command == CommandEnum.END_TURN);

        int result = 1;
        SeededGameState currentState = inputState;
        List<Move> currentPossibleMoves = possibleMoves;

        while (currentPossibleMoves.Count > 0)
        {

            var obviousMove = FindObviousMove(currentPossibleMoves);
            if (obviousMove != null)
            {
                (currentState, currentPossibleMoves) = currentState.ApplyMove(obviousMove);
            }
            else if (currentPossibleMoves.Count == 1)
            {
                // TODO add this to ovious moves instead
                // we already checked that its not end turn, so this is make choice in cases where there is only one choice
                (currentState, currentPossibleMoves) = currentState.ApplyMove(currentPossibleMoves[0]);
            }
            else
            {
                result++;
                (currentState, currentPossibleMoves) = currentState.ApplyMove(currentPossibleMoves[0]);
            }

            currentPossibleMoves.RemoveAll(x => x.Command == CommandEnum.END_TURN);
        }

        return result;
    }

    private Move FindObviousMove(List<Move> possibleMoves)
    {
        foreach (Move currMove in possibleMoves)
        {
            if (currMove.Command == CommandEnum.PLAY_CARD)
            {
                if (Utility.OBVIOUS_ACTION_PLAYS.Contains(((SimpleCardMove)currMove).Card.CommonId))
                {
                    return currMove;
                }
            }
            else if (currMove.Command == CommandEnum.ACTIVATE_AGENT)
            {
                if (Utility.OBVIOUS_AGENT_EFFECTS.Contains(((SimpleCardMove)currMove).Card.CommonId))
                {
                    return currMove;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Used for logging when debugging. Do not delete even though it has no references
    /// </summary>
    private double GetTimeSpentBeforeTurn(TimeSpan remainingTime)
    {
        return 10_000d - remainingTime.TotalMilliseconds;
    }
    public override PatronId SelectPatron(List<PatronId> availablePatrons, int round)
    {
        return availablePatrons[0];
    }
}
