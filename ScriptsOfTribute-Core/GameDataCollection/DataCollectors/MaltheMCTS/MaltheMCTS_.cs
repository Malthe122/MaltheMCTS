using System.Diagnostics;
using BestMCTS3;
using GameDataCollection;
using MaltheMCTS;
using ScriptsOfTribute;
using ScriptsOfTribute.AI;
using ScriptsOfTribute.Board;
using ScriptsOfTribute.Serializers;
using SimpleBots.src.MaltheMCTS.Utility.HeuristicScoring;
using SimpleBots.src.MaltheMCTS.Utility.HeuristicScoring.ModelEvaluation;

namespace DataCollectors_MaltheMCTS;

public class MaltheMCTS_ : MaltheMCTS.MaltheMCTS
{
    public static double RANDOM_EXPLORATION_PROBABILITY = 0.1;

    public string InstanceName;

    public List<GameStateFeatureSet> GameStateFeatureSetsFromMatch;

    public MaltheMCTS_(string? instanceName = null, Settings? settings = null) : base()
    {
        this.InstanceName = instanceName ?? "MaltheMCTS_" + Guid.NewGuid();
        Settings = settings ?? new Settings(); // Hardcoded
    }

    /// <summary>
    /// Explicitly parameterless constructor is needed for SoT framework
    /// </summary>
    public MaltheMCTS_() : base()
    {
    }

    public override void PregamePrepare()
    {
        MaltheMCTS.Utility.CategorizeCards();
        NodeGameStateHashMap = new Dictionary<int, List<Node>>();
        EnsembledTreeModelEvaluation.LoadModel(Settings.FEATURE_SET_MODEL_TYPE);

        GameStateFeatureSetsFromMatch = new List<GameStateFeatureSet>();
    }

    private void AddFeatureSet(GameState gameState)
    {
        var featureSet = FeatureSetUtility.BuildFeatureSet(gameState.ToSeededGameState(0));
        GameStateFeatureSetsFromMatch.Add(featureSet);
    }

    public override void GameEnd(EndGameState state, FullGameState? finalBoardState)
    {
        Console.WriteLine("@@@ Game ended because of " + state.Reason + " @@@");
        Console.WriteLine("@@@ Winner was " + state.Winner + " @@@");

        foreach (var featureSet in GameStateFeatureSetsFromMatch)
        {
            Program.FeatureSetToResultRates.TryAdd(featureSet, new Program.ResultRates());
            if (state.Winner == Id)
            {
                Program.FeatureSetToResultRates[featureSet].Wins++;
            }
            else if (state.Winner == PlayerEnum.NO_PLAYER_SELECTED)
            {
                Program.FeatureSetToResultRates[featureSet].Draws++;
            }
            else
            {
                Program.FeatureSetToResultRates[featureSet].Looses++;
            }
        }
    }

    public override Move Play(GameState gameState, List<Move> possibleMoves, TimeSpan remainingTime)
    {
        try
        {
            var instantPlay = FindInstantPlayMove(possibleMoves);
            if (instantPlay != null)
            {
                if (instantPlay.Command == CommandEnum.END_TURN)
                {
                    AddFeatureSet(gameState);
                }
                return instantPlay;
            }

            // Exlusive for data collector to explore new strategies
            if (MaltheMCTS.Utility.Rng.NextDouble() < RANDOM_EXPLORATION_PROBABILITY)
            {
                return possibleMoves.PickRandom(new SeededRandom());
            }

            if (possibleMoves.Count == 1)
            {
                if (possibleMoves[0].Command == CommandEnum.END_TURN)
                {
                    AddFeatureSet(gameState);
                }
                return possibleMoves[0];
            }

            ulong randomSeed = (ulong)MaltheMCTS.Utility.Rng.Next();
            var seededGameState = gameState.ToSeededGameState(randomSeed);

            var rootNode = MaltheMCTS.Utility.FindOrBuildNode(seededGameState, null, possibleMoves, this);

            var moveTimer = new Stopwatch();
            moveTimer.Start();
            int estimatedRemainingMovesInTurn = EstimateRemainingMovesInTurn(gameState, possibleMoves);
            double millisecondsForMove = (remainingTime.TotalMilliseconds / estimatedRemainingMovesInTurn) - Settings.ITERATION_COMPLETION_MILLISECONDS_BUFFER;
            while (moveTimer.ElapsedMilliseconds < millisecondsForMove)
            {
                // var iterationTimer = new Stopwatch();
                // iterationTimer.Start();
                // iterationCounter++;
                rootNode.Visit(out double score, new HashSet<Node>());
                // iterationTimer.Stop();
                // Console.WriteLine("Iteration took: " + iterationTimer.ElapsedMilliseconds + " milliseconds");
            }

            if (rootNode.MoveToChildNode.Count == 0)
            {
                // Console.WriteLine("NO TIME FOR CALCULATING MOVE@@@@@@@@@@@@@@@");
                return possibleMoves[0];
            }

            var bestMove = rootNode.MoveToChildNode
                .OrderByDescending(moveNodePair => (moveNodePair.Value.Child.TotalScore / moveNodePair.Value.VisitCount))
                .FirstOrDefault()
                .Key;

            if (!CheckMoveLegality(bestMove, rootNode, gameState, possibleMoves)) {
                string errorMessage = "Tried to play illegal move\n";
                errorMessage += "Settings:\n" + Settings.ToString();
                SaveErrorLog(errorMessage);
            }

            var officialMove = MaltheMCTS.Utility.FindOfficialMove(bestMove, possibleMoves); ;

            if (officialMove.Command == CommandEnum.END_TURN)
            {
                AddFeatureSet(gameState);
            }

            return officialMove;
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

            var errorMessage = "Something went wrong while trying to compute move. Playing random move instead. Exception:" + "\n";
            errorMessage += "Message: " + e.Message + "\n";
            errorMessage += "Stacktrace: " + e.StackTrace + "\n";
            errorMessage += "Data: " + e.Data + "\n";
            if (e.InnerException != null)
            {
                errorMessage += "Inner excpetion: " + e.InnerException.Message + "\n";
                errorMessage += "Inner stacktrace: " + e.InnerException.StackTrace + "\n";
            }

            errorMessage += "Settings was:\n" + Settings.ToString();

            SaveErrorLog(errorMessage);
            return possibleMoves[0];
        }
    }

    private void SaveErrorLog(string errorMessage)
    {
        var filePath = InstanceName + "_Error.txt";

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
            officialGameState.ToSeededGameState((ulong)MaltheMCTS.Utility.Rng.Next()).Log();
            Console.WriteLine("@@@@ Trying to play move:");
            moveToCheck.Log();
            Console.WriteLine("@@@@@@@ But available moves were:");
            officialPossiblemoves.ForEach(m => m.Log());
            Console.WriteLine("@@@@@@ But we thought moves were:");
            rootNode.PossibleMoves.ForEach(m => m.Log());

            return false;
        }

        return true;
    }

    private int EstimateRemainingMovesInTurn(GameState inputState, List<Move> inputPossibleMoves)
    {
        return EstimateRemainingMovesInTurn(inputState.ToSeededGameState((ulong)MaltheMCTS.Utility.Rng.Next()), inputPossibleMoves);
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

            var instantPlay = FindInstantPlayMove(currentPossibleMoves);
            if (instantPlay != null)
            {
                (currentState, currentPossibleMoves) = currentState.ApplyMove(instantPlay);
            }
            else if (currentPossibleMoves.Count == 1)
            {
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

    private Move FindInstantPlayMove(List<Move> possibleMoves)
    {
        if (possibleMoves.Count == 1)
        {
            // This can be different than "END_TURN" in cases where a choice needs to be made (between agents for example)
            // while there is only one agent available.
            return possibleMoves[0];
        }

        foreach (Move currMove in possibleMoves)
        {
            if (currMove.IsInstantPlay()) {
                return currMove;
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
        return availablePatrons.PickRandom(new SeededRandom());
    }
}
