﻿using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Diagnostics;
using Bots;
using ScriptsOfTribute.AI;
using Tensorflow;

public static class Utility
{

    public static List<(string, string)> BuildMatchups(List<string> bots, int amount, bool skipExternalMatches)
    {
        List<(string, string)> matchups = new();

        for (int i = 0; i < bots.Count; i++)
        {
            for (int j = i + 1; j < bots.Count; j++)
            {
                for(int k = 0; k < amount; k++)
                {
                    matchups.Add((bots[i], bots[j]));
                }
            }
        }

        if (skipExternalMatches)
        {
            matchups.RemoveAll(m => 
            {
                return m.Item1 != "MaltheMCTS" && m.Item2 != "MaltheMCTS";
            }
            );
        }

        return matchups;
    }

    public static List<(AI, AI)> BuildMatchups2(List<AI> bots, int amount, bool skipExternalMatches)
    {
        List<(AI, AI)> matchups = new();

        for (int i = 0; i < bots.Count; i++)
        {
            for (int j = i + 1; j < bots.Count; j++)
            {
                for (int k = 0; k < amount; k++)
                {
                    matchups.Add((bots[i], bots[j]));
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

    public static AI CreateBot(string botName)
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
                return new MaltheMCTS.MaltheMCTS(instanceName: Guid.NewGuid().ToString());
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
            case "BestMCTS3":
                return new BestMCTS3.BestMCTS3();
            case "HQL_BOT":
                return new HQL_BOT.HQL_BOT();
            case "Sakkiring":
                return new Sakkirin.Sakkirin();
            case "Aau903Bot:":
                return new Aau903Bot.Aau903Bot();
            case "SOISMCTS":
                return new SOISMCTS.SOISMCTS();
            case "ToT-BoT":
                throw new NotImplementedException("Benchmark does not include the feature of running python bots yet");
            default:
                throw new ArgumentException($"Bot '{botName}' is not recognized.");
        }
    }

    public static string MatchResultsToCsv(Dictionary<string, Dictionary<string, int>> results, int matchesPerMatchup)
    {
        var bots = results.Keys.ToList();
        var sb = new System.Text.StringBuilder();

        sb.Append("vs");
        foreach (var bot in bots)
        {
            sb.Append(";").Append(bot);
        }
        sb.Append(";Total Winrate %");
        sb.AppendLine();

        foreach (var bot1 in bots)
        {
            var totalMatches = 0;
            var totalWins = 0;

            sb.Append(bot1);
            foreach (var bot2 in bots)
            {
                if (bot2 != bot1)
                {
                    totalMatches += matchesPerMatchup;
                }

                if (results[bot1].TryGetValue(bot2, out int wins))
                {
                    double winRate = (double)wins / matchesPerMatchup * 100.0;
                    sb.Append(";").Append(winRate.ToString("F2"));
                    totalWins += wins;
                }
                else
                {
                    sb.Append(";0");
                }
            }
            var winrate = ((double)totalWins / totalMatches) * 100.0;
            sb.Append(";" + winrate);
            sb.AppendLine();
        }

        return sb.ToString();
    }

    public static string GetOverallWinRatesText(Dictionary<string, Dictionary<string, int>> data, int totalMatchesByBot)
    {
        var sb = new System.Text.StringBuilder();

        foreach (var bot in data.Keys)
        {
            int totalWins = data[bot].Values.Sum();
            double overallWinRate = ((double)totalWins / totalMatchesByBot) * 100.0;
            sb.AppendLine($"{bot}: {overallWinRate}");
        }

        return sb.ToString();
    }

    public static void AddWinToResultContainer(string winner, string looser, Dictionary<string, Dictionary<string, int>> container)
    {
        if (!container.ContainsKey(winner))
        {
            container.Add(winner, new Dictionary<string, int>());
        }

        if (!container[winner].ContainsKey(looser))
        {
            container[winner].Add(looser, 0);
        }

        container[winner][looser]++;
    }

    public static void AddWinToResultContainer(AI winner, AI looser, Dictionary<AI, Dictionary<AI, int>> container)
    {
        if (!container.ContainsKey(winner))
        {
            container.Add(winner, new Dictionary<AI, int>());
        }

        if (!container[winner].ContainsKey(looser))
        {
            container[winner].Add(looser, 0);
        }

        container[winner][looser]++;
    }
}