function Check-WindowsUpdates {
    Write-Host "Checking for Windows updates..."
    # Check for Windows updates using PowerShell's Get-WindowsUpdateLog cmdlet
    $windowsUpdateLog = Get-WindowsUpdateLog
    if ($windowsUpdateLog -ne $null) {
        Write-Host "Windows updates are available."
    } else {
        Write-Host "No Windows updates available."
    }
}

function Check-GitUpdate {
    Write-Host "Checking for Git updates..."
    $gitInstalled = $null
    try {
        # Check if Git is installed
        $gitInstalled = (git --version) 2>&1
    }
    catch {
        Write-Host "Git is not installed."
        return
    }

    if ($gitInstalled -like "git version") {
        # Check for updates
        $updateCheck = git fetch --dry-run 2>&1
        if ($updateCheck -notmatch "fatal: unable to access") {
            Write-Host "Git updates are available."
        } else {
            Write-Host "No Git updates available."
        }
    }
}

function Check-PipUpdate {
    Write-Host "Checking for pip updates..."
    $pipInstalled = $null
    try {
        # Check if pip is installed
        $pipInstalled = (pip --version) 2>&1
    }
    catch {
        Write-Host "pip is not installed."
        return
    }

    if ($pipInstalled -like "pip") {
        # Check for updates
        $outdatedPackages = pip list --outdated 2>&1
        if ($outdatedPackages -match "Package") {
            Write-Host "pip updates are available."
        } else {
            Write-Host "No pip updates available."
        }
    }
}

Write-Host "Checking for updates..."
Check-WindowsUpdates
Check-GitUpdate
Check-PipUpdate
