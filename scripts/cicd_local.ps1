<#
.SYNOPSIS
    SynaptiX IDE Local CI/CD Pipeline & Release Automation.
.DESCRIPTION
    Executes complete end-to-end CI/CD pipeline locally:
    1. Version detection from git tag or SimplePLC.Studio.csproj
    2. Clean previous build artifacts
    3. Restore NuGet dependencies
    4. Compile solution in Release configuration
    5. Run all 694+ automated unit tests (aborts if any test fails)
    6. Publish standalone single-file Windows x64 executable (SimplePLC.Studio.exe)
    7. Create distribution ZIP package and SHA256 checksum
    8. Generate Release Notes
    9. Optional: Upload to GitHub Releases if token provided
#>
[CmdletBinding()]
param(
    [string]$Version = "",
    [switch]$SkipTests,
    [string]$GitHubToken = $env:RELEASE_TOKEN,
    [string]$TargetRepo = "hoanv-synaptix/SynaptiX-IDE-Releases"
)

$ErrorActionPreference = "Stop"
$RootDir = $PSScriptRoot
if (!(Test-Path "$RootDir\SimplePLC.sln")) {
    $RootDir = Split-Path -Parent $PSScriptRoot
}
if (!(Test-Path "$RootDir\SimplePLC.sln")) {
    $RootDir = Get-Location
}
Set-Location $RootDir

Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host "       SYNAPTIX IDE - LOCAL CI/CD & RELEASE AUTOMATION           " -ForegroundColor Cyan
Write-Host "=================================================================" -ForegroundColor Cyan

# 1. Determine Version
if ([string]::IsNullOrWhiteSpace($Version)) {
    # Try git tag
    $gitTag = & git describe --tags --exact-match 2>$null
    if ($LASTEXITCODE -eq 0 -and $gitTag -match '^v?(\d+\.\d+(\.\d+)?)') {
        $Version = $matches[1]
    } else {
        # Read from csproj
        [xml]$proj = Get-Content "$RootDir\SimplePLC.Studio\SimplePLC.Studio.csproj"
        $Version = $proj.Project.PropertyGroup.Version
        if ([string]::IsNullOrWhiteSpace($Version)) {
            $Version = "2.0.0"
        }
    }
}
$Version = $Version.TrimStart('v')
$AssemblyVersion = "$Version.0"
$ReleaseTag = "v$Version"

Write-Host " Target Release Version : $Version" -ForegroundColor Yellow
Write-Host " Assembly Version        : $AssemblyVersion" -ForegroundColor Yellow
Write-Host " Git Tag                 : $ReleaseTag" -ForegroundColor Yellow
Write-Host " Root Directory          : $RootDir" -ForegroundColor Yellow
Write-Host ""

$PublishDir = "$RootDir\publish\win-x64"
$ReleasePackageDir = "$RootDir\publish\release"

# 2. Clean
Write-Host "[1/6] Cleaning previous publish artifacts..." -ForegroundColor Green
if (Test-Path $PublishDir) {
    Remove-Item -Recurse -Force $PublishDir -ErrorAction SilentlyContinue
}
if (Test-Path $ReleasePackageDir) {
    Remove-Item -Recurse -Force $ReleasePackageDir -ErrorAction SilentlyContinue
}
New-Item -ItemType Directory -Force -Path $PublishDir | Out-Null
New-Item -ItemType Directory -Force -Path $ReleasePackageDir | Out-Null

# 3. Restore
Write-Host "[2/6] Restoring dependencies..." -ForegroundColor Green
& dotnet restore "$RootDir\SimplePLC.sln"
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to restore dependencies."
}

# 4. Build Release
Write-Host "[3/6] Building solution in Release configuration..." -ForegroundColor Green
& dotnet build "$RootDir\SimplePLC.sln" --configuration Release --no-restore
if ($LASTEXITCODE -ne 0) {
    Write-Error "Release build failed."
}

# 5. Automated Tests
if (-not $SkipTests) {
    Write-Host "[4/6] Running automated unit tests..." -ForegroundColor Green
    & dotnet test "$RootDir\SimplePLC.sln" --configuration Release --no-build --verbosity normal
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Unit tests failed! Aborting release."
    }
} else {
    Write-Host "[4/6] Skipping automated unit tests (-SkipTests specified)." -ForegroundColor DarkYellow
}

# 6. Publish Single-File Executable
Write-Host "[5/6] Publishing standalone single-file Windows x64 binary..." -ForegroundColor Green
& dotnet publish "$RootDir\SimplePLC.Studio\SimplePLC.Studio.csproj" `
    --configuration Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:Version=$Version `
    -p:AssemblyVersion=$AssemblyVersion `
    -p:FileVersion=$AssemblyVersion `
    -o $PublishDir

if ($LASTEXITCODE -ne 0) {
    Write-Error "Publish failed."
}

$ExePath = "$PublishDir\SimplePLC.Studio.exe"
if (-not (Test-Path $ExePath)) {
    Write-Error "Published executable not found at: $ExePath"
}
$exeSizeMB = [math]::Round((Get-Item $ExePath).Length / 1MB, 2)
Write-Host " Successfully generated: $ExePath ($exeSizeMB MB)" -ForegroundColor Green

# 7. Package Distribution ZIP & Checksums
Write-Host "[6/6] Packaging release distribution and computing SHA256..." -ForegroundColor Green
$ZipName = "SynaptiX-IDE-$ReleaseTag-win-x64.zip"
$ZipPath = "$ReleasePackageDir\$ZipName"

Compress-Archive -Path "$PublishDir\*" -DestinationPath $ZipPath -Force

$hash = (Get-FileHash -Path $ExePath -Algorithm SHA256).Hash
$zipHash = (Get-FileHash -Path $ZipPath -Algorithm SHA256).Hash

$ChecksumFile = "$ReleasePackageDir\SHA256SUMS.txt"
@"
$hash  SimplePLC.Studio.exe
$zipHash  $ZipName
"@ | Set-Content -Path $ChecksumFile -Encoding utf8

# Generate Release Notes Markdown
$ReleaseNotesFile = "$ReleasePackageDir\RELEASE_NOTES_$ReleaseTag.md"
$releaseContent = @"
# SynaptiX IDE $ReleaseTag — Official Release

Phiên bản chính thức của môi trường lập trình và cấu hình thiết bị công nghiệp **SynaptiX IDE ($ReleaseTag)**.

## 🚀 Tính năng nổi bật & Nâng cấp (v2.2.1)
- **Khởi động sạch mặc định (Clean Canvas Startup)**: Ứng dụng luôn khởi động với không gian làm việc sạch (0 nodes, 0 rules), tối ưu hóa trải nghiệm bắt đầu dự án mới.
- **Scale Node Function Block (y = k * x + b)**: Chuẩn hóa khối nhân rộng giá trị analog tuyến tính với kiểm toán an toàn chia 0 và clamping.
- **Tách ranh giới Clean Architecture**: Tái cấu trúc `IComPortDiscoveryService` loại bỏ trực tiếp `System.IO.Ports` khỏi tầng ViewModel.
- **Tokenize Giao diện Công nghiệp**: Chuẩn hóa toàn bộ Font Size theo hệ Design Token `IndustrialFontSize*`.
- **0 Warning - 0 Error**: Khắc phục triệt để các cảnh báo null-safety CS8602, sẵn sàng cho quy chuẩn TreatWarningsAsErrors.
- **IEC 61131-3 Macros & Wire Contract V2.0**: Hỗ trợ đầy đủ các khối TON, TOF, TP, CTU, CTD, RTC và giao thức MCU Conformance Spec V2.0.

## 📦 Tệp đính kèm phân phối
| Tên tệp | Mô tả | SHA256 Checksum |
| :--- | :--- | :--- |
| `SimplePLC.Studio.exe` | Portable single-file x64 standalone executable | `$hash` |
| `$ZipName` | Toàn bộ gói ứng dụng nén zip | `$zipHash` |

*Yêu cầu hệ điều hành: Windows 10 / Windows 11 (64-bit). Không cần cài đặt thêm .NET Runtime.*
"@
[System.IO.File]::WriteAllText($ReleaseNotesFile, $releaseContent, [System.Text.Encoding]::UTF8)

# Copy standalone exe to release directory for direct access
Copy-Item $ExePath -Destination "$ReleasePackageDir\SimplePLC.Studio.exe" -Force

Write-Host ""
Write-Host "=================================================================" -ForegroundColor Green
Write-Host "           LOCAL CI/CD COMPLETED SUCCESSFULLY!                   " -ForegroundColor Green
Write-Host "=================================================================" -ForegroundColor Green
Write-Host " Release artifacts created in: $ReleasePackageDir" -ForegroundColor Cyan
Write-Host "  1. Standalone Executable : $ReleasePackageDir\SimplePLC.Studio.exe"
Write-Host "  2. Distribution ZIP      : $ZipPath"
Write-Host "  3. Checksums             : $ChecksumFile"
Write-Host "  4. Release Notes         : $ReleaseNotesFile"
Write-Host ""

# 8. Direct GitHub Release Upload (if token is available)
if (-not [string]::IsNullOrWhiteSpace($GitHubToken)) {
    Write-Host "Attempting direct GitHub Release upload to $TargetRepo..." -ForegroundColor Yellow
    try {
        $headers = @{
            "Authorization" = "token $GitHubToken"
            "Accept"        = "application/vnd.github.v3+json"
            "User-Agent"    = "SynaptiX-Local-CICD"
        }

        # Check existing release
        $getReleaseUrl = "https://api.github.com/repos/$TargetRepo/releases/tags/$ReleaseTag"
        $existingRelease = $null
        try {
            $existingRelease = Invoke-RestMethod -Uri $getReleaseUrl -Headers $headers -Method Get -ErrorAction Stop
        } catch {
            # Release doesn't exist yet
        }

        $releaseBody = Get-Content $ReleaseNotesFile -Raw -Encoding utf8
        if ($null -eq $existingRelease) {
            Write-Host "Creating new GitHub Release: $ReleaseTag..." -ForegroundColor Cyan
            $createPayload = @{
                tag_name         = $ReleaseTag
                target_commitish = "main"
                name             = "SynaptiX IDE $ReleaseTag"
                body             = $releaseBody
                draft            = $false
                prerelease       = $false
            } | ConvertTo-Json

            $releaseObj = Invoke-RestMethod -Uri "https://api.github.com/repos/$TargetRepo/releases" `
                -Headers $headers -Method Post -Body $createPayload -ContentType "application/json; charset=utf-8"
        } else {
            Write-Host "Found existing release for $ReleaseTag (ID: $($existingRelease.id))." -ForegroundColor Cyan
            $releaseObj = $existingRelease
        }

        # Upload Assets
        $uploadUrlTemplate = $releaseObj.upload_url -replace '\{\?name,label\}', ''
        $assetsToUpload = @(
            @{ Path = "$ReleasePackageDir\SimplePLC.Studio.exe"; ContentType = "application/octet-stream"; Name = "SimplePLC.Studio.exe" },
            @{ Path = $ZipPath; ContentType = "application/zip"; Name = $ZipName },
            @{ Path = $ChecksumFile; ContentType = "text/plain"; Name = "SHA256SUMS.txt" }
        )

        foreach ($asset in $assetsToUpload) {
            Write-Host "Uploading asset: $($asset.Name)..." -ForegroundColor Cyan
            # Delete existing asset if it exists
            if ($releaseObj.assets) {
                $existingAsset = $releaseObj.assets | Where-Object { $_.name -eq $asset.Name }
                if ($existingAsset) {
                    Write-Host "Deleting old asset: $($asset.Name)..." -ForegroundColor DarkGray
                    Invoke-RestMethod -Uri $existingAsset.url -Headers $headers -Method Delete | Out-Null
                }
            }

            $bytes = [System.IO.File]::ReadAllBytes($asset.Path)
            $uploadUri = "$uploadUrlTemplate?name=$($asset.Name)"
            $uploadHeaders = @{
                "Authorization" = "token $GitHubToken"
                "Content-Type"  = $asset.ContentType
                "User-Agent"    = "SynaptiX-Local-CICD"
            }
            $null = Invoke-RestMethod -Uri $uploadUri -Headers $uploadHeaders -Method Post -Body $bytes
            Write-Host "Uploaded: $($asset.Name) successfully!" -ForegroundColor Green
        }

        Write-Host "Release published successfully to GitHub: https://github.com/$TargetRepo/releases/tag/$ReleaseTag" -ForegroundColor Green
    } catch {
        Write-Warning "Failed to upload to GitHub Releases automatically: $_"
        Write-Host "You can still manually upload the files from '$ReleasePackageDir' to https://github.com/$TargetRepo/releases" -ForegroundColor Yellow
    }
} else {
    Write-Host "Note: No GitHub token provided. To automatically upload to GitHub Releases," -ForegroundColor DarkYellow
    Write-Host "set \$env:RELEASE_TOKEN = '<your_github_token>' and re-run with: .\cicd_local.bat" -ForegroundColor DarkYellow
}
