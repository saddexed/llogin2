$installDir = "$env:LOCALAPPDATA\Programs\llogin"
$exePath = "$installDir\llogin.exe"
$repoUrl = "https://raw.githubusercontent.com/saddexed/llogin2/install-script/publish/win-x64/llogin.exe"

Write-Host "Installing LLogin..." -ForegroundColor Cyan

if (!(Test-Path $installDir)) {
    New-Item -ItemType Directory -Path $installDir -Force | Out-Null
}

try {
    Write-Host "Downloading binary..."
    Invoke-WebRequest -Uri $repoUrl -OutFile $exePath -ErrorAction Stop
} catch {
    Write-Host "Failed to download binary from $repoUrl" -ForegroundColor Red
    exit 1
}

Write-Host "Updating PATH..."
$userPath = [Environment]::GetEnvironmentVariable("PATH", "User")
if ($userPath -notlike "*$installDir*") {
    [Environment]::SetEnvironmentVariable("PATH", "$userPath;$installDir", "User")
    $env:PATH += ";$installDir"
}

Write-Host "Setting up auto-login task..."
& "$exePath" --task

Write-Host "`nInstallation Successful!" -ForegroundColor Green
Write-Host "Restart your terminal to use 'llogin'." -ForegroundColor Yellow
