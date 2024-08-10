function Invoke-WingetUpdate {
    Write-Host "🟢 Checking for Windows package updates..." -ForegroundColor Green
    $wingetProcess = Start-Process "winget" -ArgumentList "upgrade --all" -Verb RunAs -PassThru -Wait
    if ($wingetProcess.ExitCode -ne 0) {
        Write-Host "⚠️ Winget update process exited with code $($wingetProcess.ExitCode)" -ForegroundColor Yellow
    }
}

function Invoke-GitUpdate {
    if (Get-Command git -ErrorAction SilentlyContinue) {
        Write-Host "🟢 Updating all Git repositories on the system..." -ForegroundColor Green
        $userFolders = @(
            [Environment]::GetFolderPath("MyDocuments"),
            [Environment]::GetFolderPath("Desktop"),
            [Environment]::GetFolderPath("MyMusic"),
            [Environment]::GetFolderPath("MyVideos"),
            [Environment]::GetFolderPath("MyPictures"),
            [Environment]::GetFolderPath("UserProfile") + "\Downloads"
        )
        $externalDrives = Get-WmiObject Win32_LogicalDisk -Filter "DriveType=2"
        $searchLocations = $userFolders + $externalDrives.DeviceID

        foreach ($location in $searchLocations) {
            $gitRepos = Get-ChildItem -Path $location -Recurse -Filter ".git" -Directory -ErrorAction SilentlyContinue | Where-Object { -not $_.Attributes -band [System.IO.FileAttributes]::System -and -not $_.Attributes -band [System.IO.FileAttributes]::Hidden }
            foreach ($repo in $gitRepos) {
                $repoDir = $repo.FullName.Replace(".git", "")
                Write-Host "🟢 Updating repository at $repoDir" -ForegroundColor Green
                Set-Location -Path $repoDir
                git fetch
                git pull
            }
        }
    }
}

function Invoke-PipUpdate {
    if (Get-Command pip -ErrorAction SilentlyContinue) {
        Write-Host "🟢 Updating all pip packages..." -ForegroundColor Green
        $outdatedPackages = pip list --outdated --format=json | ConvertFrom-Json

        foreach ($package in $outdatedPackages) {
            $packageName = $package.name
            Write-Host "🟢 Updating $packageName..." -ForegroundColor Green
            pip install --upgrade $packageName
        }
        Write-Host "🟢 All pip packages have been updated." -ForegroundColor Green
    }
}

Write-Host "🟢 Starting system update..." -ForegroundColor Green
Invoke-WingetUpdate
Invoke-GitUpdate
Invoke-PipUpdate
Write-Host "🟢 System update completed." -ForegroundColor Green