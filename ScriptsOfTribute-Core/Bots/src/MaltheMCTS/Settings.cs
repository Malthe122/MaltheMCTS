using Microsoft.Extensions.Configuration;

namespace MaltheMCTS;

public class MCTSHyperparameters
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

    public MCTSHyperparameters(string filePath = "MaltheMCTSSettings")
    {
        var config = new ConfigurationBuilder()
        .AddIniFile(filePath, optional: false, reloadOnChange: false)
        .Build();

        ITERATION_COMPLETION_MILLISECONDS_BUFFER = config.GetValue("ITERATION_COMPLETION_MILLISECONDS_BUFFER", 100.0);
        UCT_EXPLORATION_CONSTANT = config.GetValue("UCT_EXPLORATION_CONSTANT", 1.41421356237);
        FORCE_DELAY_TURN_END_IN_ROLLOUT = true;
        INCLUDE_PLAY_MOVE_CHANCE_NODES = config.GetValue("INCLUDE_PLAY_MOVE_CHANCE_NODES", false);
        INCLUDE_END_TURN_CHANCE_NODES = config.GetValue("INCLUDE_END_TURN_CHANCE_NODES", false);
        CHOSEN_EVALUATION_METHOD = Enum.Parse<EvaluationMethod>(config.GetValue("CHOSEN_EVALUATION_METHOD", "UCT")!);
        CHOSEN_SCORING_METHOD = Enum.Parse<ScoringMethod>(config.GetValue("CHOSEN_SCORING_METHOD", "Rollout")!);
        ROLLOUT_TURNS_BEFORE_HEURSISTIC = config.GetValue("ROLLOUT_TURNS_BEFORE_HEURSISTIC", 3);
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
                REUSE_TREE={REUSE_TREE}
                ";
    }
}