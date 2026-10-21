<#
.SYNOPSIS
    Kịch bản tự động hóa kiểm thử và phát hành phiên bản mới SynaptiX IDE dành cho Tech Lead.

.DESCRIPTION
    1. Kiểm tra trạng thái git working tree sạch sẽ.
    2. Chạy toàn bộ 1,336+ bài kiểm thử tự động (Release mode).
    3. Tạo git tag 'vX.Y.Z' và push lên remote origin.
    4. Kích hoạt GitHub Actions CI/CD tự động build standalone .exe và đẩy sang SynaptiX-IDE-Releases.

.EXAMPLE
    .\tools\create-release.ps1 -Version "2.7.0" -Notes "Unified Communication Blocks & Modbus Enhancements"
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, HelpMessage = "Phiên bản phát hành (ví dụ: 2.7.0 hoặc v2.7.0)")]
    [string]$Version,

    [Parameter(Mandatory = $false)]
    [string]$Notes = "",

    [Parameter(Mandatory = $false)]
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"

# Chuẩn hóa tiền tố 'v'
if (-not $Version.StartsWith("v", [System.StringComparison]::OrdinalIgnoreCase)) {
    $Tag = "v$Version"
} else {
    $Tag = $Version
}

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "🚀 SYNAPTIX IDE — AUTOMATED RELEASE PIPELINE" -ForegroundColor Cyan
Write-Host "Tag mục tiêu: $Tag" -ForegroundColor Yellow
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Kiểm tra git working tree
Write-Host "`n[1/4] Kiểm tra trạng thái Git..." -ForegroundColor Cyan
$gitStatus = git status --porcelain
if ($gitStatus) {
    Write-Warning "Working tree còn các tệp chưa commit:"
    git status -s
    $confirm = Read-Host "Bạn có muốn tiếp tục gắn tag phát hành không? (y/N)"
    if ($confirm -ne 'y' -and $confirm -ne 'Y') {
        Write-Host "Đã hủy tiến trình phát hành." -ForegroundColor Red
        exit 1
    }
} else {
    Write-Host "Working tree sạch sẽ. Hợp lệ." -ForegroundColor Green
}

# 2. Kiểm thử tự động
if (-not $SkipTests) {
    Write-Host "`n[2/4] Đang chạy kiểm thử tự động toàn bộ Solution (Release mode)..." -ForegroundColor Cyan
    dotnet test SimplePLC.sln --configuration Release --verbosity minimal
    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Kiểm thử thất bại! Hủy bỏ phát hành để đảm bảo an toàn sản phẩm." -ForegroundColor Red
        exit 1
    }
    Write-Host "✅ Toàn bộ Unit Tests vượt qua 100%!" -ForegroundColor Green
} else {
    Write-Warning "`n[2/4] ĐÃ BỎ QUA KIỂM THỬ TỰ ĐỘNG THEO YÊU CẦU (-SkipTests)."
}

# 3. Tạo Git Tag
Write-Host "`n[3/4] Đang tạo Git Tag $Tag..." -ForegroundColor Cyan
$existingTag = git tag -l $Tag
if ($existingTag) {
    Write-Host "❌ Tag '$Tag' đã tồn tại trong repository! Vui lòng chọn số phiên bản khác." -ForegroundColor Red
    exit 1
}

$commitMsg = if ($Notes) { "Release $Tag: $Notes" } else { "Release $Tag" }
git tag -a $Tag -m $commitMsg
Write-Host "✅ Đã tạo tag nội bộ thành công: $Tag" -ForegroundColor Green

# 4. Push Tag lên GitHub
Write-Host "`n[4/4] Đang đẩy Tag lên GitHub (origin)..." -ForegroundColor Cyan
git push origin $Tag
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Lỗi khi push tag lên GitHub remote. Vui lòng kiểm tra kết nối mạng." -ForegroundColor Red
    exit 1
}

Write-Host "`n==========================================================" -ForegroundColor Green
Write-Host "🎉 PHÁT HÀNH THÀNH CÔNG!" -ForegroundColor Green
Write-Host "Tag '$Tag' đã được đẩy lên GitHub." -ForegroundColor Green
Write-Host "GitHub Actions CI/CD đang tự động:" -ForegroundColor Yellow
Write-Host "  1. Biên dịch file SimplePLC.Studio.exe (Standalone Single-File x64)."
Write-Host "  2. Đẩy gói phát hành sang repo: hoanv-synaptix/SynaptiX-IDE-Releases"
Write-Host "Theo dõi tiến trình tại: https://github.com/SynaptiXCompany/AppPLCSimple/actions" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Green
