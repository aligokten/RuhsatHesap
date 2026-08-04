@echo off
setlocal EnableExtensions

rem ---------------------------------------------------------------------------
rem RuhsatHesap - Archicad 29 (Windows x64) yerel derleme betigi
rem
rem Kullanim:  build-windows.bat [Release|RelWithDebInfo|Debug]
rem
rem Bu dosya CRLF satir sonlariyla saklanmalidir; cmd.exe LF-only .bat
rem dosyalarindaki cok satirli bloklari hatali ayristirir. Bkz. .gitattributes
rem ---------------------------------------------------------------------------

set "PROJECT_ROOT=%~dp0"
if "%PROJECT_ROOT:~-1%"=="\" set "PROJECT_ROOT=%PROJECT_ROOT:~0,-1%"

set "BUILD_CONFIG=%~1"
if "%BUILD_CONFIG%"=="" set "BUILD_CONFIG=Release"

set "BUILD_DIR=%PROJECT_ROOT%\Build"
set "ADDON_NAME=RuhsatHesap"
set "AC_VERSION=29"

set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"

echo ============================================================
echo  RuhsatHesap - Archicad %AC_VERSION% Windows x64 derlemesi
echo ============================================================
echo  Proje klasoru : %PROJECT_ROOT%
echo  Yapilandirma  : %BUILD_CONFIG%
echo.

rem --- 1) API DevKit klasorunu bul ve dogrula -------------------------------

if not "%AC_API_DEVKIT_DIR%"=="" goto devkit_given
echo [bilgi] AC_API_DEVKIT_DIR tanimli degil, bilinen konumlar taraniyor...
call :FindDevKit
if "%AC_API_DEVKIT_DIR%"=="" goto err_devkit_missing
echo [bilgi] DevKit bulundu: %AC_API_DEVKIT_DIR%

:devkit_given
if "%AC_API_DEVKIT_DIR:~-1%"=="\" set "AC_API_DEVKIT_DIR=%AC_API_DEVKIT_DIR:~0,-1%"
if not exist "%AC_API_DEVKIT_DIR%\" goto err_devkit_nopath

for %%I in ("%AC_API_DEVKIT_DIR%") do set "DEVKIT_LEAF=%%~nxI"
if /I not "%DEVKIT_LEAF%"=="Support" goto err_devkit_notsupport
if not exist "%AC_API_DEVKIT_DIR%\Lib\ACAP_STAT.lib" goto err_devkit_nolib
if not exist "%AC_API_DEVKIT_DIR%\Modules\" goto err_devkit_nomodules

echo  API DevKit    : %AC_API_DEVKIT_DIR%

rem --- 2) CMake'i bul --------------------------------------------------------

set "CMAKE_EXE="
where cmake >nul 2>&1
if not errorlevel 1 set "CMAKE_EXE=cmake"
if not "%CMAKE_EXE%"=="" goto cmake_found
call :FindCMake
if "%CMAKE_EXE%"=="" goto err_no_cmake

:cmake_found
echo  CMake         : %CMAKE_EXE%

rem --- 3) Visual Studio uretecini belirle ------------------------------------

call :DetectGenerator
echo  Generator     : %CMAKE_GENERATOR_NAME% (toolset %CMAKE_TOOLSET%)

rem --- 4) Python 3.10+ kontrolu ---------------------------------------------
rem     Kaynak derlemesi (Tools\CompileResources.py) Python olmadan calismaz.

where python >nul 2>&1
if not errorlevel 1 goto python_ok
where py >nul 2>&1
if not errorlevel 1 goto python_ok
echo.
echo [UYARI] PATH uzerinde Python bulunamadi. Kaynak derleme adimi
echo         (CompileResources.py) basarisiz olacaktir. Python 3.10+ kurup
echo         "Add python.exe to PATH" secenegini isaretleyin.
echo.

:python_ok

rem --- 5) Proje uret ---------------------------------------------------------
rem
rem  AC_WIN_LANGCHARSET / AC_WIN_LANGUAGEID / AC_WIN_CHARSETID mutlaka
rem  gecilmelidir. Bunlar olmadan Tools\VersionInfo.rc.in dosyasindan
rem  uretilen VersionInfo.rc icinde `BLOCK ""` ve `VALUE "Translation", ,`
rem  satirlari olusur; rc.exe bunu reddeder ve .apx hic linklenmez.
rem  040904b0 = US English / Unicode (INT dili icin BuildAddOn.py ile ayni).

echo.
echo --- CMake yapilandirmasi ---
"%CMAKE_EXE%" -S "%PROJECT_ROOT%" -B "%BUILD_DIR%" -G "%CMAKE_GENERATOR_NAME%" -A x64 -T %CMAKE_TOOLSET% ^
    -DAC_VERSION=%AC_VERSION% ^
    -DAC_API_DEVKIT_DIR="%AC_API_DEVKIT_DIR%" ^
    -DAC_ADDON_LANGUAGE=INT ^
    -DAC_WIN_LANGCHARSET=040904b0 ^
    -DAC_WIN_LANGUAGEID=1033 ^
    -DAC_WIN_CHARSETID=1200 ^
    -DAC_ADDON_WARNINGS_AS_ERRORS=OFF
if errorlevel 1 goto err_configure

rem --- 6) Derle --------------------------------------------------------------

echo.
echo --- Derleme (%BUILD_CONFIG%) ---
"%CMAKE_EXE%" --build "%BUILD_DIR%" --config %BUILD_CONFIG%
if errorlevel 1 goto err_build

rem --- 7) Ciktiyi dogrula ----------------------------------------------------

set "APX_PATH=%BUILD_DIR%\%BUILD_CONFIG%\%ADDON_NAME%.apx"
if not exist "%APX_PATH%" goto err_no_apx

echo.
echo ============================================================
echo  BASARILI
echo ============================================================
echo  Add-On dosyasi:
echo    %APX_PATH%
echo.
echo  Archicad %AC_VERSION% icinde:
echo    Secenekler ^> Eklenti Yoneticisi ^> Ekle
echo  yolundan yukaridaki .apx dosyasini secin.
echo.
endlocal
exit /b 0

rem ===========================================================================
rem  Yardimci altyordamlar
rem ===========================================================================

:FindDevKit
rem Tek satirlik call'lar bilerek kullanildi: cok satirli ( ... ) bloklari
rem satir sonu bozulmalarina karsi kirilgandir.
call :ScanDevKitRoot "%ProgramFiles%\GRAPHISOFT"
call :ScanDevKitRoot "%ProgramFiles%\Graphisoft"
call :ScanDevKitRoot "%ProgramFiles%\GRAPHISOFT\Archicad 29"
call :ScanDevKitRoot "%ProgramFiles%\Graphisoft\Archicad 29"
call :ScanDevKitRoot "C:\Graphisoft"
call :ScanDevKitRoot "C:\GRAPHISOFT"
call :ScanDevKitRoot "%USERPROFILE%\Downloads"
goto :eof

:ScanDevKitRoot
if not "%AC_API_DEVKIT_DIR%"=="" goto :eof
set "SCAN_ROOT=%~1"
if not exist "%SCAN_ROOT%\" goto :eof
call :TestDevKitCandidate "%SCAN_ROOT%\Support"
if not "%AC_API_DEVKIT_DIR%"=="" goto :eof
for /d %%D in ("%SCAN_ROOT%\API*Development*Kit*") do call :TestDevKitCandidate "%%~fD\Support"
goto :eof

:TestDevKitCandidate
if not "%AC_API_DEVKIT_DIR%"=="" goto :eof
if not exist "%~1\Lib\ACAP_STAT.lib" goto :eof
if not exist "%~1\Modules\" goto :eof
set "AC_API_DEVKIT_DIR=%~1"
goto :eof

:FindCMake
rem Visual Studio ile birlikte gelen cmake.exe genellikle PATH uzerinde degildir.
if not exist "%VSWHERE%" goto FindCMakeFallback
set "VSPATH="
for /f "usebackq delims=" %%P in (`"%VSWHERE%" -latest -products * -property installationPath`) do set "VSPATH=%%P"
if "%VSPATH%"=="" goto FindCMakeFallback
if exist "%VSPATH%\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe" set "CMAKE_EXE=%VSPATH%\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe"
if not "%CMAKE_EXE%"=="" goto :eof

:FindCMakeFallback
if exist "%ProgramFiles%\CMake\bin\cmake.exe" set "CMAKE_EXE=%ProgramFiles%\CMake\bin\cmake.exe"
goto :eof

:DetectGenerator
rem Archicad 29 DevKit v143 arac setiyle derlenir (bkz. Tools\BuildAddOn.py).
set "CMAKE_TOOLSET=v143"
set "CMAKE_GENERATOR_NAME=Visual Studio 17 2022"
if not exist "%VSWHERE%" goto :eof
set "VSMAJOR="
for /f "usebackq tokens=1 delims=." %%V in (`"%VSWHERE%" -latest -products * -property installationVersion`) do set "VSMAJOR=%%V"
if "%VSMAJOR%"=="18" set "CMAKE_GENERATOR_NAME=Visual Studio 18 2026"
if "%VSMAJOR%"=="17" set "CMAKE_GENERATOR_NAME=Visual Studio 17 2022"
if "%VSMAJOR%"=="16" set "CMAKE_GENERATOR_NAME=Visual Studio 16 2019"
goto :eof

rem ===========================================================================
rem  Hata cikislari
rem ===========================================================================

:err_devkit_missing
echo.
echo [HATA] Archicad %AC_VERSION% API DevKit bulunamadi.
echo        AC_API_DEVKIT_DIR ortam degiskenini DevKit'in Support klasorune
echo        ayarlayin, ornegin PowerShell icinde:
echo.
echo          $env:AC_API_DEVKIT_DIR = "C:\Graphisoft\API.Development.Kit.WIN.29.3100\Support"
echo.
echo        DevKit indirme adresi:
echo          https://github.com/GRAPHISOFT/archicad-api-devkit/releases/download/29.3100/API.Development.Kit.WIN.29.3100.zip
goto fail

:err_devkit_nopath
echo.
echo [HATA] AC_API_DEVKIT_DIR var olmayan bir klasoru gosteriyor:
echo          %AC_API_DEVKIT_DIR%
goto fail

:err_devkit_notsupport
echo.
echo [HATA] AC_API_DEVKIT_DIR, DevKit'in "Support" alt klasorunu gostermelidir.
echo        Verilen deger : %AC_API_DEVKIT_DIR%
echo        Beklenen bicim: ...\API.Development.Kit.WIN.29.3100\Support
goto fail

:err_devkit_nolib
echo.
echo [HATA] %AC_API_DEVKIT_DIR%\Lib\ACAP_STAT.lib bulunamadi.
echo        Bu klasor gecerli bir Archicad 29 API DevKit "Support" klasoru degil,
echo        ya da DevKit arsivi eksik acilmis.
goto fail

:err_devkit_nomodules
echo.
echo [HATA] %AC_API_DEVKIT_DIR%\Modules klasoru bulunamadi.
echo        DevKit arsivi eksik acilmis olabilir.
goto fail

:err_no_cmake
echo.
echo [HATA] cmake.exe bulunamadi.
echo        Ya CMake 3.19+ kurun (https://cmake.org/download/ - kurulumda
echo        "Add CMake to the system PATH" secenegini isaretleyin), ya da
echo        Visual Studio Installer icinden "C++ CMake tools for Windows"
echo        bilesenini ekleyin.
goto fail

:err_configure
echo.
echo [HATA] CMake yapilandirmasi basarisiz oldu.
if exist "%BUILD_DIR%\CMakeCache.txt" echo        Onceki bir derlemeden kalan onbellek olabilir; "%BUILD_DIR%" klasorunu
if exist "%BUILD_DIR%\CMakeCache.txt" echo        tamamen silip betigi yeniden calistirin.
goto fail

:err_build
echo.
echo [HATA] Derleme basarisiz oldu. Yukaridaki ilk hata satirina bakin.
goto fail

:err_no_apx
echo.
echo [HATA] Derleme hatasiz bitti ancak beklenen dosya olusmadi:
echo          %APX_PATH%
echo        %BUILD_DIR% altinda uretilen dosyalari kontrol edin.
goto fail

:fail
echo.
endlocal
exit /b 1
