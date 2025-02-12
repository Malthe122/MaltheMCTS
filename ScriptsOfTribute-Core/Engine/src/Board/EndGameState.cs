namespace ScriptsOfTribute.Board;

public class EndGameState
{
    public readonly PlayerEnum Winner;
    public readonly GameEndReason Reason;
    public string AdditionalContext;
    public float AverageComputationsPerTurn;

    public int TurnsTaken { get; internal set; }

    public EndGameState(PlayerEnum winner, GameEndReason reason, int turnsTaken, string additionalContext = "")
    {
        Winner = winner;
        Reason = reason;
        AdditionalContext = additionalContext;
        TurnsTaken = turnsTaken;
    }

    public override string ToString()
    {
        return $"Winner: {Winner.ToString()}, reason: {Reason.ToString()}{(AdditionalContext == "" ? "" : $"\n{AdditionalContext}")}";
    }

    public string ToSimpleString()
    {
        return $"{Winner} {Reason} {AdditionalContext}";
    }
}
