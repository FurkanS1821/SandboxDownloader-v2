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
        static String gameServerSourceFileName;
        static String copyBuildToFolder;
        static bool needsCopied;
        static String configJSON;
        static bool needsConfig;
        static String configurationMode;

        private static void Main(string[] args)
        {
            var logicDurationWatch = new Stopwatch();

            logicDurationWatch.Start();
            
            executingDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            gameServerRepository = "https://github.com/LeagueSandbox/GameServer.git";
            repositoryBranch = "master";
            gameServerSourceFileName = "GameServer-Source";
            copyBuildToFolder = "Compiled-GameServer";
            needsCopied = true;
            pauseAtEnd = true;
            configJSON = "";
            needsConfig = true;
            configurationMode = "Release";

            var p = new NDesk.Options.OptionSet() {
                { "gameServerRepository=", "The game server repository",
                    v => gameServerRepository = v
                },
                { "repositoryBranch=", "The game server repository branch",
                    v => repositoryBranch = v
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
                { "needsConfig=", "Does it need JSON config?",
                    (bool v) => needsConfig = v
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

            Console.WriteLine("Welcome to Furkan_S's GameServer updater!");
            
            var needsCompiled = false;

            Console.WriteLine("Repository: " + gameServerRepository + ", Branch: " + repositoryBranch);

            if (IsRepositoryValid(gameServerSourceFileName, repositoryBranch))
            {
                Console.WriteLine("Repository is valid, fetching updates.");

                //Get current commit
                String lastUpdate = GetLastRepositoryCommit(gameServerSourceFileName, repositoryBranch);

                FetchServer();

                //Get current commit, compare to past
                String newUpdate = GetLastRepositoryCommit(gameServerSourceFileName, repositoryBranch);
                
                needsCompiled = !lastUpdate.Equals(newUpdate);
            } else
            {
                //Download repository
                Console.WriteLine("Repository is invalid, downloading updates.");
                DownloadServer();
                needsCompiled = true;
            }

            if (needsCompiled)
            {
                Console.WriteLine("Compiling server.");
                CompileServer();
            }

            if (needsCopied)
            {
                Console.WriteLine("Copying server.");
                //Copy server to build location
                CopyCompiledBuild();

                //Create config file for copied build
                if (needsConfig) CreateConfigFile();
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

            logicDurationWatch.Stop();
            var _timeElapsed = logicDurationWatch.ElapsedMilliseconds;
            Console.WriteLine("Time took for full download: " + _timeElapsed / 1000.0 + " seconds");
        }

        private static void FetchServer()
        {
            var logicDurationWatch = new Stopwatch();

            logicDurationWatch.Start();

            var path = Path.Combine(executingDirectory, gameServerSourceFileName);

            Console.Write("Fetching latest version... ");
            using (var repo = new Repository(path))
            {
                var remote = repo.Network.Remotes["origin"];
                repo.Network.Fetch(remote);
            }

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
            
            var nugetProcess = new Process
            {
                StartInfo = new ProcessStartInfo(Path.Combine(executingDirectory, "NuGet.exe"), $"restore \"{path}\"")
            };
            nugetProcess.StartInfo.UseShellExecute = false;
            nugetProcess.StartInfo.RedirectStandardOutput = true;
            nugetProcess.StartInfo.RedirectStandardError = true;
            nugetProcess.StartInfo.CreateNoWindow = true;
            nugetProcess.OutputDataReceived += (s, e) => Console.WriteLine(e.Data);
            nugetProcess.ErrorDataReceived += (s, e) => Console.WriteLine(e.Data);
            nugetProcess.Start();
            nugetProcess.BeginOutputReadLine();
            nugetProcess.BeginErrorReadLine();
            nugetProcess.WaitForExit();

            Console.Write("Running MSBuild... ");
            var slnPath = Path.Combine(path, "GameServer.sln");
            var msbuildProcess = new Process
            {
                StartInfo = new ProcessStartInfo(Path.Combine(executingDirectory, "MSBuild.exe"))
                {// /t:Build,AfterBuild
                    Arguments = $"\"{slnPath}\" /verbosity:minimal /property:Configuration=" +configurationMode
                }
            };
            msbuildProcess.StartInfo.UseShellExecute = false;
            msbuildProcess.StartInfo.RedirectStandardOutput = true;
            msbuildProcess.StartInfo.RedirectStandardError = true;
            msbuildProcess.StartInfo.CreateNoWindow = true;
            msbuildProcess.OutputDataReceived += (s, e) => Console.WriteLine(e.Data);
            msbuildProcess.ErrorDataReceived += (s, e) => Console.WriteLine(e.Data);
            msbuildProcess.Start();
            msbuildProcess.BeginOutputReadLine();
            msbuildProcess.BeginErrorReadLine();
            msbuildProcess.WaitForExit();

            logicDurationWatch.Stop();
            var _timeElapsed = logicDurationWatch.ElapsedMilliseconds;
            Console.WriteLine("Time took for compile: " + _timeElapsed / 1000.0 + " seconds");
        }

        private static void CreateConfigFile()
        {
            var path = Path.Combine(executingDirectory, copyBuildToFolder);

            Console.Write("Creating the GameServer's config file... ");
            if (configJSON == "")
            {
                var path2 = Path.Combine(executingDirectory, gameServerSourceFileName);
                configJSON = File.ReadAllText(Path.Combine(path2, "GameServerApp", "Settings", "GameInfo.json.template"));
            }
            Directory.CreateDirectory(Path.Combine(path, "Settings"));
            File.WriteAllText(Path.Combine(path, "Settings", "GameInfo.json"), configJSON);
        }

        private static String GetLastRepositoryCommit(String gameServerSourceFileName, String repositoryBranch)
        {
            var path = Path.Combine(executingDirectory, gameServerSourceFileName);

            if (!Directory.Exists(path))
            {
                return "";
            }
            
            using (var repo = new Repository(path))
            {
                var remote = repo.Network.Remotes["origin"];
                Commit firstCommit = repo.Commits.First();
                return firstCommit.Id.ToString() + firstCommit.Author.ToString() + firstCommit.MessageShort;
            }
        }

        private static bool IsRepositoryValid(String gameServerSourceFileName, String repositoryBranch)
        {
            var path = Path.Combine(executingDirectory, gameServerSourceFileName);//"CurrentRepository");

            if (!Directory.Exists(path))
            {
                return false;
            }

            var cloneOptions = new CloneOptions { BranchName = repositoryBranch, RecurseSubmodules = true };
            return Repository.IsValid(path);
        }
        
        private static void CopyCompiledBuild()
        {
            var logicDurationWatch = new Stopwatch();

            logicDurationWatch.Start();
            
            var oldCompiledPath = Path.Combine(executingDirectory, gameServerSourceFileName, "GameServerApp", "bin", configurationMode);
            var newCompiledPath = Path.Combine(executingDirectory, copyBuildToFolder);
            
            if (Directory.Exists(newCompiledPath) && Directory.EnumerateFileSystemEntries(newCompiledPath).Any())
            {
                DeleteDirectory(newCompiledPath);
            }

            CopyDirectory(oldCompiledPath, newCompiledPath, true);

            //Copy gamemode data
            var oldModePath = Path.Combine(executingDirectory, gameServerSourceFileName, "GameServerApp", "Content", "GameMode");
            var newModePath = Path.Combine(executingDirectory, copyBuildToFolder, "Content", "GameMode");
            if (!Directory.Exists(newModePath))
            {
                CopyDirectory(oldModePath, newModePath, true);
            }

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
