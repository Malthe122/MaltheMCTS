using Docker.DotNet;
using Docker.DotNet.Models;
using ScriptsOfTribute.AI;
using ScriptsOfTribute.Board;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BenchmarkingUtility
{
    public static class DockerUtility
    {
        public async static Task<List<string>> CreateContainers(string imageName, int amount)
        {
            using var client = new DockerClientConfiguration(new Uri("http://localhost:2375")).CreateClient();

            List<string> containerNames = new List<string>();

            for (int i = 0; i < amount; i++)
            {
                string containerName = $"gamerunner_worker_{i}";

                // Delete container if exists

                var existingContainers = await client.Containers.ListContainersAsync(new ContainersListParameters
                {
                    All = true // List stopped containers too
                });

                var existingContainer = existingContainers.FirstOrDefault(c => c.Names.Contains($"/{containerName}"));
                if (existingContainer != null)
                {
                    Console.WriteLine($"Container {containerName} already exists. Removing...");
                    await client.Containers.RemoveContainerAsync(containerName, new ContainerRemoveParameters
                    {
                        Force = true // Ensure it gets removed even if running
                    });
                }

                // Create container

                var response = await client.Containers.CreateContainerAsync(new CreateContainerParameters
                {
                    Image = imageName,
                    Name = containerName,
                    HostConfig = new HostConfig
                    {
                        Memory = 2L * 1024L * 1024L * 1024L,  // 2 GB in bytes
                        MemorySwap = 2L * 1024L * 1024L * 1024L, // Prevent swap usage
                        OomKillDisable = false // Allow Docker to kill if it exceeds memory
                    },
                    Cmd = new List<string> { "sleep", "infinity" } // Keep container running
                });

                await client.Containers.StartContainerAsync(response.ID, new ContainerStartParameters());
                Console.WriteLine($"Started container {containerName}");
            }

            return containerNames;
        }

        public static string PlayMatchOnContainer(string containerName, (AI, AI) bots, int timeout)
        {
            Console.WriteLine($"Starting match on container '{containerName}' between {bots.Item1} and {bots.Item2}");

            // Step 1: Start the container if it's not already running
            if (!RunDockerCommand($"start {containerName}", out _))
            {
                throw new Exception($"Failed to start container '{containerName}'.");
            }

            // Step 2: Execute the game inside the running container
            string bot1 = bots.Item1.GetType().Name;
            string bot2 = bots.Item2.GetType().Name;
            string gameCommand = $"exec {containerName} dotnet run -- {bot1} {bot2} -n 1 -to {timeout}";

            if (RunDockerCommand(gameCommand, out string output))
            {
                Console.WriteLine($"Match completed on '{containerName}':\n{output}");
                return output;
            }
            else
            {
                throw new Exception($"Match execution failed in container '{containerName}'.");
            }
        }

        private static bool RunDockerCommand(string arguments, out string output)
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var process = Process.Start(processInfo))
            {
                output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode == 0)
                    return true;

                Console.WriteLine($"Docker command error:\n{error}");
                return false;
            }
        }

        public static float ExtractAverageComputationCount(string output)
        {
            // Split the output into lines
            string[] lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            // Iterate backward to find the desired line (starting from the last line)
            for (int i = lines.Length - 1; i >= 0; i--)
            {
                string line = lines[i].Trim(); // Trim spaces in case of formatting issues

                if (line.StartsWith("Average computation count per turn:"))
                {
                    string valuePart = line.Replace("Average computation count per turn:", "").Trim();

                    if (float.TryParse(valuePart, out float result))
                    {
                        return result; // Successfully extracted the value
                    }
                }
            }

            throw new Exception("Failed to extract AverageComputationCount from output.");
        }

        public static void LoadGamerunnerImage(string imageTarFilePath)
        {
            Console.WriteLine($"Loading Docker image from: {imageTarFilePath}");

            var processInfo = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = $"load -i \"{imageTarFilePath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var process = Process.Start(processInfo))
            {
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode == 0)
                {
                    Console.WriteLine($"Docker image loaded successfully:\n{output}");
                }
                else
                {
                    Console.WriteLine($"Failed to load Docker image. Error:\n{error}");
                }
            }
        }
    }
}
