<#
.SYNOPSIS
    ÖzgeçmişAnalizi ilk kurulum: SDK kontrolü, derleme, EF Core migration, LocalDB, git commit ve push.

.DESCRIPTION
    Betik proje kök klasöründe çalıştırılır. Tüm çıktı kurulum.log dosyasına da yazılır.
    Git bağlantısı yalnızca bu repo için berkcandikeriz kullanıcısıyla yapılandırılır (global ayarlara dokunmaz).

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\kurulum.ps1
    powershell -ExecutionPolicy Bypass -File .\kurulum.ps1 -SkipPush
#>
param(
    [switch]$SkipPush,
    [switch]$SkipCert,
    [string]$GitUser = "berkcandikeriz",
    [string]$GitEmail = "berkdikeriz@hotmail.com",
    [string]$Remote = "https://github.com/berkcandikeriz/ozgecmisanalizi.git"
)

# Yerel komutların stderr çıktısı (git ilerleme mesajları gibi) hata sayılmasın; başarı çıkış koduyla denetlenir.
$ErrorActionPreference = "Continue"
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
Set-Location -Path $PSScriptRoot
$log = Join-Path $PSScriptRoot "kurulum.log"
"=== Kurulum $(Get-Date -Format s) ===" | Out-File $log -Encoding utf8

function Step([string]$text) { $line = "`n>> $text"; Write-Host $line -ForegroundColor Cyan; $line | Out-File $log -Append -Encoding utf8 }
function Info([string]$text) { Write-Host $text; $text | Out-File $log -Append -Encoding utf8 }
function Fail([string]$text) { Write-Host "HATA: $text" -ForegroundColor Red; "HATA: $text" | Out-File $log -Append -Encoding utf8; exit 1 }
function Run([string]$file, [string[]]$arguments, [switch]$AllowFail) {
    $display = "$file $($arguments -join ' ')"
    Info "   $display"
    $output = & $file @arguments 2>&1
    $code = $LASTEXITCODE
    $output | ForEach-Object { $_.ToString() } | Tee-Object -FilePath $log -Append | Out-Host
    if ($code -ne 0 -and -not $AllowFail) { Fail "'$display' başarısız oldu (çıkış kodu $code). Ayrıntılar: kurulum.log" }
    return $code
}

Step "1/7 .NET SDK kontrolü"
$sdks = (& dotnet --list-sdks 2>$null) -join "`n"
Info $sdks
if ($sdks -notmatch '(?m)^(1[0-9])\.') { Fail ".NET 10 SDK bulunamadı. https://dotnet.microsoft.com/download/dotnet/10.0 adresinden kurun ve betiği tekrar çalıştırın." }

Step "2/7 Araçlar ve paketler"
Run "dotnet" @("tool", "restore") | Out-Null
Run "dotnet" @("restore", "OzgecmisAnalizi.slnx") | Out-Null

Step "3/7 Derleme"
Run "dotnet" @("build", "OzgecmisAnalizi.slnx", "-c", "Debug", "--no-restore", "-nologo", "-clp:ErrorsOnly;Summary") | Out-Null

Step "4/7 EF Core migration (code-first)"
$migrationDir = "src\OzgecmisAnalizi.DataAccess\Migrations"
$existing = Get-ChildItem $migrationDir -Filter *.cs -ErrorAction SilentlyContinue
if (-not $existing) {
    Run "dotnet" @("ef", "migrations", "add", "InitialCreate", "--project", "src\OzgecmisAnalizi.DataAccess", "--startup-project", "src\OzgecmisAnalizi.API", "--output-dir", "Migrations", "--no-build") | Out-Null
    Run "dotnet" @("build", "OzgecmisAnalizi.slnx", "-c", "Debug", "--no-restore", "-nologo", "-clp:ErrorsOnly;Summary") | Out-Null
} else {
    Info "   Migration zaten var: $($existing.Count) dosya"
}

Step "5/7 Veritabanı (SQL Server LocalDB)"
$localdb = Get-Command sqllocaldb -ErrorAction SilentlyContinue
if ($localdb) {
    Run "sqllocaldb" @("create", "MSSQLLocalDB") -AllowFail | Out-Null
    Run "sqllocaldb" @("start", "MSSQLLocalDB") -AllowFail | Out-Null
    Run "dotnet" @("ef", "database", "update", "--project", "src\OzgecmisAnalizi.DataAccess", "--startup-project", "src\OzgecmisAnalizi.API", "--no-build") -AllowFail | Out-Null
    Info "   Meslek kataloğu API ilk açıldığında otomatik yüklenir."
} else {
    Info "   LocalDB bulunamadı. src\OzgecmisAnalizi.API\appsettings.json içindeki ConnectionStrings:Default değerini kendi SQL Server'ınıza göre düzenleyin."
}
if (-not $SkipCert) {
    Run "dotnet" @("dev-certs", "https", "--trust") -AllowFail | Out-Null
}

Step "6/7 Git deposu ($GitUser)"
if (-not (Test-Path ".git")) { Run "git" @("init", "-b", "main") | Out-Null }
Run "git" @("config", "--local", "user.name", $GitUser) | Out-Null
Run "git" @("config", "--local", "user.email", $GitEmail) | Out-Null
Run "git" @("config", "--local", "credential.https://github.com.username", $GitUser) | Out-Null
Run "git" @("config", "--local", "core.autocrlf", "true") | Out-Null
$remoteUrl = $Remote -replace '^https://', "https://$GitUser@"
$hasOrigin = (& git remote) -contains "origin"
if ($hasOrigin) { Run "git" @("remote", "set-url", "origin", $remoteUrl) | Out-Null } else { Run "git" @("remote", "add", "origin", $remoteUrl) | Out-Null }

Run "git" @("add", "-A") | Out-Null
$hasHead = (& git rev-parse --verify HEAD 2>$null)
if (-not $hasHead) {
    $msgFile = Join-Path $env:TEMP "ozgecmis-commit.txt"
    $commitText = @"
İlk sürüm: katmanlı ÖzgeçmişAnalizi platformu

- Core: global hata yönetimi, korelasyon kimliği, Serilog, extension ve araçlar
- Entities: varlıklar, enum'lar, istemcilerle paylaşılan DTO'lar
- DataAccess: EF Core code-first, migration, repository + UnitOfWork, 68 meslekli seed
- Business: CV ayrıştırma (PDF/DOCX), kural tabanlı analiz, ATS kontrolleri,
  eksik beceri, maaş tahmini, kariyer yolu, PDF şablonları, iş arama, ön yazı, AI sağlayıcıları
- API: JWT, OpenAPI/Scalar, rate limit, CORS
- WebUI: ASP.NET Core MVC, açık/koyu tema, responsive tasarım sistemi

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01SMf6BbZR32qVNJ75dcyDDk
"@
    [System.IO.File]::WriteAllText($msgFile, $commitText, (New-Object System.Text.UTF8Encoding($false)))
    Run "git" @("commit", "-F", $msgFile) | Out-Null
} else {
    $pending = (& git status --porcelain)
    if ($pending) { Run "git" @("commit", "-m", "Kurulum çıktıları (migration)") | Out-Null } else { Info "   Commit edilecek değişiklik yok." }
}

Step "7/7 Push"
if ($SkipPush) { Info "   -SkipPush verildi; push atlandı."; exit 0 }
$remoteHeads = (& git ls-remote --heads origin 2>&1) -join "`n"
Info $remoteHeads
if ($LASTEXITCODE -ne 0) { Fail "Uzak depoya ulaşılamadı. GitHub oturumunu '$GitUser' hesabıyla açtığınızdan emin olun." }
if ($remoteHeads -match 'refs/heads/main') {
    Info "   Uzak depoda main dalı var; geçmişler birleştiriliyor (çakışmada yerel dosyalar korunur)."
    Run "git" @("fetch", "origin", "main") | Out-Null
    Run "git" @("merge", "origin/main", "--allow-unrelated-histories", "-X", "ours", "-m", "Uzak depo geçmişiyle birleştir") | Out-Null
}
Run "git" @("push", "-u", "origin", "main") | Out-Null
Info "`nTamamlandı. Uygulamayı başlatmak için: .\calistir.ps1"
