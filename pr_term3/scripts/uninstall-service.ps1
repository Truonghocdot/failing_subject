#Requires -RunAsAdministrator
<#
.SYNOPSIS
  Dừng và gỡ Windows Service "HomestayBooking" (không xoá thư mục publish, .env, log, ảnh upload hay .eml).
.EXAMPLE
  .\scripts\uninstall-service.ps1
#>
param([string]$ServiceName = 'HomestayBooking')
$ErrorActionPreference = 'Stop'

$service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if (-not $service) { Write-Host "Không có service $ServiceName – không cần gỡ"; exit 0 }
if ($service.Status -ne 'Stopped') { Stop-Service $ServiceName -Force; $service.WaitForStatus('Stopped', '00:00:30') }
sc.exe delete $ServiceName | Out-Null
Write-Host "Đã gỡ service $ServiceName"
