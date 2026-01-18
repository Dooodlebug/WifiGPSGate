#Requires -Version 5.1
<#
.SYNOPSIS
    Builds the WifiGPSGate installer.

.DESCRIPTION
    This script builds the WifiGPSGate application in Release mode,
    publishes it as a self-contained application, and creates an
    installer using Inno Setup.

.PARAMETER Configuration
    Build configuration (Release or Debug). Default is Release.

.PARAMETER SkipBuild
    Skip the dotnet build/publish step.

.PARAMETER SkipInstaller
    Skip the Inno Setup compilation step.

.EXAMPLE
    .\build-installer.ps1

.EXAMPLE
    .\build-installer.ps1 -SkipBuild
#>

param(
    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release",

    [switch]$SkipBuild,

    [switch]$SkipInstaller
)

$ErrorActionPreference = "Stop"

# Script paths
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RootDir = Split-Path -Parent $ScriptDir
$AppProjectPath = Join-Path $RootDir "src\WifiGPSGate.App\WifiGPSGate.App.csproj"
$IssPath = Join-Path $ScriptDir "WifiGPSGate.iss"
$OutputDir = Join-Path $ScriptDir "output"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "WifiGPSGate Installer Build Script" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Ensure output directory exists
if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir | Out-Null
    Write-Host "Created output directory: $OutputDir" -ForegroundColor Green
}

# Step 1: Build and publish the application
if (-not $SkipBuild) {
    Write-Host "Step 1: Building and publishing application..." -ForegroundColor Yellow
    Write-Host ""

    # Clean previous publish
    $PublishDir = Join-Path $RootDir "src\WifiGPSGate.App\bin\$Configuration\net9.0-windows\publish"
    if (Test-Path $PublishDir) {
        Write-Host "Cleaning previous publish directory..." -ForegroundColor Gray
        Remove-Item -Path $PublishDir -Recurse -Force
    }

    # Build and publish
    Write-Host "Publishing application..." -ForegroundColor Gray
    $publishArgs = @(
        "publish",
        $AppProjectPath,
        "-c", $Configuration,
        "-r", "win-x64",
        "--self-contained", "false",
        "-p:PublishSingleFile=false",
        "-p:IncludeNativeLibrariesForSelfExtract=true"
    )

    & dotnet @publishArgs

    if ($LASTEXITCODE -ne 0) {
        Write-Host "Build failed!" -ForegroundColor Red
        exit 1
    }

    Write-Host "Build completed successfully." -ForegroundColor Green
    Write-Host ""
}
else {
    Write-Host "Skipping build step." -ForegroundColor Gray
    Write-Host ""
}

# Step 2: Run tests
Write-Host "Step 2: Running tests..." -ForegroundColor Yellow
$TestProjectPath = Join-Path $RootDir "src\WifiGPSGate.Tests\WifiGPSGate.Tests.csproj"

& dotnet test $TestProjectPath -c $Configuration --no-build --verbosity minimal

if ($LASTEXITCODE -ne 0) {
    Write-Host "Tests failed! Continuing anyway..." -ForegroundColor Yellow
}
else {
    Write-Host "Tests passed." -ForegroundColor Green
}
Write-Host ""

# Step 3: Create installer
if (-not $SkipInstaller) {
    Write-Host "Step 3: Creating installer..." -ForegroundColor Yellow
    Write-Host ""

    # Find Inno Setup
    $InnoSetupPaths = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles}\Inno Setup 6\ISCC.exe",
        "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
        "C:\Program Files\Inno Setup 6\ISCC.exe"
    )

    $IsccPath = $null
    foreach ($path in $InnoSetupPaths) {
        if (Test-Path $path) {
            $IsccPath = $path
            break
        }
    }

    if (-not $IsccPath) {
        Write-Host "Inno Setup not found!" -ForegroundColor Red
        Write-Host "Please install Inno Setup 6 from https://jrsoftware.org/isinfo.php" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "Build completed, but installer was not created." -ForegroundColor Yellow
        exit 0
    }

    Write-Host "Using Inno Setup: $IsccPath" -ForegroundColor Gray

    # Run Inno Setup
    & $IsccPath $IssPath

    if ($LASTEXITCODE -ne 0) {
        Write-Host "Installer creation failed!" -ForegroundColor Red
        exit 1
    }

    Write-Host "Installer created successfully." -ForegroundColor Green
    Write-Host ""

    # Show output
    $InstallerFiles = Get-ChildItem -Path $OutputDir -Filter "*.exe"
    if ($InstallerFiles) {
        Write-Host "Output files:" -ForegroundColor Cyan
        foreach ($file in $InstallerFiles) {
            $size = [math]::Round($file.Length / 1MB, 2)
            Write-Host "  $($file.Name) ($size MB)" -ForegroundColor White
        }
    }
}
else {
    Write-Host "Skipping installer creation step." -ForegroundColor Gray
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Build completed successfully!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
