cd /d %~dp0
cd "SandboxDownloader-v2"
cd "bin"
cd "Debug"
AutoCompilerForGameServer.exe --gameServerRepository "https://github.com/LeagueSandbox/GameServer.git" --repositoryBranch "master" --commitMessageName "LastCommitMessage.txt" --gameServerSourceFileName "GameServer Source" --copyBuildToFolder "Compiled GameServer" --needsCopied false --pauseAtEnd true --configJSON ""