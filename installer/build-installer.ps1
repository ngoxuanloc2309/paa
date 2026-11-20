<#
.SYNOPSIS
    Automated 1-Click Production Build Script for SynaptiX IDE Desktop Installer.
    Standard: Microsoft Visual Studio Code / Git for Windows Reference.
.DESCRIPTION
    1. Terminates any active instances of SynaptixIDE or active installer executables.
    2. Generates high-DPI brand bitmaps via Pillow.
    3. Publishes SimplePLC.Studio to self-contained win-x64 binaries in staging.
    4. Bundles SimplePLC.McuEmulator tool into staging/Tools/.
    5. Compiles official Inno Setup package (LZMA2 Ultra64 Solid Compression):
       installer/dist/SynaptiX_IDE_Setup_v2.15.0.exe
.PARAMETER SkipPublish
    Skip publishing SimplePLC.Studio if staging is already up to date.
#>
param(
    [string]$Version = "",
    [switch]$SkipPublish = $false
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = (Resolve-Path "$scriptDir\..").Path
$originalRepoRoot = $repoRoot
Set-Location $repoRoot

# Workaround for Windows WOW64 32-bit redirection if workspace is inside C:\Windows\System32 (self-hosted runner)
$mappedDrive = $null
if ($repoRoot -like "*\System32\*") {
    $driveLetter = "X:"
    Write-Host "Detected System32 workspace path. Mapping virtual drive $driveLetter to prevent 32-bit WOW64 redirection..." -ForegroundColor Yellow
    subst $driveLetter /D 2>$null
    subst $driveLetter $repoRoot
    $mappedDrive = $driveLetter
    $repoRoot = $driveLetter
    $scriptDir = "$driveLetter\installer"
    Set-Location $repoRoot
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    if (Test-Path "$repoRoot\SimplePLC.Studio\SimplePLC.Studio.csproj") {
        [xml]$proj = Get-Content "$repoRoot\SimplePLC.Studio\SimplePLC.Studio.csproj"
        $Version = $proj.Project.PropertyGroup.Version
    }
    if ([string]::IsNullOrWhiteSpace($Version)) {
        $Version = "2.15.4"
    }
}
$Version = $Version.TrimStart('v')

$stagingDir = "$scriptDir\dist\staging"
$distDir = "$scriptDir\dist"
$targetSetupExe = "$distDir\SynaptiX_IDE_Setup_v$Version.exe"
$issPath = "$scriptDir\SynaptixIDE.iss"
$isccPath = $null

Write-Host "`n=======================================================" -ForegroundColor Cyan
Write-Host " SynaptiX IDE v$Version - Professional Setup Build" -ForegroundColor Cyan
Write-Host " Standard: Microsoft Visual Studio Code / Git for Windows" -ForegroundColor Cyan
Write-Host "=======================================================`n" -ForegroundColor Cyan

# 1. Kill locking processes
Write-Host "[1/5] Ensuring no locking processes are active..." -ForegroundColor Yellow
Get-Process -Name "SynaptixIDE", "SynaptiX_IDE_Setup*", "SynaptiX_Setup*", "SimplePLC.McuEmulator" -ErrorAction SilentlyContinue | ForEach-Object {
    Write-Host "  Terminating active process: $($_.ProcessName) (PID: $($_.Id))" -ForegroundColor Gray
    Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
}
Start-Sleep -Milliseconds 500

# 2. Check / Generate Brand Assets
Write-Host "`n[2/5] Checking installer brand assets..." -ForegroundColor Yellow
if (-not (Test-Path "$scriptDir\wizard_large.bmp") -or -not (Test-Path "$scriptDir\wizard_small.bmp")) {
    Write-Host "  Generating high-DPI installer brand assets via generate_assets.py..." -ForegroundColor Gray
    $pythonCmd = Get-Command "python" -ErrorAction SilentlyContinue
    if ($pythonCmd) {
        & $pythonCmd.Source "$scriptDir\generate_assets.py"
        if ($LASTEXITCODE -ne 0) { throw "Asset generation failed." }
    } else {
        throw "Brand bitmaps not found and Python is not installed to generate them."
    }
} else {
    Write-Host "  Using existing pre-rendered brand assets (wizard_large.bmp, wizard_small.bmp)." -ForegroundColor Green
}

# 3. Publish SimplePLC.Studio (if not skipped)
if (-not $SkipPublish -or -not (Test-Path "$stagingDir\SynaptixIDE.exe")) {
    Write-Host "`n[3/5] Publishing SimplePLC.Studio (Release, win-x64, self-contained)..." -ForegroundColor Yellow
    if (Test-Path $stagingDir) {
        Remove-Item -Path $stagingDir -Recurse -Force -ErrorAction SilentlyContinue
    }
    New-Item -ItemType Directory -Path $stagingDir -Force | Out-Null

    dotnet publish "SimplePLC.Studio\SimplePLC.Studio.csproj" `
        -c Release `
        -r win-x64 `
        --self-contained true `
        -o $stagingDir
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish SimplePLC.Studio failed with exit code $LASTEXITCODE." }

    # Publish McuEmulator into Tools/
    Write-Host "  Publishing SynaptiX MCU Emulator into staging/Tools/..." -ForegroundColor Gray
    $toolsDir = "$stagingDir\Tools"
    New-Item -ItemType Directory -Path $toolsDir -Force | Out-Null
    dotnet publish "tools\SimplePLC.McuEmulator\SimplePLC.McuEmulator.csproj" `
        -c Release `
        -r win-x64 `
        --self-contained true `
        -o $toolsDir
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish SimplePLC.McuEmulator failed with exit code $LASTEXITCODE." }

    # Copy app.ico
    Copy-Item "SimplePLC.Studio\Assets\app.ico" "$stagingDir\app.ico" -Force
} else {
    Write-Host "`n[3/5] Skipping SimplePLC.Studio publish (-SkipPublish specified)." -ForegroundColor Gray
}

# 4. Check for Inno Setup Compiler
Write-Host "`n[4/5] Checking for Inno Setup Compiler (iscc.exe)..." -ForegroundColor Yellow
$isccCandidates = @(
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe",
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "C:\Users\DELL\AppData\Local\Programs\Inno Setup 6\ISCC.exe"
)
$resolvedIscc = $null
$cmd = Get-Command "iscc" -ErrorAction SilentlyContinue
if ($cmd) {
    $resolvedIscc = $cmd.Source
} else {
    foreach ($candidate in $isccCandidates) {
        if (Test-Path $candidate) {
            $resolvedIscc = $candidate
            break
        }
    }
}
if (-not $resolvedIscc) {
    throw "Inno Setup Compiler (iscc.exe) not found. Please install Inno Setup 6 or add ISCC to PATH."
}
$isccPath = $resolvedIscc
Write-Host "  Found Inno Setup Compiler at: $isccPath" -ForegroundColor Green

# 5. Compile Installer Executable
Write-Host "`n[5/5] Compiling release installer executable with LZMA2 Ultra64..." -ForegroundColor Yellow
if (Test-Path $targetSetupExe) {
    Remove-Item $targetSetupExe -Force -ErrorAction SilentlyContinue
}

Write-Host "  Compiling with Inno Setup (Version: $Version)..." -ForegroundColor Gray
try {
    & $isccPath "/DMyAppVersion=$Version" $issPath
    if ($LASTEXITCODE -ne 0) { throw "ISCC compilation failed with exit code $LASTEXITCODE." }
} finally {
    if ($mappedDrive) {
        Write-Host "Unmapping virtual drive $mappedDrive and restoring root..." -ForegroundColor Gray
        Set-Location $originalRepoRoot
        subst $mappedDrive /D 2>$null
        $targetSetupExe = "$originalRepoRoot\installer\dist\SynaptiX_IDE_Setup_v$Version.exe"
    }
}

if (Test-Path $targetSetupExe) {
    $item = Get-Item $targetSetupExe
    $sizeMB = [Math]::Round($item.Length / 1MB, 2)
    $hash = (Get-FileHash -Path $targetSetupExe -Algorithm SHA256).Hash

    Write-Host "`n=======================================================" -ForegroundColor Green
    Write-Host " BUILD SUCCESSFUL: SynaptiX IDE Setup Ready!" -ForegroundColor Green
    Write-Host "=======================================================" -ForegroundColor Green
    Write-Host " Output File: $targetSetupExe" -ForegroundColor Cyan
    Write-Host " Size:        $sizeMB MB ($($item.Length) bytes)" -ForegroundColor Cyan
    Write-Host " SHA-256:     $hash" -ForegroundColor Cyan
    Write-Host " Standard:    Microsoft VS Code Inno Setup 6 (Ultra-Compact)" -ForegroundColor Cyan
    Write-Host "=======================================================`n" -ForegroundColor Green
} else {
    throw "Target installer executable was not found at $targetSetupExe"
}
