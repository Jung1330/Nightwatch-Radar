@echo off
setlocal
title Nightwatch Build

echo ===================================================
echo                   Nightwatch Build
echo ===================================================
echo.

set "OUT=ReleaseBuildClean"

:: ---------------------------------------------------------------
:: TEK DOSYA (single-file) publish:
::   -p:PublishSingleFile=true                     -> yonetilen kod tek Nightwatch.exe
::   -p:IncludeNativeLibrariesForSelfExtract=false  -> native DLL'ler exe'nin YANINA konur
::   --self-contained true                         -> .NET runtime kurulu olmasi gerekmez
::
:: ONEMLI (neden false?):
::   true olsaydi exe her acilista kendini %TEMP%\.net\Nightwatch\... icine acardi.
::   Bu klasor kilitlenince/bozulunca uygulama HIC acilmiyordu:
::     "Failure processing application bundle.
::      Failed to create directory [...\Temp\.net\Nightwatch\...] for extracting bundled files."
::   false ile: hic acma yok -> daha hizli acilis, %TEMP% bagimliligi yok,
::   SmartScreen/temp kaynakli "acilmiyor" sorunlari tamamen biter.
::
:: YONETICI: Uygulama zaten her zaman yonetici olarak acilir.
::   Nightwatch\app.manifest -> requestedExecutionLevel level="requireAdministrator"
::   Nightwatch.csproj       -> <ApplicationManifest>app.manifest</ApplicationManifest>
::
:: NOT: AlbionPacketInspector ve AOSnifferNET arsive alindi (_arsiv\).
::      Artik sadece Nightwatch publish edilir (bagimliliklari otomatik gelir).
:: ---------------------------------------------------------------

:: 1) Cikti klasorunu TEMIZLE + eski tek-dosya exe'nin %TEMP% copunu sil
::    Neden: eski cikti klasoru zip'ten geldiyse icindeki dosyalar
::    "Mark of the Web" tasir -> SmartScreen/erisim sorunlari cikar.
echo [1/4] Eski cikti ve eski %TEMP% cozumleri temizleniyor...
if exist "%OUT%" (
    attrib -R "%OUT%\*.*" /S /D >nul 2>&1
    rmdir /s /q "%OUT%"
)
powershell -NoProfile -ExecutionPolicy Bypass -Command "Remove-Item -LiteralPath (Join-Path $env:TEMP '.net\Nightwatch') -Recurse -Force -ErrorAction SilentlyContinue"

:: 2) Projedeki tum dosyalardan internet isaretini (MOTW) kaldir
::    NOT: Klasorler de isaret tasiyabilir -> -File filtresi KULLANILMAZ.
echo [2/4] Internet isaretleri kaldiriliyor (proje + klasorler)...
powershell -NoProfile -ExecutionPolicy Bypass -Command "Unblock-File -LiteralPath '%CD%' -ErrorAction SilentlyContinue; Get-ChildItem -LiteralPath '%CD%' -Recurse -ErrorAction SilentlyContinue | Unblock-File -ErrorAction SilentlyContinue"

:: 3) Publish (Nightwatch) - TEK DOSYA
echo [3/4] Nightwatch derleniyor (Release / win-x64 / TEK DOSYA / self-contained)...
dotnet publish Nightwatch\Nightwatch.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=false -o "%OUT%"
if errorlevel 1 goto FAIL

:: 4) Ciktinin isaretlerini temizle + uygulamayi baslat
::    ONEMLI: "start" ShellExecute kullanir -> SmartScreen cikar.
::    Start-Process CreateProcess kullanir -> SmartScreen CIKMAZ.
echo [4/4] Cikti isaretleri temizleniyor ve uygulama baslatiliyor...
powershell -NoProfile -ExecutionPolicy Bypass -Command "Unblock-File -LiteralPath '%CD%\%OUT%' -ErrorAction SilentlyContinue; Get-ChildItem -LiteralPath '%CD%\%OUT%' -Recurse -ErrorAction SilentlyContinue | Unblock-File -ErrorAction SilentlyContinue"
powershell -NoProfile -ExecutionPolicy Bypass -Command "if (Test-Path '%CD%\%OUT%\Nightwatch.exe') { Start-Process -FilePath '%CD%\%OUT%\Nightwatch.exe' } else { Write-Host 'HATA: Nightwatch.exe olusmadi!' }"

echo.
echo ===================================================
echo                   Build OK.
echo        Cikti : %OUT%\Nightwatch.exe   (TEK DOSYA)
echo        UAC sorarsa Evet de -> yonetici olarak acilir.
echo ===================================================
echo.
echo NOT: Uygulama gorunmez calisir. Sistem tepsisindeki (saat yani)
echo      Nightwatch ikonuna cift tikla veya F12'ye bas.
echo.
pause
exit /b 0

:FAIL
echo.
echo ===================================================
echo   HATA: Derleme basarisiz!
echo   Yukaridaki hata satirlarini kontrol et.
echo ===================================================
echo.
pause
exit /b 1
