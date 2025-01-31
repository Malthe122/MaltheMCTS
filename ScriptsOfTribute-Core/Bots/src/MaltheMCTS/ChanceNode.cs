using System.Xml.Schema;
using ScriptsOfTribute;
using ScriptsOfTribute.Serializers;

namespace MaltheMCTS;

public class ChanceNode : Node
{
    public Node Parent;
    public Move AppliedMove;
    private Dictionary<int, List<Node>> knownPossibleOutcomes;
    public ChanceNode(SeededGameState gameState, Node parent, Move appliedMove, MaltheMCTS bot) : base(gameState, new List<Move>(), bot)
    {
        AppliedMove = appliedMove;
        knownPossibleOutcomes = new Dictionary<int, List<Node>>();
        Parent = parent;
    }

    public override void Visit(out double score, HashSet<Node> visitedNodes)
    {   
        (var newState, var newMoves) = Parent.GameState.ApplyMove(AppliedMove, (ulong)Utility.Rng.Next());

        var child = Utility.FindOrBuildNode(newState, this, newMoves, Bot);
        child.Visit(out score, visitedNodes);

        TotalScore += score;
        VisitCount++;
    }
}
