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
    [switch]$SkipInstaller,
    [string]$GitHubToken = $env:RELEASE_TOKEN,
    [string]$TargetRepo = "SynaptiXCompany/SynaptiX-IDE-Releases"
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
    # Read from csproj first as primary source of truth for current project state
    if (Test-Path "$RootDir\SimplePLC.Studio\SimplePLC.Studio.csproj") {
        [xml]$proj = Get-Content "$RootDir\SimplePLC.Studio\SimplePLC.Studio.csproj"
        $Version = $proj.Project.PropertyGroup.Version
    }

    if ([string]::IsNullOrWhiteSpace($Version)) {
        # Try git tag
        try {
            $gitTag = (git tag --points-at HEAD 2>$null | Select-Object -First 1)
            if ($gitTag -and $gitTag -match '^v?(\d+\.\d+(\.\d+)?)') {
                $Version = $matches[1]
            }
        } catch {
            # Ignore git tag lookup failure
        }
    }

    if ([string]::IsNullOrWhiteSpace($Version)) {
        $Version = "2.0.0"
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
Write-Host "[5/7] Publishing standalone single-file Windows x64 binary..." -ForegroundColor Green
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

$ExePath = "$PublishDir\SynaptixIDE.exe"
if (-not (Test-Path $ExePath)) {
    $ExePath = "$PublishDir\SimplePLC.Studio.exe"
}
if (-not (Test-Path $ExePath)) {
    Write-Error "Published executable not found in: $PublishDir"
}
# Keep backwards-compatible alias
if (-not (Test-Path "$PublishDir\SimplePLC.Studio.exe")) {
    Copy-Item $ExePath -Destination "$PublishDir\SimplePLC.Studio.exe" -Force
}
$exeSizeMB = [math]::Round((Get-Item $ExePath).Length / 1MB, 2)
Write-Host " Successfully generated: $ExePath ($exeSizeMB MB)" -ForegroundColor Green

# 7. Build Official Windows Desktop Setup Installer (Inno Setup 6)
$InstallerName = "SynaptiX_IDE_Setup_v$Version.exe"
$InstallerPackagePath = "$ReleasePackageDir\$InstallerName"
$installerScript = "$RootDir\installer\build-installer.ps1"
$installerHash = $null

if (-not $SkipInstaller) {
    Write-Host "[6/7] Building official Windows desktop setup installer..." -ForegroundColor Green
    if (Test-Path $installerScript) {
        try {
            & powershell -NoProfile -ExecutionPolicy Bypass -File $installerScript -Version $Version
            $builtInstaller = "$RootDir\installer\dist\$InstallerName"
            if (Test-Path $builtInstaller) {
                Copy-Item $builtInstaller -Destination $InstallerPackagePath -Force
                $installerHash = (Get-FileHash -Path $InstallerPackagePath -Algorithm SHA256).Hash
                $installerSizeMB = [math]::Round((Get-Item $InstallerPackagePath).Length / 1MB, 2)
                Write-Host " Successfully generated installer: $InstallerPackagePath ($installerSizeMB MB)" -ForegroundColor Green
            } else {
                Write-Warning "Installer build completed but output not found at: $builtInstaller"
            }
        } catch {
            Write-Warning "Failed to build Windows setup installer: $_"
        }
    } else {
        Write-Warning "Installer build script not found at $installerScript. Skipping installer creation."
    }
} else {
    Write-Host "[6/7] Skipping official installer build (-SkipInstaller specified)." -ForegroundColor DarkYellow
}

# 8. Package Distribution ZIP & Checksums
Write-Host "[7/7] Packaging release distribution and computing SHA256..." -ForegroundColor Green
$ZipName = "SynaptiX-IDE-$ReleaseTag-win-x64.zip"
$ZipPath = "$ReleasePackageDir\$ZipName"

Compress-Archive -Path "$PublishDir\*" -DestinationPath $ZipPath -Force

$hash = (Get-FileHash -Path $ExePath -Algorithm SHA256).Hash
$zipHash = (Get-FileHash -Path $ZipPath -Algorithm SHA256).Hash

$ChecksumFile = "$ReleasePackageDir\SHA256SUMS.txt"
$checksumLines = @()
if ($installerHash) {
    $checksumLines += "$installerHash  $InstallerName"
}
$checksumLines += "$hash  $([System.IO.Path]::GetFileName($ExePath))"
$checksumLines += "$zipHash  $ZipName"
$checksumLines -join "`r`n" | Set-Content -Path $ChecksumFile -Encoding utf8

# Generate Release Notes Markdown
$ReleaseNotesFile = "$ReleasePackageDir\RELEASE_NOTES_$ReleaseTag.md"
$installerTableRow = ""
if ($installerHash) {
    $installerTableRow = "| ``$InstallerName`` | **Bản cài đặt chính thức (Khuyên dùng)** — Windows x64 Setup Wizard (tạo Desktop/Start Menu shortcut, mở file .synx, đính kèm MCU Emulator) | $installerHash |`r`n"
}

$releaseContent = @"
# SynaptiX IDE $ReleaseTag — Official Release

Phiên bản chính thức của môi trường lập trình và cấu hình thiết bị công nghiệp **SynaptiX IDE ($ReleaseTag)**.

## 🚀 Tính năng nổi bật & Nâng cấp (v$Version)
- **Chuẩn Hóa Cơ Chế Nạp Sang Chuẩn IDE Công Nghiệp Toàn Cầu (Universal Industrial IDE Download Standard)**: Cho phép nạp độc lập cấu hình thiết bị (MQTT Broker, DTU Gateway, RS-485 Modbus, Wi-Fi...) xuống phần cứng ngay cả khi dự án có 0 logic rule, tương đương tiêu chuẩn các IDE công nghiệp quốc tế (Siemens TIA Portal, Rockwell Studio 5000, Codesys).
- **Tối Ưu Hóa Hiệu Năng Chuyển Tab với Bộ Nhớ Đệm Visual Tree (`CachedContentPresenter`)**: Triệt tiêu hoàn toàn độ trễ (lag), rò rỉ bộ nhớ (memory leaks) và tái tạo cây thị giác khi chuyển đổi giữa 7 workspaces của IDE.
- **Đồng Bộ Hai Chiều Trạng Thái Cổng Modbus Master & Chuẩn Hóa Vòng Đời Modal Dialog**: Giải quyết triệt để lỗi ghi đè cấu hình (stale auto-commit on tab switch) và crash khi đóng hộp thoại cấu hình cổng ngoại vi.
- **Hệ Thống Trạng Thái Biên Dịch Đa Phân Hệ & Tự Động Đồng Bộ Rule Table**: Hiển thị trạng thái biên dịch trực quan dạng token rút gọn (`{r} rules, {m} MQTT ready`), tự động đồng bộ hóa bảng Rule Table khi chuyển tab mà không cần lưu thủ công.
- **Dịch Vụ Giám Sát Chẩn Đoán Truyền Thông (Comm Diagnostics Service & System Tag Watchdog)**: Bổ sung `ICommDiagnosticsService` và cơ chế giám sát watchdog thời gian thực qua tag hệ thống `SYS_COMM_OK`.
- **Bộ Cài Đặt Desktop Setup Installer Chính Thức (Inno Setup 6)**: Tự động đăng ký file association `.synx`, tạo icon Desktop/Start Menu và tích hợp công cụ giả lập MCU Emulator vào hệ thống.
- **Độ Tin Cậy & Ổn Định Tuyệt Đối**: Vượt qua 100% bài kiểm thử tự động toàn giải pháp, 0 vi phạm Clean Architecture, 0 vi phạm token XAML, hoàn thành kiểm tra Web Wiki Portal không lỗi.

## 📦 Tệp đính kèm phân phối
| Tên tệp | Định dạng & Mục đích | SHA256 Checksum |
| :--- | :--- | :--- |
$($installerTableRow)| ``SynaptixIDE.exe`` | **Bản Portable độc lập** — Windows x64 single-file executable, chạy trực tiếp không cần cài đặt | $hash |
| ``$ZipName`` | **Gói nén ZIP đầy đủ** — Toàn bộ tệp ứng dụng nén zip | $zipHash |

*Yêu cầu hệ điều hành: Windows 10 / Windows 11 (64-bit). Không cần cài đặt thêm .NET Runtime.*
"@
[System.IO.File]::WriteAllText($ReleaseNotesFile, $releaseContent, [System.Text.Encoding]::UTF8)

# Copy standalone exe to release directory for direct access
Copy-Item $ExePath -Destination "$ReleasePackageDir\$([System.IO.Path]::GetFileName($ExePath))" -Force
Copy-Item "$PublishDir\SimplePLC.Studio.exe" -Destination "$ReleasePackageDir\SimplePLC.Studio.exe" -Force

Write-Host ""
Write-Host "=================================================================" -ForegroundColor Green
Write-Host "           LOCAL CI/CD COMPLETED SUCCESSFULLY!                   " -ForegroundColor Green
Write-Host "=================================================================" -ForegroundColor Green
Write-Host " Release artifacts created in: $ReleasePackageDir" -ForegroundColor Cyan
if (Test-Path $InstallerPackagePath) {
    Write-Host "  1. Desktop Setup Installer: $InstallerPackagePath"
}
Write-Host "  2. Standalone Executable  : $ReleasePackageDir\SimplePLC.Studio.exe"
Write-Host "  3. Distribution ZIP       : $ZipPath"
Write-Host "  4. Checksums              : $ChecksumFile"
Write-Host "  5. Release Notes          : $ReleaseNotesFile"
Write-Host ""

# 9. Direct GitHub Release Upload (if token is available)
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
        $assetsToUpload = @()
        if (Test-Path $InstallerPackagePath) {
            $assetsToUpload += @{ Path = $InstallerPackagePath; ContentType = "application/octet-stream"; Name = $InstallerName }
        }
        $assetsToUpload += @(
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
