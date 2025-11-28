using ScriptsOfTribute;
using ScriptsOfTribute.Board.Cards;
using ScriptsOfTribute.Serializers;

namespace MaltheMCTS;

public static class MiscellaneousExtensions{

    public static List<UniqueCard> GetCompleteDeck(this SerializedPlayer player)
    {
        var completeDeck = new List<UniqueCard>(player.Agents.Count + player.CooldownPile.Count + player.DrawPile.Count + player.Hand.Count + player.Played.Count);
        var agents = player.Agents.Select(x => x.RepresentingCard).ToList();
        completeDeck.AddRange(agents);
        completeDeck.AddRange(player.CooldownPile);
        completeDeck.AddRange(player.DrawPile);
        completeDeck.AddRange(player.Hand);
        completeDeck.AddRange(player.Played);

        // TODO remove this after debugging (to make sure that i dont count same cards multiple times if they appear in for example both played and cooldown or other
        var seen = new HashSet<UniqueCard>();
        foreach (var x in completeDeck)
        {
            if (!seen.Add(x))
            {
                {
                    throw new Exception($"Duplicate: {x}");
                }
            }
        }

        return completeDeck;
    }

    public static int GetPatronFavourCount(this SeededGameState gameState, PlayerEnum player)
    {
        int favourCount = 0;

        foreach (var patronState in  gameState.PatronStates.All)
        {
            if (patronState.Value == player)
            {
                favourCount++;
            }
        }

        return favourCount;
    }
}