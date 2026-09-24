# 创建符号链接脚本
# 此脚本需要在管理员权限下运行

Write-Host "正在创建符号链接..." -ForegroundColor Yellow

$targetPath = Join-Path $PSScriptRoot "..\doc"
$linkPath = Join-Path $PSScriptRoot "doc"

# 检查目标目录是否存在
if (-not (Test-Path $targetPath)) {
    Write-Host "错误: 目标目录不存在: $targetPath" -ForegroundColor Red
    exit 1
}

# 如果链接已存在，先删除
if (Test-Path $linkPath) {
    Write-Host "删除现有链接: $linkPath" -ForegroundColor Yellow
    Remove-Item $linkPath -Force
}

# 创建符号链接
try {
    New-Item -ItemType SymbolicLink -Path $linkPath -Target $targetPath | Out-Null
    Write-Host "✓ 符号链接创建成功: $linkPath -> $targetPath" -ForegroundColor Green
    Write-Host ""
    Write-Host "现在可以启动 Docsify 服务器了:" -ForegroundColor Cyan
    Write-Host "  docsify serve ." -ForegroundColor White
} catch {
    Write-Host "错误: 创建符号链接失败" -ForegroundColor Red
    Write-Host "请以管理员权限运行此脚本" -ForegroundColor Yellow
    Write-Host $_.Exception.Message -ForegroundColor Red
    exit 1
}

