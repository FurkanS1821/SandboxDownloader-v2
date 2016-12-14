using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using LibGit2Sharp;
using Newtonsoft.Json.Linq;

namespace AutoCompilerForGameServer
{
    class Program
    {
        private static readonly string ExecutingDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        private static void Main()
        {
            Console.WriteLine("Welcome to my GameServer updater!");
            var lastCommit = GetLastCommitMessageFromWeb();
            if (!File.Exists(Path.Combine(ExecutingDirectory, "LastCommitMessage.txt")))
            { // First run
                DownloadAndCompileServer();
                File.WriteAllText(Path.Combine(ExecutingDirectory, "LastCommitMessage.txt"), lastCommit);
            }
            else if (File.ReadAllText(Path.Combine(ExecutingDirectory, "LastCommitMessage.txt")) != GetLastCommitMessageFromWeb())
            { // Update required
                Console.WriteLine("Update found. Updating.");
                FetchAndCompileServer();
                File.WriteAllText(Path.Combine(ExecutingDirectory, "LastCommitMessage.txt"), lastCommit);
            }
            else
            { // Updated already
                Console.Write("Your GameServer is already updated.\nPress any key to exit... ");
                Console.ReadKey(true);
                return;
            }

            Console.Write("Everything is completed.\nPress any key to exit... ");
            Console.ReadKey(true);
        }

        private static void DownloadAndCompileServer()
        {
            var path = Path.Combine(ExecutingDirectory, "CurrentRepository");

            var cloneOptions = new CloneOptions { BranchName = "master", RecurseSubmodules = true };
            Console.Write("Cloning GameServer... ");
            if (Directory.Exists(path))
            {
                DeleteDirectory(path);
            }
            Repository.Clone("https://github.com/LeagueSandbox/GameServer.git", path, cloneOptions);
            Console.WriteLine("done.");

            Console.Write("Creating the GameServer's config file... ");
            var configContent = File.ReadAllText(Path.Combine(path, "GameServerApp", "Settings", "GameInfo.json.template"));
            File.WriteAllText(Path.Combine(path, "GameServerApp", "Settings", "GameInfo.json"), configContent);
            Console.WriteLine("done.");

            Console.Write("Restoring nuget packages... ");
            var nugetProcess = new Process
            {
                StartInfo = new ProcessStartInfo(Path.Combine(ExecutingDirectory, "NuGet.exe"), $"restore {path}")
            };
            nugetProcess.Start();
            nugetProcess.WaitForExit();
            Console.WriteLine("done.");

            Console.Write("Compiling GameServer... ");
            var slnPath = Path.Combine(path, "GameServer.sln");
            var msbuildProcess = new Process
            {
                StartInfo = new ProcessStartInfo(Path.Combine(ExecutingDirectory, "MSBuild.exe"))
                {
                    Arguments = $"\"{slnPath}\" /verbosity:minimal"
                }
            };
            msbuildProcess.Start();
            msbuildProcess.WaitForExit();
            Console.WriteLine("done.");
            DoThingsWithTempStuff();
        }

        private static void FetchAndCompileServer()
        {
            var path = Path.Combine(ExecutingDirectory, "CurrentRepository");
            if (Directory.Exists(path))
            {
                DeleteDirectory(path);
            }
            if (!Directory.Exists(Path.Combine(ExecutingDirectory, "GameServer Source")))
            {
                DownloadAndCompileServer();
                return;
            }
            Directory.Move(Path.Combine(ExecutingDirectory, "GameServer Source"), path);

            Console.Write("Fetching latest version... ");
            using (var repo = new Repository(path))
            {
                var remote = repo.Network.Remotes["origin"];
                repo.Network.Fetch(remote);
            }
            Console.WriteLine("done.");

            Console.Write("Creating the GameServer's config file... ");
            var configContent = File.ReadAllText(Path.Combine(path, "GameServerApp", "Settings", "GameInfo.json.template"));
            File.WriteAllText(Path.Combine(path, "GameServerApp", "Settings", "GameInfo.json"), configContent);
            Console.WriteLine("done.");

            Console.Write("Restoring nuget packages... ");
            var nugetProcess = new Process
            {
                StartInfo = new ProcessStartInfo(Path.Combine(ExecutingDirectory, "NuGet.exe"), $"restore {path}")
            };
            nugetProcess.Start();
            nugetProcess.WaitForExit();
            Console.WriteLine("done.");

            Console.Write("Compiling GameServer... ");
            var slnPath = Path.Combine(path, "GameServer.sln");
            var msbuildProcess = new Process
            {
                StartInfo = new ProcessStartInfo(Path.Combine(ExecutingDirectory, "MSBuild.exe"))
                {
                    Arguments = $"\"{slnPath}\" /verbosity:minimal"
                }
            };
            msbuildProcess.Start();
            msbuildProcess.WaitForExit();
            Console.WriteLine("done.");
            DoThingsWithTempStuff();
        }

        private static string GetLastCommitMessageFromWeb()
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (compatible; MSIE 10.0; Windows NT 6.2; WOW64; Trident/6.0)");

                using (var response = client.GetAsync("https://api.github.com/repos/LeagueSandbox/GameServer/commits").Result)
                {
                    var json = response.Content.ReadAsStringAsync().Result;

                    dynamic commits = JArray.Parse(json);
                    return commits[0].commit.message;
                }
            }
        }

        private static void DoThingsWithTempStuff()
        {
            var repoPath = Path.Combine(ExecutingDirectory, "CurrentRepository");
            var newRepoPath = Path.Combine(ExecutingDirectory, "GameServer source");
            var oldCompiledPath = Path.Combine(ExecutingDirectory, "CurrentRepository", "GameServerApp", "bin", "Debug");
            var newCompiledPath = Path.Combine(ExecutingDirectory, "Compiled GameServer");

            if (Directory.Exists(newCompiledPath) && Directory.EnumerateFileSystemEntries(newCompiledPath).Any())
            {
                Console.Write("Deleting old compiled GameServer folder for your sake... ");
                DeleteDirectory(newCompiledPath);
                Console.WriteLine("done.");
            }

            Console.Write("Moving compiled stuff to where you can access :) ... ");
            Directory.Move(oldCompiledPath, newCompiledPath);
            Directory.Move(repoPath, newRepoPath);
            Console.WriteLine("done.");
        }

        private static void DeleteDirectory(string targetDir)
        {
            var files = Directory.GetFiles(targetDir);
            var dirs = Directory.GetDirectories(targetDir);

            foreach (var file in files)
            {
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            }

            foreach (var dir in dirs)
            {
                DeleteDirectory(dir);
            }

            Directory.Delete(targetDir, false);
        }
    }
}
