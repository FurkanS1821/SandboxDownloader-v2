using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using LibGit2Sharp;

namespace AutoCompilerForGameServer
{
    class Program
    {
        private static string _executingDirectory;
        private static string _gameServerRepository;
        private static string _repositoryBranch;
        private static bool _pauseAtEnd;
        private static string _gameServerSourceFileName;
        private static string _copyBuildToFolder;
        private static bool _needsCopied;
        private static string _configJson;
        private static bool _needsConfig;
        private static string _configurationMode;
        private static bool _onlyPrintBranches;

        private static void Main(string[] args)
        {
            var logicDurationWatch = new Stopwatch();

            logicDurationWatch.Start();
            
            _executingDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            _gameServerRepository = "https://github.com/LeagueSandbox/GameServer.git";
            _repositoryBranch = "indev";
            _gameServerSourceFileName = "GameServer-Source";
            _copyBuildToFolder = "Compiled-GameServer";
            _needsCopied = true;
            _pauseAtEnd = true;
            _configJson = "";
            _needsConfig = true;
            _configurationMode = "Release";
            _onlyPrintBranches = false;

            var p = new NDesk.Options.OptionSet
            {
                { "gameServerRepository=", "The game server repository",
                    v => _gameServerRepository = v
                },
                { "repositoryBranch=", "The game server repository branch",
                    v => _repositoryBranch = v
                },
                { "gameServerSourceFileName=", "Game server source folder name",
                    v => _gameServerSourceFileName = v
                },
                { "copyBuildToFolder=", "The folder that the build gets copied to",
                    v => _copyBuildToFolder = v
                },
                { "needsCopied=", "Does it need copied even if it doesn't need built?",
                    (bool v) => _needsCopied = v
                },
                { "needsConfig=", "Does it need JSON config?",
                    (bool v) => _needsConfig = v
                },
                { "pauseAtEnd=", "Should it be pasued at the end?",
                    (bool v) => _pauseAtEnd = v
                },
                { "configJSON=", "The config JSON for the compiled game server.",
                    v => _configJson = v
                },
                { "onlyPrintBranches=", "Only print the repository branches and exit",
                    (bool v) => _onlyPrintBranches = v
                }
            };
            
            try
            {
                p.Parse(args);
            }
            catch (NDesk.Options.OptionException e)
            {
                Console.WriteLine($"Command line error: {e.Message}");
                return; 
            }

            Console.WriteLine("Welcome to the GameServer updater!");
            
            bool needsCompiled;

            Console.WriteLine($"Repository: {_gameServerRepository}, Branch: {_repositoryBranch}");

            if (_onlyPrintBranches)
            {
                Console.WriteLine("Repository Branches:");
                foreach (var refer in Repository.ListRemoteReferences(_gameServerRepository))
                {
                    if (refer.IsLocalBranch)
                    {
                        var name = refer.CanonicalName;
                        name = name.Replace("refs/heads/", "");
                        Console.WriteLine(name);
                    }
                }
                Console.WriteLine("End Repository Branches");
                if (_pauseAtEnd)
                {
                    Console.ReadKey(true);
                }
                return;
            }

            if (IsRepositoryValid(_gameServerSourceFileName, _repositoryBranch))
            {
                Console.WriteLine("Repository is valid, fetching updates.");

                //Get current commit
                var lastUpdate = GetLastRepositoryCommit(_gameServerSourceFileName, _repositoryBranch);

                Console.WriteLine($"Old Commit: {lastUpdate}");

                FetchServer();

                //Get current commit, compare to past
                var newUpdate = GetLastRepositoryCommit(_gameServerSourceFileName, _repositoryBranch);

                Console.WriteLine($"New Commit: {newUpdate}");

                needsCompiled = !lastUpdate.Equals(newUpdate);
            }
            else
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

            if (_needsCopied)
            {
                Console.WriteLine("Copying server.");
                //Copy server to build location
                CopyCompiledBuild();

                //Create config file for copied build
                if (_needsConfig)
                {
                    CreateConfigFile();
                }
            }
            
            Console.WriteLine("Everything is completed.");
            
            logicDurationWatch.Stop();
            var timeElapsed = logicDurationWatch.ElapsedMilliseconds;
            Console.WriteLine($"Time took: {timeElapsed / 1000.0} seconds");

            if (_pauseAtEnd)
            {
                Console.Write("Press any key to exit. ");
                Console.ReadKey(true);
            }
        }

        private static void DownloadServer()
        {
            var logicDurationWatch = new Stopwatch();

            logicDurationWatch.Start();

            var path = Path.Combine(_executingDirectory, _gameServerSourceFileName);//"CurrentRepository");

            var cloneOptions = new CloneOptions { BranchName = _repositoryBranch, RecurseSubmodules = true };
            Console.Write("Cloning GameServer... ");
            if (Directory.Exists(path))
            {
                DeleteDirectory(path);
            }
            Repository.Clone(_gameServerRepository, path, cloneOptions);

            logicDurationWatch.Stop();
            var timeElapsed = logicDurationWatch.ElapsedMilliseconds;
            Console.WriteLine($"Time took for full download: {timeElapsed / 1000.0} seconds");
        }

        private static void FetchServer()
        {
            var logicDurationWatch = new Stopwatch();

            logicDurationWatch.Start();

            var path = Path.Combine(_executingDirectory, _gameServerSourceFileName);

            Console.Write("Fetching latest version... ");
            using (var repo = new Repository(path))
            {
                /*
                LibGit2Sharp.PullOptions options = new LibGit2Sharp.PullOptions();
                options.FetchOptions = new FetchOptions();
                repo.Network.Pull(new LibGit2Sharp.Signature("Sandbox", "Sandbox", new DateTimeOffset(DateTime.Now)), options);
                */

                // "origin" is the default name given by a Clone operation
                // to the created remote
                var remote = repo.Network.Remotes["origin"];

                // Retrieve the changes from the remote repository
                // (eg. new commits that have been pushed by other contributors)
                repo.Network.Fetch(remote);
                repo.Checkout($"origin/{_repositoryBranch}");
            }

            logicDurationWatch.Stop();
            var timeElapsed = logicDurationWatch.ElapsedMilliseconds;
            Console.WriteLine($"Time took for fetch: {timeElapsed / 1000.0} seconds");
        }

        private static void CompileServer()
        {
            var logicDurationWatch = new Stopwatch();

            logicDurationWatch.Start();

            var path = Path.Combine(_executingDirectory, _gameServerSourceFileName);//, "CurrentRepository");
            Console.WriteLine($"Game server path: {path}");

            Console.Write("Restoring nuget packages... ");
            
            var nugetProcess = new Process
            {
                StartInfo = new ProcessStartInfo("NuGet.exe", $"restore \"{path}\"")
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };
            nugetProcess.OutputDataReceived += (s, e) => Console.WriteLine(e.Data);
            nugetProcess.ErrorDataReceived += (s, e) => Console.WriteLine(e.Data);
            nugetProcess.Start();
            nugetProcess.BeginOutputReadLine();
            nugetProcess.BeginErrorReadLine();
            nugetProcess.WaitForExit();

            nugetProcess = new Process
            {
                StartInfo = new ProcessStartInfo("NuGet.exe", $"update")
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };
            nugetProcess.OutputDataReceived += (s, e) => Console.WriteLine(e.Data);
            nugetProcess.ErrorDataReceived += (s, e) => Console.WriteLine(e.Data);
            nugetProcess.Start();
            nugetProcess.BeginOutputReadLine();
            nugetProcess.BeginErrorReadLine();
            nugetProcess.WaitForExit();

            Console.Write("Running dotnet... ");
            var slnPath = Path.Combine(path, "GameServer.sln");
            var buildProcess = new Process
            {
                StartInfo = new ProcessStartInfo("cmd.exe")
                {
                    Arguments = $"/c dotnet build \"{slnPath}\" --configuration Release",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };
            buildProcess.OutputDataReceived += (s, e) => Console.WriteLine(e.Data);
            buildProcess.ErrorDataReceived += (s, e) => Console.WriteLine(e.Data);
            buildProcess.Start();
            buildProcess.BeginOutputReadLine();
            buildProcess.BeginErrorReadLine();
            buildProcess.WaitForExit();

            logicDurationWatch.Stop();
            var timeElapsed = logicDurationWatch.ElapsedMilliseconds;
            Console.WriteLine($"Time took for compile: {timeElapsed / 1000.0} seconds");
        }

        private static void CreateConfigFile()
        {
            var path = Path.Combine(_executingDirectory, _copyBuildToFolder);

            Console.Write("Creating the GameServer's config file... ");
            if (string.IsNullOrEmpty(_configJson))
            {
                var path2 = Path.Combine(_executingDirectory, _gameServerSourceFileName);
                _configJson = File.ReadAllText(Path.Combine(path2, "GameServerConsole", "Settings", "GameInfo.json.template"));
            }
            Directory.CreateDirectory(Path.Combine(path, "Settings"));
            File.WriteAllText(Path.Combine(path, "Settings", "GameInfo.json"), _configJson);
        }

        private static string GetLastRepositoryCommit(string gameServerSourceFileName, string repositoryBranch)
        {
            var path = Path.Combine(_executingDirectory, gameServerSourceFileName);

            if (!Directory.Exists(path))
            {
                return string.Empty;
            }
            
            using (var repo = new Repository(path))
            {
                var remote = repo.Network.Remotes["origin"];
                var firstCommit = repo.Commits.First();
                return firstCommit.Id + firstCommit.Author.ToString() + firstCommit.MessageShort;
            }
        }

        private static bool IsRepositoryValid(string gameServerSourceFileName, string repositoryBranch)
        {
            var path = Path.Combine(_executingDirectory, gameServerSourceFileName);//"CurrentRepository");

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
            
            var oldCompiledPath = Path.Combine(_executingDirectory, _gameServerSourceFileName, "GameServerConsole", "bin", _configurationMode);
            var newCompiledPath = Path.Combine(_executingDirectory, _copyBuildToFolder);
            
            if (Directory.Exists(newCompiledPath) && Directory.EnumerateFileSystemEntries(newCompiledPath).Any())
            {
                DeleteDirectory(newCompiledPath);
            }

            CopyDirectory(oldCompiledPath, newCompiledPath, true);

            logicDurationWatch.Stop();
            var timeElapsed = logicDurationWatch.ElapsedMilliseconds;
            Console.WriteLine($"Time took for copying build: {timeElapsed / 1000.0} seconds");
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
            var dir = new DirectoryInfo(sourceDirName);

            if (!dir.Exists)
            {
                throw new DirectoryNotFoundException($"Source directory does not exist or could not be found: {sourceDirName}");
            }

            var dirs = dir.GetDirectories();
            // If the destination directory doesn't exist, create it.
            if (!Directory.Exists(destDirName))
            {
                Directory.CreateDirectory(destDirName);
            }

            // Get the files in the directory and copy them to the new location.
            var files = dir.GetFiles();
            foreach (var file in files)
            {
                var temppath = Path.Combine(destDirName, file.Name);
                file.CopyTo(temppath, false);
            }

            // If copying subdirectories, copy them and their contents to new location.
            if (copySubDirs)
            {
                foreach (var subdir in dirs)
                {
                    var temppath = Path.Combine(destDirName, subdir.Name);
                    CopyDirectory(subdir.FullName, temppath, copySubDirs);
                }
            }
        }
    }
}
