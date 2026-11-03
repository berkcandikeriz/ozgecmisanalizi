<#
.SYNOPSIS
    API (https://localhost:7101) ve MVC arayüzünü (https://localhost:7201) ayrı pencerelerde başlatır.
.PARAMETER Mobil
    Android uygulaması için arayüzü yerel ağda (http://<bilgisayar-ip>:5201) yayınlar.
    Telefon ile bilgisayar aynı Wi-Fi ağında olmalıdır.
.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\calistir.ps1 -Mobil
#>
param([switch]$Mobil)

Set-Location -Path $PSScriptRoot
Start-Process powershell -ArgumentList "-NoExit", "-Command", "dotnet run --project src\OzgecmisAnalizi.API --launch-profile https"
Start-Sleep -Seconds 6

if (-not $Mobil) {
    Start-Process powershell -ArgumentList "-NoExit", "-Command", "dotnet run --project src\OzgecmisAnalizi.WebUI --launch-profile https"
    Start-Sleep -Seconds 8
    Start-Process "https://localhost:7201"
    return
}

# Windows Güvenlik Duvarı: 5201 portuna yerel ağdan gelen bağlantılara izin ver (yönetici yetkisi gerekir).
$ruleName = "OzgecmisAnalizi Mobil (5201)"
try {
    if (-not (Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue)) {
        New-NetFirewallRule -DisplayName $ruleName -Direction Inbound -Protocol TCP -LocalPort 5201 -Action Allow -Profile Private, Domain -ErrorAction Stop | Out-Null
        Write-Host "Güvenlik duvarı kuralı eklendi: $ruleName" -ForegroundColor Green
    }
}
catch {
    Write-Host "Güvenlik duvarı kuralı eklenemedi (PowerShell'i yönetici olarak açın veya 5201 portunu elle açın)." -ForegroundColor Yellow
}

Start-Process powershell -ArgumentList "-NoExit", "-Command", "dotnet run --project src\OzgecmisAnalizi.WebUI --launch-profile mobil"
Start-Sleep -Seconds 8

$ips = Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue |
    Where-Object { $_.IPAddress -notlike "127.*" -and $_.IPAddress -notlike "169.254.*" -and $_.PrefixOrigin -ne "WellKnown" } |
    Select-Object -ExpandProperty IPAddress

Write-Host ""
Write-Host "Android uygulamasında 'Sunucu adresi' alanına aşağıdakilerden birini girin:" -ForegroundColor Cyan
foreach ($ip in $ips) { Write-Host "    http://${ip}:5201" -ForegroundColor White }
Write-Host ""
Write-Host "Telefon ile bu bilgisayar aynı Wi-Fi ağında olmalıdır. Ağ profili 'Özel' (Private) olmalıdır."
