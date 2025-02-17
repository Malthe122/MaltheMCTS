using Microsoft.Extensions.Configuration;

namespace MaltheMCTS;

public class Settings
{
    public double ITERATION_COMPLETION_MILLISECONDS_BUFFER { get; set; }
    public double UCT_EXPLORATION_CONSTANT { get; set; }
    public bool FORCE_DELAY_TURN_END_IN_ROLLOUT { get; set; }
    public bool INCLUDE_PLAY_MOVE_CHANCE_NODES { get; set; }
    public bool INCLUDE_END_TURN_CHANCE_NODES { get; set; }
    public EvaluationMethod CHOSEN_EVALUATION_METHOD { get; set; }
    public ScoringMethod CHOSEN_SCORING_METHOD { get; set; }
    public int ROLLOUT_TURNS_BEFORE_HEURSISTIC { get; set; }
    public bool REUSE_TREE { get; set; }
    public bool SIMULATE_MULTIPLE_TURNS { get; set; }

    public Settings()
    {
        ITERATION_COMPLETION_MILLISECONDS_BUFFER = 500;
        UCT_EXPLORATION_CONSTANT = 1.41421356237; // sqrt(2) generally used default value
        FORCE_DELAY_TURN_END_IN_ROLLOUT = true;
        INCLUDE_PLAY_MOVE_CHANCE_NODES = false;
        INCLUDE_END_TURN_CHANCE_NODES = false;
        CHOSEN_EVALUATION_METHOD = EvaluationMethod.UCT;
        CHOSEN_SCORING_METHOD = ScoringMethod.Heuristic;
        ROLLOUT_TURNS_BEFORE_HEURSISTIC = 1;
        SIMULATE_MULTIPLE_TURNS = false;
        REUSE_TREE = true;
    }

    public override string ToString()
    {
        return @$"ITERATION_COMPLETION_MILLISECONDS_BUFFER={ITERATION_COMPLETION_MILLISECONDS_BUFFER}
                UCT_EXPLORATION_CONSTANT={UCT_EXPLORATION_CONSTANT}
                FORCE_DELAY_TURN_END_IN_ROLLOUT={FORCE_DELAY_TURN_END_IN_ROLLOUT}
                INCLUDE_PLAY_MOVE_CHANCE_NODES={INCLUDE_PLAY_MOVE_CHANCE_NODES}
                INCLUDE_END_TURN_CHANCE_NODES={INCLUDE_END_TURN_CHANCE_NODES}
                CHOSEN_EVALUATION_METHOD={CHOSEN_EVALUATION_METHOD}
                CHOSEN_SCORING_METHOD={CHOSEN_SCORING_METHOD}
                ROLLOUT_TURNS_BEFORE_HEURSISTIC={ROLLOUT_TURNS_BEFORE_HEURSISTIC}
                SIMULATE_MULTIPLE_TURNS={SIMULATE_MULTIPLE_TURNS}
                REUSE_TREE={REUSE_TREE}
                ";
    }

    public static Settings LoadSettingsFromFile(string filePath)
    {
        // TODO
        throw new NotImplementedException();
    }

    //public static Dictionary<string, string> LoadEnvFile(string filePath)
    //{
    //    if (!File.Exists(filePath))
    //    {
    //        Console.Error.WriteLine($"Env file '{filePath}' not found");
    //        return new Dictionary<string, string>();
    //    }

    //    var envVariables = new Dictionary<string, string>();

    //    foreach (var line in File.ReadAllLines(filePath))
    //    {
    //        if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#"))
    //            continue;

    //        var parts = line.Split('=', 2);
    //        if (parts.Length != 2)
    //            continue;

    //        var key = parts[0].Trim();
    //        var value = parts[1].Trim().Trim('"');

    //        envVariables[key] = value;
    //    }

    //    return envVariables;
    //}

}