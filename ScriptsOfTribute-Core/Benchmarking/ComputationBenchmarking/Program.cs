using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Diagnostics;
using Bots;
using ScriptsOfTribute.AI;
using Docker.DotNet;
using System.Threading;
using BenchmarkingUtility;
using System.Collections.Concurrent;

namespace ComputationBenchmarking
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var botsOption = new Option<List<string>>(
                aliases: new[] { "--bots", "-b" },
                description: "List of bot names",
                parseArgument: result => result.Tokens.Select(t => t.Value).ToList()
            )
            {
                IsRequired = true,
                AllowMultipleArgumentsPerToken = true
            };

            var numberOfMatchupsOption = new Option<int>(
                aliases: new[] { "--number-of-matchups", "-n" },
                description: "Number of matchups between each bot (Round-Robin format)"
            )
            {
                IsRequired = true
            };

            var timeoutOption = new Option<int>(
                aliases: new[] { "--timeout", "-to" },
                description: "Timeout in seconds",
                getDefaultValue: () => 10
            );

            var threadsOption = new Option<int>(
                aliases: new[] { "--threads", "-t" },
                description: "Number of threads"
            )
            {
                IsRequired = true
            };

            var nameOption = new Option<string>(
                aliases: new[] { "--benchmark-name", "-bm" },
                description: "Name of benchmark"
            )
            {
                IsRequired = true
            };

            // Root command
            var rootCommand = new RootCommand
        {
            botsOption,
            numberOfMatchupsOption,
            timeoutOption,
            threadsOption,
            nameOption,
        };

            var arguments = rootCommand.Parse(args);

            rootCommand.SetHandler(
                BenchmarkComputation,
                botsOption, numberOfMatchupsOption, timeoutOption, threadsOption, nameOption
            );

            await rootCommand.InvokeAsync(args);
        }


        private static async Task BenchmarkComputation(List<string> bots, int numberOfMatchups, int timeout, int threads, string benchmarkName)
        {
            Console.WriteLine("Starting computation benchmark...");

            var totalMatchups = Utility.BuildMatchups(bots, numberOfMatchups, true);


            // Sequential

            Console.WriteLine("Starting sequential benchmark");

            List<float> sequentialAverageComputations = PlaySequential(timeout, totalMatchups);
            await File.WriteAllLinesAsync(Path.Combine(benchmarkName, "sequential.txt"), sequentialAverageComputations.ConvertAll(x => x.ToString()));

            Console.WriteLine("Finished sequential benchmark");

            // parallel with shared memory

            Console.WriteLine("Starting parallel with shared memory benchmark");

            List<float> sharedMemoryParallelAverageComputations = PlayParallelWithSharedMemory(timeout, threads, totalMatchups);
            await File.WriteAllLinesAsync(Path.Combine(benchmarkName, "parallel_shared_memory.txt"), sharedMemoryParallelAverageComputations.ConvertAll(x => x.ToString()));

            Console.WriteLine("Finished parallel with shared memory benchmark");

            // parallel using containers with seperated memory (SKIPPED FOR NOW)

                //Console.WriteLine("Starting parallel using containers with seperated memory benchmark");

                List<float> seperateMemoryParallelAverageComputations = new();
                //List<float> seperateMemoryParallelAverageComputations = await PlayParallelWithSeperatedMemory(timeout, threads, totalMatchups);
            
                //await File.WriteAllLinesAsync(Path.Combine(benchmarkName, "parallel_separate_memory.txt"), seperateMemoryParallelAverageComputations.ConvertAll(x => x.ToString()));

                //Console.WriteLine("Finished parallel using containers with seperated memory benchmark");

            // Summary
            var sequentialAverageAcrossGames = sequentialAverageComputations.Sum() / sequentialAverageComputations.Count;
            var parallelSharedMemoryAcrossGames = sharedMemoryParallelAverageComputations.Sum() / sharedMemoryParallelAverageComputations.Count;
            var seperateMemoryParralelAcrossGames = seperateMemoryParallelAverageComputations.Sum() / seperateMemoryParallelAverageComputations.Count;

            Directory.CreateDirectory(benchmarkName);

            var summaryLog = "Bots: " + bots[0] + " vs " + bots[1]
                + "\nMatches: " + numberOfMatchups
                + "\nTimeout: " + timeout
                + "\nThreads: " + threads
                + "\n"
                + "\nAverage computations per turn:"
                + "\nSequential: \t" + sequentialAverageAcrossGames
                + "\nShared memory: \t" + parallelSharedMemoryAcrossGames
                + "\nSeperate memory: \t" + seperateMemoryParralelAcrossGames;

            await File.WriteAllTextAsync(Path.Combine(benchmarkName, "summary.txt"), summaryLog);

            Console.WriteLine("Benchmark complete. Results logged in folder: " + benchmarkName);
        }

        private async static Task<List<float>> PlayParallelWithSeperatedMemory(int timeout, int threads, List<(AI, AI)> matchups)
        {
            List<float> computationAverages = new List<float>();
            DockerUtility.LoadGamerunnerImage("./gamerunner-image.tar");
            List<string> containers = await DockerUtility.CreateContainers("gamerunner-image", threads);

            // creates the desired amount of matchups (matches) for each matchup
            var gameQueue = new ConcurrentQueue<(AI, AI)>(matchups);

            var parallalelOptions = new ParallelOptions { MaxDegreeOfParallelism = threads };

            Parallel.ForEach(containers, parallalelOptions, containerName =>
            {
                while (gameQueue.TryDequeue(out (AI, AI) matchup))
                {
                    var output = DockerUtility.PlayMatchOnContainer(containerName, matchup, timeout);
                    var averageTurnComputationCount = DockerUtility.ExtractAverageComputationCount(output);
                    computationAverages.Add(averageTurnComputationCount);
                }
            });

            return computationAverages;
        }

        private static List<float> PlaySequential(int timeout, List<(string, string)> matchups)
        {
            var res = new List<float>();

            foreach (var matchup in matchups)
            {
                var bot1 = Utility.CreateBot(matchup.Item1);
                var bot2 = Utility.CreateBot(matchup.Item2);
                var match = new ScriptsOfTribute.AI.ScriptsOfTribute(bot1, bot2, TimeSpan.FromSeconds(timeout));
                var matchResult = match.Play();
                res.Add(matchResult.Item1.AverageComputationsPerTurn);
            }

            return res;
        }

        private static List<float> PlayParallelWithSharedMemory(int timeout, int threads, List<(string, string)> matchups)
        {
            List<float> res = new List<float>();

            var matches = new List<ScriptsOfTribute.AI.ScriptsOfTribute>();

            foreach (var matchup in matchups)
            {
                var bot1 = Utility.CreateBot(matchup.Item1);
                var bot2 = Utility.CreateBot(matchup.Item2);
                var match = new ScriptsOfTribute.AI.ScriptsOfTribute(bot1, bot2, TimeSpan.FromSeconds(timeout));
                matches.Add(match);
            }

            var options = new ParallelOptions { MaxDegreeOfParallelism = threads };
            Parallel.ForEach(matches, options, match =>
            {
                var matchResult = match.Play();
                res.Add(matchResult.Item1.AverageComputationsPerTurn);
            });

            return res;
        }
    }
}
