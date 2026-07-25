param(
    [string]$ScriptDir = ""
)

# Gerekli assembly'leri yukle
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

# Eger ScriptDir argumani bos geldiyse, scriptin bulundugu klasorden yukarı dogru bul
if ([string]::IsNullOrEmpty($ScriptDir)) {
    $ScriptDir = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
}

$ScriptDir = $ScriptDir.Replace('"', '').TrimEnd('\')

Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "     NIGHTWATCH TEK TUSLA VERITABANI GUNCELLEME YONETICISI" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host ""

# 1. Kullanicidan Dumper Cikti Klasorunu Secmesini Iste
$FolderBrowser = New-Object System.Windows.Forms.FolderBrowserDialog
$FolderBrowser.Description = "Lütfen Dumper Çıktı Klasörünü Seçin (Örn: C:\output)"
$FolderBrowser.SelectedPath = "C:\output"
$FolderBrowser.ShowNewFolderButton = $false

$result = $FolderBrowser.ShowDialog()
if ($result -ne [System.Windows.Forms.DialogResult]::OK) {
    Write-Host "[-] Seçim iptal edildi. Guncelleme sonlandiriliyor." -ForegroundColor Yellow
    exit 0
}

$SelectedFolder = $FolderBrowser.SelectedPath
Write-Host "[+] Secilen Klasor: $SelectedFolder" -ForegroundColor Green

# 2. Secilen klasor icinde gerekli JSON dosyalarini ara (Rekursif arama)
Write-Host "[+] Gerekli dosyalar araniyor (items.json, mobs.json, localization.json)..." -ForegroundColor Cyan

$itemsStatsFile = Join-Path $SelectedFolder "items.json"
$itemsDumperFile = Join-Path $SelectedFolder "formatted\items.json"
$mobsFile = Join-Path $SelectedFolder "mobs.json"
$localizationFile = Join-Path $SelectedFolder "localization.json"

if (-not (Test-Path $itemsStatsFile) -or -not (Test-Path $itemsDumperFile) -or -not (Test-Path $mobsFile) -or -not (Test-Path $localizationFile)) {
    $errMsg = "Secilen klasorde gerekli dosyalar bulunamadi!`n`nLutfen sunlari kontrol edin:`n- Dizin altinda 'items.json'`n- Dizin altinda 'mobs.json'`n- Dizin altinda 'localization.json'`n- '\formatted\' altinda 'items.json'"
    [System.Windows.Forms.MessageBox]::Show($errMsg, "Dosyalar Eksik", [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Error)
    Write-Host "[HATA] Gerekli JSON dosyalari bulunamadi. Islemi iptal ettik." -ForegroundColor Red
    exit 1
}

# 3. Dosyalari Update klasorune kopyala ve isimlerini esle
$DestItems = Join-Path $PSScriptRoot "items_dumper.json"
$DestMobs = Join-Path $PSScriptRoot "mobs.json"
$DestLocalization = Join-Path $PSScriptRoot "localization.json"
$DestStats = Join-Path $PSScriptRoot "items_stats.json"

Write-Host "[+] Dosyalar guncelleme araci dizinine hazirlaniyor..." -ForegroundColor Cyan
Copy-Item $itemsDumperFile $DestItems -Force
Copy-Item $mobsFile $DestMobs -Force
Copy-Item $localizationFile $DestLocalization -Force
Copy-Item $itemsStatsFile $DestStats -Force
Write-Host "[+] Dosya hazirligi tamamlandi." -ForegroundColor Green

# 4. Python Donusturucu Scriptini Calistir
Write-Host "[+] Update.py calistiriliyor..." -ForegroundColor Cyan
Push-Location $PSScriptRoot
python Update.py
$pythonExitCode = $LASTEXITCODE
Pop-Location

if ($pythonExitCode -ne 0) {
    [System.Windows.Forms.MessageBox]::Show("Update.py calistirilirken bir hata olustu! Lutfen girdi dosyalarini (items_stats.json vb.) kontrol edin.", "Guncelleme Hatasi", [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Error)
    Write-Host "[HATA] Python betigi basarisiz oldu." -ForegroundColor Red
    exit 1
}

# 5. Olusan minified dosyalari hedef dizindeki Helper klasorune kopyala
$HelperDir = Join-Path $PSScriptRoot "Helper"
if (Test-Path $HelperDir) {
    $TargetHelperDir = Join-Path $ScriptDir "Helper"
    $absHelperDir = [System.IO.Path]::GetFullPath($HelperDir)
    $absTargetHelperDir = [System.IO.Path]::GetFullPath($TargetHelperDir)

    if ($absHelperDir -ne $absTargetHelperDir) {
        if (-not (Test-Path $TargetHelperDir)) {
            New-Item -ItemType Directory -Path $TargetHelperDir -Force | Out-Null
        }
        Write-Host "[+] Cikti dosyalari Helper klasorune kopyalaniyor ($TargetHelperDir)..." -ForegroundColor Cyan
        Get-ChildItem -Path $HelperDir -File | ForEach-Object {
            Copy-Item $_.FullName $TargetHelperDir -Force
        }
    } else {
        Write-Host "[+] Dosyalar zaten hedef Helper klasorunde ($TargetHelperDir). Kopyalama atlandi." -ForegroundColor Green
    }
}

Write-Host ""
Write-Host "============================================================" -ForegroundColor Green
Write-Host "   ISLEM BASARIYLA TAMAMLANDI!" -ForegroundColor Green
Write-Host "============================================================" -ForegroundColor Green

[System.Windows.Forms.MessageBox]::Show("Guncelleme islemi basariyla tamamlandi!`n`nYeni veritabani dosyalari '.bat'/'.exe' dosyasinin yanina kopyalandi.", "Basarili", [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Information)
exit 0
