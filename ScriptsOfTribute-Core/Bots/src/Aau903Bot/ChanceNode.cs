using System.Xml.Schema;
using ScriptsOfTribute;
using ScriptsOfTribute.Serializers;

namespace Aau903Bot;

public class ChanceNode : Node
{
    public Node Parent;
    public Move AppliedMove;
    private Dictionary<int, List<Node>> knownPossibleOutcomes;
    public ChanceNode(SeededGameState gameState, Node parent, Move appliedMove, Aau903Bot bot) : base(gameState, new List<Move>(), bot)
    {
        AppliedMove = appliedMove;
        knownPossibleOutcomes = new Dictionary<int, List<Node>>();
        Parent = parent;
    }

    public override void Visit(out double score, HashSet<Node> visitedNodes)
    {   
        (var newState, var newMoves) = Parent.GameState.ApplyMove(AppliedMove, (ulong)Utility.Rng.Next());

        var child = Utility.FindOrBuildNode(newState, this, newMoves, Bot);

        if (Bot.Params.EQUAL_CHANCE_NODE_DISTRIBUTION)
        {
            Node? existingChild = null;
            if (knownPossibleOutcomes.Keys.Contains(child.GameStateHash)){
                existingChild = knownPossibleOutcomes[child.GameStateHash].SingleOrDefault(node => node.GameState.IsIdentical(child.GameState));
                if (existingChild == null){
                    knownPossibleOutcomes[child.GameStateHash].Add(child);
                }
            }
            else {
                knownPossibleOutcomes.Add(child.GameStateHash, new List<Node>(){child});
            }
            if (existingChild != null) {

                var leastVisitedChild = existingChild;
                var lowestVisitCount = int.MaxValue;
                foreach (var currChild in knownPossibleOutcomes.Values.SelectMany(list => list).ToList())
                {
                    if (currChild.VisitCount < lowestVisitCount)
                    {
                        leastVisitedChild = currChild;
                        lowestVisitCount = currChild.VisitCount;
                    }
                }

                leastVisitedChild.Visit(out score, visitedNodes);
            }
            else
            {
                child.Visit(out score, visitedNodes);
            }
        }
        else
        {
            child.Visit(out score, visitedNodes);
        }

        TotalScore += score;
        VisitCount++;
    }
}
