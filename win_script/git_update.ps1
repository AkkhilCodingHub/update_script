function Invoke-WingetUpdate {
    Start-Process "winget" -ArgumentList "upgrade all" -Verb RunAs -NoNewWindow
}

function Invoke-GitUpdate {
    Write-Host "Updating all Git repositories on the system..."
    $drives = Get-WmiObject Win32_LogicalDisk -Filter "DriveType=3"
    foreach ($drive in $drives) {
        $driveRoot = $drive.DeviceID + "\"
        Write-Host "Searching for Git repositories in $driveRoot"
        $gitRepos = Get-ChildItem -Path $driveRoot -Recurse -Filter ".git" -Directory -ErrorAction SilentlyContinue | Where-Object { -not $_.Attributes -band [System.IO.FileAttributes]::System -and -not $_.Attributes -band [System.IO.FileAttributes]::Hidden }
        foreach ($repo in $gitRepos) {
            $repoDir = $repo.FullName.Replace(".git", "")
            Write-Host "Updating repository at $repoDir"
            Set-Location -Path $repoDir
            & git fetch
            & git pull -rebase
        }
    }
}
function Invoke-PipUpdate {
    Write-Host "Updating all pip packages..."
    $outdatedPackages = pip list --outdated --format=json
    foreach ($package in $outdatedPackages) {
        $packageName = $package -split '=='[0]
        Write-Host "Updating $packageName..."
        pip install --upgrade $packageName
    }
    Write-Host "All pip packages have been updated."
}

Write-Host "Checking for updates..."
Invoke-WingetUpdate
Invoke-GitUpdate
Invoke-PipUpdate

