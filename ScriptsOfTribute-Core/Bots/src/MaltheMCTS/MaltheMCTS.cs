using System.Diagnostics;
using BestMCTS3;
using Microsoft.ML;
using ScriptsOfTribute;
using ScriptsOfTribute.AI;
using ScriptsOfTribute.Board;
using ScriptsOfTribute.Serializers;
using SimpleBots.src.MaltheMCTS.Utility.HeuristicScoring;
using SimpleBots.src.MaltheMCTS.Utility.HeuristicScoring.ModelEvaluation;
using static SimpleBots.src.MaltheMCTS.Utility.HeuristicScoring.ModelEvaluation.EnsembledTreeModelEvaluation;

namespace MaltheMCTS;

public class MaltheMCTS : AI
{
    public Dictionary<int, List<Node>> NodeGameStateHashMap = new Dictionary<int, List<Node>>();
    public Settings Settings { get; set; }
    // Having this here only makes sense when competing MaltheMCTS aganist each other with different prediction Engines
    // Consider refactoring it back to Utility when submitting agent
    public PredictionEngine<GameStateFeatureSetCsvRow, ModelOutput> PredictionEngine;

    public string InstanceName;

    public MaltheMCTS(string? instanceName = null, Settings? settings = null) : base()
    {
        this.InstanceName = instanceName ?? "MaltheMCTS_" + Guid.NewGuid();
        Settings = settings ?? new Settings(); // Hardcoded
        PredictionEngine = EnsembledTreeModelEvaluation.GetPredictionEngine(Settings.FEATURE_SET_MODEL_TYPE);
    }

    // TODO initiate the static model class, so its not using time on the first turn
    /// <summary>
    /// Explicitly parameterless constructor is needed for SoT framework
    /// </summary>
    public MaltheMCTS() : base()
    {
        InstanceName = "MaltheMCTS_" + Guid.NewGuid();
        Settings = new Settings(); // Hardcoded
        PredictionEngine = EnsembledTreeModelEvaluation.GetPredictionEngine(Settings.FEATURE_SET_MODEL_TYPE);
    }

    public override void PregamePrepare()
    {
        Utility.CategorizeCards();
        NodeGameStateHashMap = new Dictionary<int, List<Node>>();
    }

    public override void GameEnd(EndGameState state, FullGameState? finalBoardState)
    {
        Console.WriteLine("@@@ Game ended because of " + state.Reason + " @@@");
        Console.WriteLine("@@@ Winner was " + state.Winner + " @@@");
        Console.WriteLine("Patrons were:");
        finalBoardState?.Patrons.ForEach(p => Console.WriteLine(p));
    }

    public override Move Play(GameState gameState, List<Move> possibleMoves, TimeSpan remainingTime)
    {
        try
        {
            if (Settings.APPLY_INSTANT_MOVES)
            {
                // TODO consider Ayleid Quartermaster (and other Knock Out All) before playing any agents
                // TODO consider removing instantplay as new effects such as knock out all, donate, etc. makes the logic to complicated to simplify 
                var instantPlay = Utility.FindInstantPlayMove(possibleMoves, gameState);
                if (instantPlay != null)
                {
                    return instantPlay;
                }
            }

            ulong randomSeed = (ulong)Utility.Rng.Next();
            var seededGameState = gameState.ToSeededGameState(randomSeed);

            var rootNode = Utility.FindOrBuildNode(seededGameState, null, possibleMoves, this);

            var moveTimer = new Stopwatch();
            moveTimer.Start();
            int estimatedRemainingMovesInTurn = EstimateRemainingMovesInTurn(seededGameState, possibleMoves);
            double millisecondsForMove = (remainingTime.TotalMilliseconds / estimatedRemainingMovesInTurn) - Settings.ITERATION_COMPLETION_MILLISECONDS_BUFFER;
            while (moveTimer.ElapsedMilliseconds < millisecondsForMove)
            {
                rootNode.Visit(out double score, new HashSet<Node>());
            }

            if (rootNode.MoveToChildNode.Count == 0)
            {
                var nonEndMove = possibleMoves.FirstOrDefault(m => m.Command != CommandEnum.END_TURN);
                if (nonEndMove != null)
                {
                    return nonEndMove;
                }
                return possibleMoves[0];
            }

            var bestMove = rootNode.MoveToChildNode
                .OrderByDescending(moveNodePair => (moveNodePair.Value.Child.TotalScore / moveNodePair.Value.VisitCount))
                .FirstOrDefault()
                .Key;

            return Utility.FindOfficialMove(bestMove, possibleMoves, seededGameState);
        }
        catch (Exception e)
        {
            LogError(e);
            return possibleMoves[0];
        }
    }

    private void LogError(Exception e)
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

            var instantPlay = Utility.FindInstantPlayMove(currentPossibleMoves, null); //TODO refactor to use seeded gamestate or insert gamestate here
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

    public override PatronId SelectPatron(List<PatronId> availablePatrons, int round)
    {
        return availablePatrons.PickRandom(new SeededRandom());
    }
}
