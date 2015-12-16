## PowerShell Editor Services Release Process

1. Find the latest build version for the `master` branch on AppVeyor:

2. Run `.\scripts\FetchLatestBuild.ps1`
   
3. Once this script completes, sign the binaries in `.\release\BinariesToSign`.

4. Once you have the authenticode-signed binaries, copy them back into 
   `.\release\BinariesToSign`

5. Run `.\scripts\PackageSignedBinaries.ps1`.  If any binaries didn't get signed 
   correctly, this script will tell you.  Once this script completes, the updated 
   .nupkg files will be in `.\release\FinalPackages`
   
6. Run `.\scripts\PublishPackages.ps1` to publish the final packages to NuGet.