using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Diagnostics;
using Bots;
using ScriptsOfTribute.AI;

public static class Utility
{

    public static List<(AI, AI)> BuildMatchups(List<string> bots)
    {
        List<(AI, AI)> matchups = new List<(AI, AI)>();

        for (int i = 0; i < bots.Count; i++)
        {
            for (int j = i + 1; j < bots.Count; j++)
            {
                matchups.Add((CreateBot(bots[i]), CreateBot(bots[j])));
            }
        }

        return matchups;
    }

    private static AI CreateBot(string botName)
    {
        switch (botName)
        {
            case "AlwaysFirstOptionBot":
                return new AlwaysFirstOptionBot();
            case "BeamSearchBot":
                return new BeamSearchBot();
            case "DecisionTreeBot":
                return new DecisionTreeBot();
            case "MaxAgentsBot":
                return new MaxAgentsBot();
            case "MaxPrestigeBot":
                return new MaxPrestigeBot();
            case "MCTSBot":
                return new MCTSBot();
            case "MaltheMCTS":
                return new MaltheMCTS.MaltheMCTS();
            case "PatronFavorsBot":
                return new PatronFavorsBot();
            case "PatronSelectionTimeoutBot":
                return new PatronSelectionTimeoutBot();
            case "RandomBot":
                return new RandomBot();
            case "RandomBotWithRandomStateExploring":
                return new RandomBotWithRandomStateExploring();
            case "RandomSimulationBot":
                return new RandomSimulationBot();
            case "RandomWithoutEndTurnBot":
                return new RandomWithoutEndTurnBot();
            case "TurnTimeoutBot":
                return new TurnTimeoutBot();
            default:
                throw new ArgumentException($"Bot '{botName}' is not recognized.");
                // TODO add tournament bots
        }
    }
}