function Invoke-WindowsUpdates {
    Write-Host "Checking for Windows updates..."
    $updateSession = New-Object -ComObject Microsoft.Update.Session
    $updateSearcher = $updateSession.CreateUpdateSearcher()
    $searchResult = $updateSearcher.Search("IsInstalled=0")
    
    if ($null -ne $searchResult.Updates) {
        Write-Host "Windows updates are available."
        $searchResult.Updates | ForEach-Object { Write-Host $_.Title }
    } else {
        Write-Host "No Windows updates available."
    }
}

function Invoke-GitUpdate {
    Write-Host "Updating all Git repositories on the system..."
    $drives = Get-PSDrive -PSProvider 'FileSystem' | Where-Object { $null -ne $_.Used }
    foreach ($drive in $drives) {
        $driveRoot = $drive.Root
        Write-Host "Searching for Git repositories in $driveRoot"
        $gitRepos = Get-ChildItem -Path $driveRoot -Recurse -Directory -Filter ".git" -ErrorAction SilentlyContinue
        foreach ($repo in $gitRepos) {
            $repoDir = $repo.FullName
            Write-Host "Updating repository at $repoDir"
            Set-Location -Path $repoDir
            Set-Location -Path ..
            git fetch
            git pull
            Set-Location -Path $driveRoot
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
Invoke-WindowsUpdates
Invoke-GitUpdate
Invoke-PipUpdate
