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
        static String ExecutingDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        private static void Main(string[] args)
        {

            var logicDurationWatch = new Stopwatch();

            logicDurationWatch.Start();
            /*
            //
             
            String executingDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            String repositoryDirectoryName = "CurrentRepository";
            String gameServerRepository = "https://github.com/LeagueSandbox/GameServer.git";
            String repositoryBranch = "master";
            bool generateGameJSON = true;
            bool pauseAtEnd = true;
            bool copyToTemporaryLocation;
            String temporaryLocation;
            String commitMessageTextFileName = "LastCommitMessage.txt";
            String commitMessageTextFilePath = Path.Combine(ExecutingDirectory, "LastCommitMessage.txt");
            String gameServerSourcePath = Path.Combine(ExecutingDirectory, "GameServer Source");

            //executingDirectory, gameServerDirectoryName, gameServerRepository, repositoryBranch, generateGameJSON

            bool show_help = false;
            List<string> names = new List<string>();
            int repeat = 1;

            var p = new NDesk.Options.OptionSet() {
    { "executingDirectory=", "The repository directory",
       v => names.Add (v) },
    { "r|repeat=",
       "the number of {TIMES} to repeat the greeting.\n" +
          "this must be an integer.",
        (int v) => repeat = v },
    { "v", "increase debug message verbosity",
       v => { if (v != null) ++verbosity; } },
    { "h|help",  "show this message and exit",
       v => show_help = v != null },
};

            List<string> extra;
            try
            {
                extra = p.Parse(args);
            }
            catch (OptionException e)
            {
                Console.Write("greet: ");
                Console.WriteLine(e.Message);
                Console.WriteLine("Try `greet --help' for more information.");
                return;
            }



    */


            //ExecutingDirectory
            //

            Console.WriteLine("Welcome to my GameServer updater!");
            var lastCommit = GetLastCommitMessageFromWeb();
            if (!File.Exists(Path.Combine(ExecutingDirectory, "LastCommitMessage.txt")))
            { // First run
                File.WriteAllText(Path.Combine(ExecutingDirectory, "LastCommitMessage.txt"), lastCommit);
                DownloadServer();
                CompileServer();
            }
            else if (File.ReadAllText(Path.Combine(ExecutingDirectory, "LastCommitMessage.txt")) != GetLastCommitMessageFromWeb())
            { // Update required
                Console.WriteLine("Update found. Updating.");
                File.WriteAllText(Path.Combine(ExecutingDirectory, "LastCommitMessage.txt"), lastCommit);
                FetchServer();
                CompileServer();
            }
            else
            { // Updated already
                Console.WriteLine("Your GameServer is already updated.");
            }

            //TODO : Copy to temporary location if need to

            //TODO : Generate temporary JSON at temporary location if need to

            Console.Write("Everything is completed.\nPress any key to exit... ");


            logicDurationWatch.Stop();
            var _timeElapsed = logicDurationWatch.ElapsedMilliseconds;
            Console.WriteLine("Time took: " + _timeElapsed / 1000.0 + " seconds");

            Console.ReadKey(true);
        }

        private static void DownloadServer()
        {

            var logicDurationWatch = new Stopwatch();

            logicDurationWatch.Start();

            var path = Path.Combine(ExecutingDirectory, "GameServer Source");//"CurrentRepository");

            var cloneOptions = new CloneOptions { BranchName = "master", RecurseSubmodules = true };
            Console.Write("Cloning GameServer... ");
            //if (Directory.Exists(path))
            //{
            //    DeleteDirectory(path);
            //}
            Repository.Clone("https://github.com/LeagueSandbox/GameServer.git", path, cloneOptions);
            Console.WriteLine("done.");


            logicDurationWatch.Stop();
            var _timeElapsed = logicDurationWatch.ElapsedMilliseconds;
            Console.WriteLine("Time took for download: " + _timeElapsed / 1000.0 + " seconds");
        }

        private static void FetchServer()
        {
            var logicDurationWatch = new Stopwatch();

            logicDurationWatch.Start();

            var path = Path.Combine(ExecutingDirectory, "GameServer Source");//, "CurrentRepository");
            //if (Directory.Exists(path))
            //{
            //    DeleteDirectory(path);
            //}
            if (!Directory.Exists(Path.Combine(ExecutingDirectory, "GameServer Source")))
            {
                DownloadServer();
                return;
            }
            //Directory.Move(Path.Combine(ExecutingDirectory, "GameServer Source"), path);

            Console.Write("Fetching latest version... ");
            using (var repo = new Repository(path))
            {
                var remote = repo.Network.Remotes["origin"];
                repo.Network.Fetch(remote);
            }
            Console.WriteLine("done.");

            logicDurationWatch.Stop();
            var _timeElapsed = logicDurationWatch.ElapsedMilliseconds;
            Console.WriteLine("Time took for fetch: " + _timeElapsed / 1000.0 + " seconds");
        }

        private static void CompileServer()
        {
            var logicDurationWatch = new Stopwatch();

            logicDurationWatch.Start();

            var path = Path.Combine(ExecutingDirectory, "GameServer Source");//, "CurrentRepository");
            Console.WriteLine("Game server path: " + path);

            Console.Write("Restoring nuget packages... ");

            Console.WriteLine("Nuget path: " + Path.Combine(ExecutingDirectory, "NuGet.exe"));
            var nugetProcess = new Process
            {
                StartInfo = new ProcessStartInfo(Path.Combine(ExecutingDirectory, "NuGet.exe"), $"restore \"{path}\"")
            };
            nugetProcess.Start();
            nugetProcess.WaitForExit();
            Console.WriteLine("done.");

            Console.Write("Compiling GameServer... ");
            var slnPath = Path.Combine(path, "GameServer.sln");
            Console.WriteLine("Compile server path: " + slnPath);
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

            logicDurationWatch.Stop();
            var _timeElapsed = logicDurationWatch.ElapsedMilliseconds;
            Console.WriteLine("Time took for compile: " + _timeElapsed / 1000.0 + " seconds");
            
            DoThingsWithTempStuff();

            CreateConfigFile();
        }

        private static void CreateConfigFile()
        {
            var path = Path.Combine(ExecutingDirectory, "Compiled GameServer");
            //var path = Path.Combine(ExecutingDirectory, "GameServer Source");//, "CurrentRepository");

            Console.Write("Creating the GameServer's config file... ");
            //var configContent = File.ReadAllText(Path.Combine(path, "GameServerApp", "Settings", "GameInfo.json.template"));
            //File.WriteAllText(Path.Combine(path, "GameServerApp", "Settings", "GameInfo.json"), configContent);
            var configContent = File.ReadAllText(Path.Combine(path, "Settings", "GameInfo.json.template"));
            File.WriteAllText(Path.Combine(path, "Settings", "GameInfo.json"), configContent);
            Console.WriteLine("done.");
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

            var logicDurationWatch = new Stopwatch();

            logicDurationWatch.Start();

            /*
            var repoPath = Path.Combine(ExecutingDirectory, "CurrentRepository");
            var newRepoPath = Path.Combine(ExecutingDirectory, "GameServer source");
            var oldCompiledPath = Path.Combine(ExecutingDirectory, "CurrentRepository", "GameServerApp", "bin", "Debug");
            var newCompiledPath = Path.Combine(ExecutingDirectory, "Compiled GameServer");
            */

            
            var oldCompiledPath = Path.Combine(ExecutingDirectory, "GameServer Source", "GameServerApp", "bin", "Debug");
            var newCompiledPath = Path.Combine(ExecutingDirectory, "Compiled GameServer");

            if (Directory.Exists(newCompiledPath) && Directory.EnumerateFileSystemEntries(newCompiledPath).Any())
            {
                Console.Write("Deleting old compiled GameServer folder for your sake... ");
                DeleteDirectory(newCompiledPath);
                Console.WriteLine("done.");
            }

            CopyDirectory(oldCompiledPath, newCompiledPath, true);

            //Console.Write("Moving compiled stuff to where you can access :) ... ");
            //Directory.Move(oldCompiledPath, newCompiledPath);
            //Directory.Move(repoPath, newRepoPath);
            Console.WriteLine("done.");


            logicDurationWatch.Stop();
            var _timeElapsed = logicDurationWatch.ElapsedMilliseconds;
            Console.WriteLine("Time took for copying build: " + _timeElapsed / 1000.0 + " seconds");
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

        private static void CopyDirectory(string sourceDirName, string destDirName, bool copySubDirs)
        {
            // Get the subdirectories for the specified directory.
            DirectoryInfo dir = new DirectoryInfo(sourceDirName);

            if (!dir.Exists)
            {
                throw new DirectoryNotFoundException(
                    "Source directory does not exist or could not be found: "
                    + sourceDirName);
            }

            DirectoryInfo[] dirs = dir.GetDirectories();
            // If the destination directory doesn't exist, create it.
            if (!Directory.Exists(destDirName))
            {
                Directory.CreateDirectory(destDirName);
            }

            // Get the files in the directory and copy them to the new location.
            FileInfo[] files = dir.GetFiles();
            foreach (FileInfo file in files)
            {
                string temppath = Path.Combine(destDirName, file.Name);
                file.CopyTo(temppath, false);
            }

            // If copying subdirectories, copy them and their contents to new location.
            if (copySubDirs)
            {
                foreach (DirectoryInfo subdir in dirs)
                {
                    string temppath = Path.Combine(destDirName, subdir.Name);
                    CopyDirectory(subdir.FullName, temppath, copySubDirs);
                }
            }
        }
    }

}
