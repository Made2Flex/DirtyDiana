# ============================================================
# DirtyDiana Windows Build Script
# ============================================================
# (c) Made2Flex <https://github.com/Made2Flex/DirtyDiana>


# Warn if ran from cmd line and not powershell
if ($env:ComSpec -ne $null -and ($Host.Name -eq 'ConsoleHost' -or $Host.Name -eq 'Windows PowerShell Console Host')) {
    if ($env:PROMPT -ne $null -and $env:PROMPT.Contains('CMD')) {
        Write-Warning "You are running this script from cmd.exe. Please run it using Windows PowerShell or PowerShell 7+."
    }
}

function Get-Dependency {
    param(
        [string]$Command,
        [string]$PackageId,
        [string]$Description
    )
    if (-not (Get-Command $Command -ErrorAction SilentlyContinue)) {
        Write-Host "$Description not found. Attempting to install via winget..."
        if (-not (Get-Command winget -ErrorAction SilentlyContinue)) {
            Write-Error "winget (Windows Package Manager) is not installed. Install it from Microsoft Store or https://aka.ms/getwinget"
            exit 1
        }
        $installResult = winget install --id $PackageId --silent --accept-package-agreements --accept-source-agreements
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Failed to install $Description via winget. Please install it manually."
            exit 1
        }
    } else {
        Write-Host "$Description is already installed."
    }
}

if (-not (Get-Command winget -ErrorAction SilentlyContinue)) {
    Write-Error "winget (Windows Package Manager) is not installed. Install it from Microsoft Store or https://aka.ms/getwinget"
    exit 1
}

# Check for dotnet 8.0 SDK
$dotnet8Installed = & dotnet --list-sdks 2>$null | Select-String "^8\.0"
if (-not $dotnet8Installed) {
    Write-Host "dotnet 8.0 SDK not found. Attempting to install via winget..."
    $dotnetInstallResult = winget install --id Microsoft.DotNet.SDK.8 --accept-package-agreements --accept-source-agreements
    if ($LASTEXITCODE -ne 0) {
        Write-Error "dotnet 8.0 installation via winget failed. Please install manually from https://dotnet.microsoft.com/"
        exit 1
    }
    # Check again after install
    $dotnet8Installed = & dotnet --list-sdks 2>$null | Select-String "^8\.0"
    if (-not $dotnet8Installed) {
        Write-Error "dotnet 8.0 SDK install failed. Please install manually."
        exit 1
    }
    Write-Host "dotnet 8.0 SDK installed successfully."
} else {
    Write-Host "dotnet 8.0 SDK is installed."
}

$clExists = $false

# find cl.exe
if (Get-Command cl.exe -ErrorAction SilentlyContinue) {
    $clExists = $true
} else {
    Write-Host "Looking for BuildTools..."
    $potentialCl = Get-ChildItem -Path "${env:ProgramFiles(x86)}\Microsoft Visual Studio" -Filter cl.exe -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($potentialCl) {
        $env:Path += ";$($potentialCl.DirectoryName)"
        $clExists = $true
        Write-Host "Added $($potentialCl.DirectoryName) to PATH."
    }
}

if (-not $clExists) {
    Write-Host "Visual C++ Build Tools not found. Attempting to install via winget..."
    $vcBuildToolsResult = winget install --id Microsoft.VisualStudio.2022.BuildTools --accept-package-agreements --accept-source-agreements
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Visual C++ Build Tools install failed (winget exited with error). Please install manually from https://visualstudio.microsoft.com/visual-cpp-build-tools/"
        exit 1
    }

    $potentialCl = Get-ChildItem -Path "${env:ProgramFiles(x86)}\Microsoft Visual Studio" -Filter cl.exe -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($potentialCl) {
        $env:Path += ";$($potentialCl.DirectoryName)"
        Write-Host "Visual C++ Build Tools installed and cl.exe found at $($potentialCl.FullName). Added to PATH."
    } elseif (Get-Command cl.exe -ErrorAction SilentlyContinue) {
        Write-Host "Visual C++ Build Tools installed and cl.exe detected."
    } else {
        Write-Error "Visual C++ Build Tools install completed, but cl.exe not found. Please install manually from https://visualstudio.microsoft.com/visual-cpp-build-tools/"
        exit 1
    }
} else {
    Write-Host "Visual C++ Build Tools detected."
}

# Build the project
Write-Host "Building the project with dotnet..."
$buildResult = dotnet build -c Release -r win-x64 DirtyDiana/DirtyDiana.csproj
if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet build failed."
    exit 1
}

Write-Host "Publishing project binaries..."
$publishResult = dotnet publish -c Release -r win-x64 DirtyDiana/DirtyDiana.csproj -o publish
if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet publish failed."
    exit 1
}

# Determine Desktop path and move published binary
$desktopPath = [Environment]::GetFolderPath('Desktop')
$targetDir = Join-Path $desktopPath "DirtyDiana"

if (-not (Test-Path $targetDir)) {
    New-Item -ItemType Directory -Path $targetDir | Out-Null
}

# Move all contents from publish directory to Desktop\DirtyDiana
$publishDir = Join-Path $PSScriptRoot "publish"

if (Test-Path $publishDir) {
    Get-ChildItem -Path $publishDir -Recurse | ForEach-Object {
        $dest = Join-Path $targetDir $_.Name
        Move-Item -Path $_.FullName -Destination $dest -Force
    }
    Write-Host "Published binaries moved to $targetDir"
} else {
    Write-Error "Publish directory not found at $publishDir"
    exit 1
}

Write-Host "Project binaries published successfully."
Write-Host "Build completed."
exit 0
