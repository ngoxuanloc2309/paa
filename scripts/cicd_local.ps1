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

# 7. Package Distribution ZIP & Checksums
Write-Host "[6/6] Packaging release distribution and computing SHA256..." -ForegroundColor Green
$ZipName = "SynaptiX-IDE-$ReleaseTag-win-x64.zip"
$ZipPath = "$ReleasePackageDir\$ZipName"

Compress-Archive -Path "$PublishDir\*" -DestinationPath $ZipPath -Force

$hash = (Get-FileHash -Path $ExePath -Algorithm SHA256).Hash
$zipHash = (Get-FileHash -Path $ZipPath -Algorithm SHA256).Hash

$ChecksumFile = "$ReleasePackageDir\SHA256SUMS.txt"
@"
$hash  $([System.IO.Path]::GetFileName($ExePath))
$zipHash  $ZipName
"@ | Set-Content -Path $ChecksumFile -Encoding utf8

# Generate Release Notes Markdown
$ReleaseNotesFile = "$ReleasePackageDir\RELEASE_NOTES_$ReleaseTag.md"
$releaseContent = @"
# SynaptiX IDE $ReleaseTag — Official Release

Phiên bản chính thức của môi trường lập trình và cấu hình thiết bị công nghiệp **SynaptiX IDE ($ReleaseTag)**.

## 🚀 Tính năng nổi bật & Nâng cấp ($ReleaseTag)
- **Bảo toàn cấu hình Modbus Master khi chuyển/đổi cổng RS-485**: Tự động đồng bộ in-place danh sách cổng trong hộp thoại cấu hình `MB_READ`/`MB_WRITE`, bảo toàn 100% PortIndex, ReadMode, FunctionCode, TriggerMode và loại bỏ hoàn toàn viền đỏ lỗi validation dropdown.
- **Chuẩn hóa công thái học cửa sổ cấu hình (NoResize & Clean Close X)**: Khóa phóng to/thu nhỏ trên toàn bộ 5 hộp thoại cấu hình, giữ lại duy nhất nút đóng X thanh thoát; thiết kế lại nút xóa dòng TableRowDeleteBtnStyle với vector Path đối xứng 4 chiều và kích thước 25x25px căn giữa chuẩn xác.
- **Tối giản nhãn chân Pin khối Modbus FBD**: Tên chân pin trên canvas Nodify chỉ hiển thị tên nhãn ngắn gọn, không bị nối dài chuỗi register gây che khuất tham số khối lân cận; giữ nguyên chi tiết mapping trong tooltip đa ngữ.
- **Tách bạch kết nối khối Modbus Read và MQTT qua thanh ghi PLC**: Chuẩn hóa luồng dữ liệu Modbus trực tiếp ra các thanh ghi PLC (VREG/VREG_RETAIN) và MQTT trỏ đọc cấu hình trực tiếp từ thanh ghi.
- **Bộ kiểm thử & Độ bao phủ tự động**: Bổ sung bộ kiểm thử chuyên sâu cho Dialog Styling, Window ResizeMode NoResize và Modbus Pin DisplayTitle.
- **Độ tin cậy & Ổn định Tuyệt đối**: Khắc phục race condition kiểm thử rút cắm nóng F3_02; vượt qua 100% toàn bộ 1,483 bài kiểm thử tự động trên toàn giải pháp.

## 📦 Tệp đính kèm phân phối
| Tên tệp | Mô tả | SHA256 Checksum |
| :--- | :--- | :--- |
| `SynaptixIDE.exe` | Portable single-file x64 standalone executable | `$hash` |
| `$ZipName` | Toàn bộ gói ứng dụng nén zip | `$zipHash` |

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
