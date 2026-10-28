#Requires -RunAsAdministrator
<#
.SYNOPSIS
  Dừng và gỡ Windows Service "SoChiTieu" (không xoá thư mục publish, .env hay log).
.EXAMPLE
  .\scripts\uninstall-service.ps1
#>
param([string]$ServiceName = 'SoChiTieu')
$ErrorActionPreference = 'Stop'

$service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if (-not $service) { Write-Host "Không có service $ServiceName – không cần gỡ"; exit 0 }
if ($service.Status -ne 'Stopped') { Stop-Service $ServiceName -Force; $service.WaitForStatus('Stopped', '00:00:30') }
sc.exe delete $ServiceName | Out-Null
Write-Host "Đã gỡ service $ServiceName"
