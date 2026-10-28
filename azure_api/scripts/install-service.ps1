#Requires -RunAsAdministrator
<#
.SYNOPSIS
  Đăng ký backend (bản publish) thành Windows Service "SoChiTieu" – tự chạy khi bật máy, tự khởi động lại khi lỗi.
.EXAMPLE
  # PowerShell "Run as Administrator", tại thư mục azure_api:
  .\scripts\install-service.ps1
  .\scripts\install-service.ps1 -PublishDir D:\SoChiTieu
#>
param(
    [string]$PublishDir = (Join-Path $PSScriptRoot '..\backend\publish'),
    [string]$ServiceName = 'SoChiTieu',
    # Tài khoản ít quyền của Windows; chỉ được đọc thư mục publish và ghi thư mục logs
    [string]$Account = 'NT AUTHORITY\LocalService',
    [int]$Port = 5090
)
$ErrorActionPreference = 'Stop'

$PublishDir = (Resolve-Path $PublishDir).Path
$exe = Join-Path $PublishDir 'backend.exe'
$envFile = Join-Path $PublishDir '.env'
if (-not (Test-Path $exe)) { throw "Không thấy $exe – chạy trước: cd backend; dotnet publish -c Release -o publish" }
if (-not (Test-Path $envFile)) {
    throw "Không thấy $envFile – copy backend\.env.example thành publish\.env và điền mật khẩu DB + Jwt__Secret (xem docs/07-van-hanh.md)"
}
if (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue) {
    throw "Service $ServiceName đã tồn tại – gỡ trước bằng .\scripts\uninstall-service.ps1"
}

# Quyền thư mục: đọc + chạy bản publish, ghi log; .env chứa bí mật → chỉ Administrators, SYSTEM và tài khoản service
$logs = Join-Path $PublishDir 'logs'
New-Item -ItemType Directory -Force $logs | Out-Null
icacls $PublishDir /grant "${Account}:(OI)(CI)RX" | Out-Null
icacls $logs /grant "${Account}:(OI)(CI)M" | Out-Null
icacls $envFile /inheritance:r /grant:r "Administrators:F" "SYSTEM:F" "${Account}:R" | Out-Null

New-Service -Name $ServiceName -BinaryPathName "`"$exe`"" -DisplayName 'Sổ Chi Tiêu' `
    -Description 'Quản lý chi tiêu cá nhân – API + giao diện tại http://localhost:5090' -StartupType Automatic | Out-Null
sc.exe config $ServiceName obj= $Account password= '' | Out-Null
# Chờ mạng / Docker (MySQL) lên trước; lỗi thì tự khởi động lại sau 5 giây, 5 giây, 1 phút
sc.exe config $ServiceName start= delayed-auto | Out-Null
sc.exe failure $ServiceName reset= 86400 actions= restart/5000/restart/5000/restart/60000 | Out-Null

# Luôn chạy Production dù .env ghi gì (biến môi trường của service thắng giá trị trong .env)
Set-ItemProperty "HKLM:\SYSTEM\CurrentControlSet\Services\$ServiceName" -Name Environment `
    -Type MultiString -Value @('ASPNETCORE_ENVIRONMENT=Production')

Start-Service $ServiceName
Write-Host "Đã cài và khởi động $ServiceName (tài khoản $Account). Kiểm tra:"
for ($i = 0; $i -lt 20; $i++) {
    try {
        $health = Invoke-RestMethod "http://localhost:$Port/health" -TimeoutSec 2
        Write-Host "  /health → status=$($health.status), database=$($health.database)"
        Write-Host "  Mở http://localhost:$Port – log ở $logs"
        exit 0
    } catch { Start-Sleep -Seconds 1 }
}
Write-Warning "Service chưa trả lời /health – xem log ở $logs và Event Viewer (Windows Logs > Application)"
exit 1
