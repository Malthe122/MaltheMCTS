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

    public static List<(AI, AI)> BuildMatchups(List<string> bots, int amount, bool skipExternalMatches)
    {
        List<(AI, AI)> matchups = new List<(AI, AI)>();

        for (int i = 0; i < bots.Count; i++)
        {
            for (int j = i + 1; j < bots.Count; j++)
            {
                for(int k = 0; k < amount; k++)
                {
                    matchups.Add((CreateBot(bots[i]), CreateBot(bots[j])));
                }
            }
        }

        if (skipExternalMatches)
        {
            matchups.RemoveAll(m => 
            {
                return !(m.Item1 is MaltheMCTS.MaltheMCTS) && !(m.Item2 is MaltheMCTS.MaltheMCTS);
            }
            );
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
                return new MaltheMCTS.MaltheMCTS(Guid.NewGuid().ToString());
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

    public static string MatchResultsToCsv(Dictionary<string, Dictionary<string, int>> results, int matchesPerMatchup)
    {
        var bots = results.Keys.ToList();
        var sb = new System.Text.StringBuilder();

        sb.Append("vs.");
        foreach (var bot in bots)
        {
            sb.Append(",").Append(bot);
        }
        sb.AppendLine();

        foreach (var bot1 in bots)
        {
            sb.Append(bot1);
            foreach (var bot2 in bots)
            {
                if (results[bot1].TryGetValue(bot2, out int wins))
                {
                    double winRate = (double)wins / matchesPerMatchup * 100.0;
                    sb.Append(",").Append(winRate.ToString("F2"));
                }
                else
                {
                    sb.Append(",0");
                }
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    public static string GetOverallWinRatesText(Dictionary<string, Dictionary<string, int>> data, int matches)
    {
        var sb = new System.Text.StringBuilder();

        foreach (var bot in data.Keys)
        {
            int totalWins = data[bot].Values.Sum();
            double overallWinRate = (double)totalWins / (data[bot].Count * matches) * 100.0;
            sb.AppendLine($"{bot}: {overallWinRate}");
        }

        return sb.ToString();
    }

    public static void AddWinToResultContainer(AI winner, AI looser, Dictionary<string, Dictionary<string, int>> container)
    {
        string winnerName = winner.GetType().Name;
        string looserName = looser.GetType().Name;

        if (!container.ContainsKey(winnerName))
        {
            container.Add(winnerName, new Dictionary<string, int>());
        }

        if (!container[winnerName].ContainsKey(looserName))
        {
            container[winnerName].Add(looserName, 0);
        }

        container[winnerName][looserName]++;
    }
}