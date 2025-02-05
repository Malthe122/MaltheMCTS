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
        public static void CreateContainer(string imageName, string containerName, string arguments)
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = $"create --name {containerName} {imageName} {arguments}",
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
                    Console.WriteLine($"Container '{containerName}' created successfully (ID: {output.Trim()})");
                }
                else
                {
                    Console.WriteLine($"Error creating container '{containerName}':\n{error}");
                }
            }
        }

        public static List<string> CreateContainers(string imageName, int amount)
        {
            //TODO
            throw new NotImplementedException();
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
