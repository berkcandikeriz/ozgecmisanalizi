<#
.SYNOPSIS
    API (https://localhost:7101) ve MVC arayüzünü (https://localhost:7201) ayrı pencerelerde başlatır.
#>
Set-Location -Path $PSScriptRoot
Start-Process powershell -ArgumentList "-NoExit", "-Command", "dotnet run --project src\OzgecmisAnalizi.API --launch-profile https"
Start-Sleep -Seconds 6
Start-Process powershell -ArgumentList "-NoExit", "-Command", "dotnet run --project src\OzgecmisAnalizi.WebUI --launch-profile https"
Start-Sleep -Seconds 8
Start-Process "https://localhost:7201"
