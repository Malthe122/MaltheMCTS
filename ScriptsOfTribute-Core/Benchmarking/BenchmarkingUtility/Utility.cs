using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Diagnostics;
using Bots;
using ScriptsOfTribute.AI;
using Tensorflow;
using ScriptsOfTribute.Board;
using ScriptsOfTribute;
using BenchmarkingUtility;
using System.Text;

public static class Utility
{

    public static List<(AI, AI)> BuildMatchups(List<AI> bots, int amount, bool skipExternalMatches)
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

    // TODO refactor to use Result variant instead
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
    public static string MatchResultsToCsv(List<Result> results, int matchesPerMatchup)
    {
        var bots = new List<AI>();
        var sb = new System.Text.StringBuilder();
        var botToBotWinrates = new Dictionary<AI, Dictionary<AI, int>>();

        results.ForEach(r =>
        {
            if (!bots.Contains(r.Competitors.Item1))
            {
                bots.Add(r.Competitors.Item1);
            }
            if (!bots.Contains(r.Competitors.Item2))
            {
                bots.Add(r.Competitors.Item2);
            }
            AddWinToResultContainer(r.Winner, r.Looser, botToBotWinrates);
        });

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

                if (botToBotWinrates[bot1].TryGetValue(bot2, out int wins))
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

    private static List<AI> GetBotsFromResults(List<Result> results)
    {
        var bots = new List<AI>();
        results.ForEach(r =>
        {
            if (!bots.Contains(r.Competitors.Item1))
            {
                bots.Add(r.Competitors.Item1);
            }
            if (!bots.Contains(r.Competitors.Item2))
            {
                bots.Add(r.Competitors.Item2);
            }
        });

        return bots;
    }

    // TODO refactor to use Result variant instead
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
    public static string GetOverallWinRatesText(List<Result> results, int totalMatchesByBot)
    {
        var sb = new System.Text.StringBuilder();
        var bots = GetBotsFromResults(results);

        foreach (var bot in bots)
        {
            decimal totalWins = 0m;
            results.ForEach(r =>
            {
                if (r.Winner == bot)
                {
                    totalWins += 1;
                }
                else if (r.Winner == null)
                {
                    totalWins += 0.5m; // I give half a win for draws
                }
            });
            decimal overallWinRate = (totalWins / totalMatchesByBot) * 100m;
            sb.AppendLine($"{bot}: {overallWinRate}");
        }

        return sb.ToString();
    }
    public static string GetBotPatronWinRatesText(List<Result> results, int totalMatchesPerBot)
    {
        List<AI> bots = GetBotsFromResults(results);
        var sb = new System.Text.StringBuilder();

        foreach (var bot in bots)
        {
            var individualPatronWins = new Dictionary<PatronId, decimal>();
            var individualPatronTotal = new Dictionary<PatronId, int>();

            var patronCombinationWins = new Dictionary<string, decimal>();
            var patronCombinationTotal = new Dictionary<string, int>();

            foreach (var r in results)
            {
                bool isWinner = r.Winner.Equals(bot);
                bool isParticipant = r.Competitors.Item1.Equals(bot) || r.Competitors.Item2.Equals(bot);
                if (!isParticipant)
                    continue;

                foreach (var p in r.Patrons)
                {
                    if (!individualPatronTotal.ContainsKey(p))
                    {
                        individualPatronTotal[p] = 0;
                    }
                    if (!individualPatronWins.ContainsKey(p))
                    {
                        individualPatronWins[p] = 0;
                    }

                    individualPatronTotal[p]++;

                    if (isWinner)
                    {
                        individualPatronWins[p]++;
                    }
                    else if (r.Winner == null)
                    {
                        individualPatronWins[p] += 0.5m; //I give a half win for draws
                    }
                }

                var combinationKey = string.Join(",", r.Patrons.OrderBy(x => x.ToString()));
                if (!patronCombinationTotal.ContainsKey(combinationKey))
                {
                    patronCombinationTotal[combinationKey] = 0;
                }
                if (!patronCombinationWins.ContainsKey(combinationKey))
                {
                    patronCombinationWins[combinationKey] = 0;
                }
                patronCombinationTotal[combinationKey]++;

                if (isWinner)
                {
                    patronCombinationWins[combinationKey]++;
                }
                else if (r.Winner == null)
                {
                    patronCombinationWins[combinationKey] += 0.5m; //I give a half win for draws
                }
            }

            sb.AppendLine(bot.ToString() + ":");

            foreach (var p in individualPatronTotal.Keys.OrderBy(x => x.ToString()))
            {
                decimal winrate = individualPatronWins[p] / individualPatronTotal[p] * 100;
                sb.AppendLine($"{p}: {winrate:F0} %");
            }

            foreach (var combination in patronCombinationTotal.Keys.OrderBy(x => x))
            {
                decimal winrate = patronCombinationWins[combination] / patronCombinationTotal[combination] * 100;
                sb.AppendLine($"{combination}: {winrate:F0} %");
            }
        }

        return sb.ToString();
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