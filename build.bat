@echo off
echo ===================================================
echo Nightwatch Single-File Build (Release)
echo ===================================================
echo.
echo Fody/Costura sorunlari ve kalabalik dosyalari 
echo engellemek icin .NET'in native Single-File ozelligi kullaniliyor...
echo.
:: .NET Publish komutu ile tek bir exe oluşturma
:: IncludeNativeLibrariesForSelfExtract=true komutu cimgui.dll gibi native eklentileri EXE icine gomer.
dotnet publish Nightwatch.sln -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "ReleaseBuild"
echo.
echo ===================================================
echo BUILD TAMAMLANDI!
echo EXE dosyaniz "ReleaseBuild" klasorundedir.
echo ===================================================
pause