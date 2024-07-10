function Invoke-WingetUpdate {
    Write-Host "<color=green>🟢 Checking for Windows package updates...</color>"
    Start-Process "winget" -ArgumentList "upgrade all" -Verb RunAs -NoNewWindow
}

function Invoke-GitUpdate {
    Write-Host "<color=green>🟢 Updating all Git repositories on the system...</color>"
    $userFolders = [Environment]::GetFolderPath("MyDocuments"), [Environment]::GetFolderPath("Desktop"), [Environment]::GetFolderPath("Downloads"), [Environment]::GetFolderPath("Music"), [Environment]::GetFolderPath("Videos"), [Environment]::GetFolderPath("Pictures")
    $externalDrives = Get-WmiObject Win32_LogicalDisk -Filter "DriveType=2"
    $searchLocations = $userFolders + $externalDrives.DeviceID
    foreach ($location in $searchLocations) {
        Write-Host "<color=green>🟢 Searching for Git repositories in $location</color>"
        $gitRepos = Get-ChildItem -Path $location -Recurse -Filter ".git" -Directory -ErrorAction SilentlyContinue | Where-Object { -not $_.Attributes -band [System.IO.FileAttributes]::System -and -not $_.Attributes -band [System.IO.FileAttributes]::Hidden }
        foreach ($repo in $gitRepos) {
            $repoDir = $repo.FullName.Replace(".git", "")
            Write-Host "<color=green>🟢 Updating repository at $repoDir</color>"
            Set-Location -Path $repoDir
            & git fetch
            & git pull -rebase
        }
    }
}
function Invoke-PipUpdate {
    Write-Host "<color=green>🟢 Updating all pip packages...</color>"
    $outdatedPackages = pip list --outdated --format=json
    foreach ($package in $outdatedPackages) {
        $packageName = $package -split '=='[0]
        Write-Host "<color=green>🟢 Updating $packageName...</color>"
        pip install --upgrade $packageName
    }
    Write-Host "<color=green>🟢 All pip packages have been updated.</color>"
}

Write-Host "<color=green>🟢 Checking for updates...</color>"
Invoke-WingetUpdate
Invoke-GitUpdate
Invoke-PipUpdate

