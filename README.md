#SandboxDownloader-v2

This project will download and compile [GameServer](github.com/LeagueSandbox/GameServer), or update it if needed.

###How to Install?
Just go to releases tab above (find it yourself plz :P), download and run the latest release.

###Any known bugs?
Since this checks if latest commit name is different than the one it installed, if a commit is amended to repository, this won't detect it. But, this is not a problem since all commits to GameServer repository is done via Pull Requests and they _must_ have a different name than the latest commit.

######TL;DR No bugs.