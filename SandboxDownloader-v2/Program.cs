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
        static String executingDirectory;
        static String gameServerRepository;
        static String repositoryBranch;
        static bool pauseAtEnd;
        static String commitMessageName;
        static String gameServerSourceFileName;
        static String copyBuildToFolder;
        static bool needsCopied;
        static String configJSON;

        private static void Main(string[] args)
        {
            var logicDurationWatch = new Stopwatch();

            logicDurationWatch.Start();
            
            executingDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            gameServerRepository = "https://github.com/LeagueSandbox/GameServer.git";
            repositoryBranch = "master";
            commitMessageName = "LastCommitMessage.txt";
            gameServerSourceFileName = "GameServer Source";
            copyBuildToFolder = "Compiled GameServer";
            needsCopied = false;
            pauseAtEnd = true;
            configJSON = "";
            
            var p = new NDesk.Options.OptionSet() {
                { "gameServerRepository=", "The game server repository",
                    v => gameServerRepository = v
                },
                { "repositoryBranch=", "The game server repository branch",
                    v => repositoryBranch = v
                },
                { "commitMessageName=", "Commit message file name",
                    v => commitMessageName = v
                },
                { "gameServerSourceFileName=", "Game server source folder name",
                    v => gameServerSourceFileName = v
                },
                { "copyBuildToFolder=", "The folder that the build gets copied to",
                    v => copyBuildToFolder = v
                },
                { "needsCopied=", "Does it need copied even if it doesn't need built?",
                    (bool v) => needsCopied = v
                },
                { "pauseAtEnd=", "Should it be pasued at the end?",
                    (bool v) => pauseAtEnd = v
                },
                { "configJSON=", "The config JSON for the compiled game server.",
                    v => configJSON = v
                }
            };
            
            try
            {
                p.Parse(args);
            }
            catch (NDesk.Options.OptionException e)
            {
                Console.Write("Command line error: ");
                Console.WriteLine(e.Message);
                return;
            }

            Console.WriteLine("Welcome to my GameServer updater!");
            var lastCommit = GetLastCommitMessageFromWeb();

            var needsCompiled = false;

            if (!File.Exists(Path.Combine(executingDirectory, commitMessageName)))
            { // First run
                File.WriteAllText(Path.Combine(executingDirectory, commitMessageName), lastCommit);
                DownloadServer();
                needsCompiled = true;
            }
            else if (File.ReadAllText(Path.Combine(executingDirectory, commitMessageName)) != GetLastCommitMessageFromWeb())
            { // Update required
                Console.WriteLine("Update found. Updating.");
                File.WriteAllText(Path.Combine(executingDirectory, commitMessageName), lastCommit);
                FetchServer();
                needsCompiled = true;
            }
            else
            { // Updated already
                Console.WriteLine("Your GameServer is already updated.");
            }

            if (needsCompiled)
            {
                CompileServer();
                needsCopied = true;
            }

            if (needsCopied)
            {
                //Copy server to build location
                CopyCompiledBuild();

                //Create config file for copied build
                CreateConfigFile();
            }
            
            Console.Write("Everything is completed.\nPress any key to exit... ");
            
            logicDurationWatch.Stop();
            var _timeElapsed = logicDurationWatch.ElapsedMilliseconds;
            Console.WriteLine("Time took: " + _timeElapsed / 1000.0 + " seconds");

            if (pauseAtEnd) Console.ReadKey(true);
        }

        private static void DownloadServer()
        {
            var logicDurationWatch = new Stopwatch();

            logicDurationWatch.Start();

            var path = Path.Combine(executingDirectory, gameServerSourceFileName);//"CurrentRepository");

            var cloneOptions = new CloneOptions { BranchName = repositoryBranch, RecurseSubmodules = true };
            Console.Write("Cloning GameServer... ");
            if (Directory.Exists(path))
            {
                DeleteDirectory(path);
            }
            Repository.Clone(gameServerRepository, path, cloneOptions);
            Console.WriteLine("done.");

            logicDurationWatch.Stop();
            var _timeElapsed = logicDurationWatch.ElapsedMilliseconds;
            Console.WriteLine("Time took for download: " + _timeElapsed / 1000.0 + " seconds");
        }

        private static void FetchServer()
        {
            var logicDurationWatch = new Stopwatch();

            logicDurationWatch.Start();

            var path = Path.Combine(executingDirectory, gameServerSourceFileName);//, "CurrentRepository");
            if (!Directory.Exists(Path.Combine(executingDirectory, gameServerSourceFileName)))
            {
                DownloadServer();
                return;
            }

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

            var path = Path.Combine(executingDirectory, gameServerSourceFileName);//, "CurrentRepository");
            Console.WriteLine("Game server path: " + path);

            Console.Write("Restoring nuget packages... ");

            Console.WriteLine("Nuget path: " + Path.Combine(executingDirectory, "NuGet.exe"));
            var nugetProcess = new Process
            {
                StartInfo = new ProcessStartInfo(Path.Combine(executingDirectory, "NuGet.exe"), $"restore \"{path}\"")
            };
            nugetProcess.Start();
            nugetProcess.WaitForExit();
            Console.WriteLine("done.");

            Console.Write("Compiling GameServer... ");
            var slnPath = Path.Combine(path, "GameServer.sln");
            Console.WriteLine("Compile server path: " + slnPath);
            var msbuildProcess = new Process
            {
                StartInfo = new ProcessStartInfo(Path.Combine(executingDirectory, "MSBuild.exe"))
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
        }

        private static void CreateConfigFile()
        {
            var path = Path.Combine(executingDirectory, copyBuildToFolder);
            //var path = Path.Combine(executingDirectory, "GameServer Source");//, "CurrentRepository");

            Console.Write("Creating the GameServer's config file... ");
            //var configContent = File.ReadAllText(Path.Combine(path, "GameServerApp", "Settings", "GameInfo.json.template"));
            //File.WriteAllText(Path.Combine(path, "GameServerApp", "Settings", "GameInfo.json"), configContent);
            if (configJSON == "")
            {
                configJSON = File.ReadAllText(Path.Combine(path, "Settings", "GameInfo.json.template"));
            }
            File.WriteAllText(Path.Combine(path, "Settings", "GameInfo.json"), configJSON);
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

        private static void CopyCompiledBuild()
        {
            var logicDurationWatch = new Stopwatch();

            logicDurationWatch.Start();
            
            var oldCompiledPath = Path.Combine(executingDirectory, gameServerSourceFileName, "GameServerApp", "bin", "Debug");
            var newCompiledPath = Path.Combine(executingDirectory, copyBuildToFolder);

            if (Directory.Exists(newCompiledPath) && Directory.EnumerateFileSystemEntries(newCompiledPath).Any())
            {
                Console.Write("Deleting old compiled GameServer folder for your sake... ");
                DeleteDirectory(newCompiledPath);
                Console.WriteLine("done.");
            }

            CopyDirectory(oldCompiledPath, newCompiledPath, true);
            
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
