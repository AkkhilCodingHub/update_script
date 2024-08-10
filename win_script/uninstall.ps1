# Get list of all installed packages except pip and its core dependencies
$packages = pip freeze | Where-Object { $_ -notmatch '^(pip|setuptools|wheel)==' }

# Uninstall packages
if ($packages) {
    Write-Host "Uninstalling packages:"
    $packages | ForEach-Object { Write-Host $_ }
    $packages | ForEach-Object { 
        $packageName = $_.Split('==')[0]
        pip uninstall -y $packageName
    }
} else {
    Write-Host "No packages to uninstall."
}