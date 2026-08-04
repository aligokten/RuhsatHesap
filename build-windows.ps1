<#
.SYNOPSIS
    RuhsatHesap - Archicad 29 (Windows x64) yerel derleme betigi.

.DESCRIPTION
    build-windows.bat ile ayni isi yapar, ancak PowerShell'de calistigi icin
    satir sonu (CRLF/LF) sorunlarindan etkilenmez. Derleme sonunda uretilen
    .apx dosyasinin tam yolunu yazar.

.PARAMETER Configuration
    Release (varsayilan), RelWithDebInfo veya Debug.

.PARAMETER DevKitDir
    Archicad 29 API DevKit'in "Support" klasoru. Verilmezse AC_API_DEVKIT_DIR
    ortam degiskeni, o da yoksa bilinen kurulum konumlari kullanilir.

.PARAMETER Clean
    Varsa Build klasorunu silip sifirdan yapilandirir.

.EXAMPLE
    .\build-windows.ps1

.EXAMPLE
    .\build-windows.ps1 -DevKitDir "C:\Graphisoft\API.Development.Kit.WIN.29.3100\Support" -Clean
#>
[CmdletBinding()]
param (
    [ValidateSet('Release', 'RelWithDebInfo', 'Debug')]
    [string] $Configuration = 'Release',

    [string] $DevKitDir,

    [switch] $Clean
)

$ErrorActionPreference = 'Stop'

$AcVersion   = 29
$AddOnName   = 'RuhsatHesap'
$ProjectRoot = $PSScriptRoot
$BuildDir    = Join-Path $ProjectRoot 'Build'

function Test-DevKitFolder {
    param ([string] $Path)
    if ([string]::IsNullOrWhiteSpace($Path)) { return $false }
    if (-not (Test-Path -LiteralPath $Path -PathType Container)) { return $false }
    if (-not (Test-Path -LiteralPath (Join-Path $Path 'Lib\ACAP_STAT.lib'))) { return $false }
    if (-not (Test-Path -LiteralPath (Join-Path $Path 'Modules') -PathType Container)) { return $false }
    return $true
}

function Find-DevKit {
    $roots = @(
        "$env:ProgramFiles\GRAPHISOFT"
        "$env:ProgramFiles\GRAPHISOFT\Archicad $AcVersion"
        "$env:ProgramFiles\Graphisoft"
        "$env:ProgramFiles\Graphisoft\Archicad $AcVersion"
        'C:\Graphisoft'
        'C:\GRAPHISOFT'
        "$env:USERPROFILE\Downloads"
    )

    foreach ($root in $roots) {
        if (-not (Test-Path -LiteralPath $root -PathType Container)) { continue }

        $direct = Join-Path $root 'Support'
        if (Test-DevKitFolder $direct) { return $direct }

        $nested = Get-ChildItem -LiteralPath $root -Directory -Filter 'API*Development*Kit*' -ErrorAction SilentlyContinue
        foreach ($dir in $nested) {
            $candidate = Join-Path $dir.FullName 'Support'
            if (Test-DevKitFolder $candidate) { return $candidate }
        }
    }
    return $null
}

function Find-CMake {
    $onPath = Get-Command cmake -CommandType Application -ErrorAction SilentlyContinue
    if ($onPath) { return $onPath.Source }

    # Visual Studio ships cmake.exe but does not put it on PATH.
    $vsWhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (Test-Path -LiteralPath $vsWhere) {
        $vsPath = & $vsWhere -latest -products '*' -property installationPath 2>$null | Select-Object -First 1
        if ($vsPath) {
            $bundled = Join-Path $vsPath 'Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe'
            if (Test-Path -LiteralPath $bundled) { return $bundled }
        }
    }

    $standalone = Join-Path $env:ProgramFiles 'CMake\bin\cmake.exe'
    if (Test-Path -LiteralPath $standalone) { return $standalone }

    return $null
}

function Get-VisualStudioGenerator {
    $vsWhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (-not (Test-Path -LiteralPath $vsWhere)) { return 'Visual Studio 17 2022' }

    $version = & $vsWhere -latest -products '*' -property installationVersion 2>$null | Select-Object -First 1
    switch (($version -split '\.')[0]) {
        '18'    { return 'Visual Studio 18 2026' }
        '17'    { return 'Visual Studio 17 2022' }
        '16'    { return 'Visual Studio 16 2019' }
        default { return 'Visual Studio 17 2022' }
    }
}

Write-Host '============================================================'
Write-Host " RuhsatHesap - Archicad $AcVersion Windows x64 derlemesi"
Write-Host '============================================================'
Write-Host " Proje klasoru : $ProjectRoot"
Write-Host " Yapilandirma  : $Configuration"

# --- 1) API DevKit -----------------------------------------------------------

if (-not $DevKitDir) { $DevKitDir = $env:AC_API_DEVKIT_DIR }
if (-not $DevKitDir) {
    Write-Host '[bilgi] AC_API_DEVKIT_DIR tanimli degil, bilinen konumlar taraniyor...'
    $DevKitDir = Find-DevKit
}

if (-not $DevKitDir) {
    Write-Error @"
Archicad $AcVersion API DevKit bulunamadi.

DevKit'in "Support" klasorunu belirtin:
  .\build-windows.ps1 -DevKitDir "C:\Graphisoft\API.Development.Kit.WIN.29.3100\Support"

Indirme adresi:
  https://github.com/GRAPHISOFT/archicad-api-devkit/releases/download/29.3100/API.Development.Kit.WIN.29.3100.zip
"@
}

$DevKitDir = $DevKitDir.TrimEnd('\')

if (-not (Test-Path -LiteralPath $DevKitDir -PathType Container)) {
    Write-Error "AC_API_DEVKIT_DIR var olmayan bir klasoru gosteriyor: $DevKitDir"
}
if ((Split-Path $DevKitDir -Leaf) -ne 'Support') {
    Write-Error @"
API DevKit yolu DevKit'in "Support" alt klasorunu gostermelidir.
  Verilen deger : $DevKitDir
  Beklenen bicim: ...\API.Development.Kit.WIN.29.3100\Support
"@
}
if (-not (Test-DevKitFolder $DevKitDir)) {
    Write-Error @"
$DevKitDir gecerli bir Archicad $AcVersion API DevKit "Support" klasoru degil.
Lib\ACAP_STAT.lib veya Modules klasoru eksik - DevKit arsivi eksik acilmis olabilir.
"@
}
Write-Host " API DevKit    : $DevKitDir"

# --- 2) CMake ----------------------------------------------------------------

$cmake = Find-CMake
if (-not $cmake) {
    Write-Error @"
cmake.exe bulunamadi.

Ya CMake 3.19+ kurun (https://cmake.org/download/ - kurulumda "Add CMake to the
system PATH" secenegini isaretleyin), ya da Visual Studio Installer icinden
"C++ CMake tools for Windows" bilesenini ekleyin.
"@
}
Write-Host " CMake         : $cmake"

$generator = Get-VisualStudioGenerator
Write-Host " Generator     : $generator (toolset v143)"

# --- 3) Python 3.10+ ---------------------------------------------------------
# Kaynak derlemesi (Tools\CompileResources.py) Python olmadan calismaz.

if (-not (Get-Command python -CommandType Application -ErrorAction SilentlyContinue) -and
    -not (Get-Command py -CommandType Application -ErrorAction SilentlyContinue)) {
    Write-Warning @"
PATH uzerinde Python bulunamadi. Kaynak derleme adimi (CompileResources.py)
basarisiz olacaktir. Python 3.10+ kurup "Add python.exe to PATH" secenegini
isaretleyin.
"@
}

# --- 4) Yapilandirma ---------------------------------------------------------
#
# AC_WIN_LANGCHARSET / AC_WIN_LANGUAGEID / AC_WIN_CHARSETID mutlaka gecilmelidir.
# Bunlar olmadan Tools\VersionInfo.rc.in dosyasindan uretilen VersionInfo.rc
# icinde `BLOCK ""` ve `VALUE "Translation", ,` satirlari olusur; rc.exe bunu
# reddeder ve .apx hic linklenmez.
# 040904b0 = US English / Unicode (INT dili icin BuildAddOn.py ile ayni deger).

if ($Clean -and (Test-Path -LiteralPath $BuildDir)) {
    Write-Host "`n[bilgi] Onceki Build klasoru siliniyor..."
    Remove-Item -LiteralPath $BuildDir -Recurse -Force
}

Write-Host "`n--- CMake yapilandirmasi ---"
& $cmake -S $ProjectRoot -B $BuildDir -G $generator -A x64 -T v143 `
    "-DAC_VERSION=$AcVersion" `
    "-DAC_API_DEVKIT_DIR=$DevKitDir" `
    '-DAC_ADDON_LANGUAGE=INT' `
    '-DAC_WIN_LANGCHARSET=040904b0' `
    '-DAC_WIN_LANGUAGEID=1033' `
    '-DAC_WIN_CHARSETID=1200' `
    '-DAC_ADDON_WARNINGS_AS_ERRORS=OFF'

if ($LASTEXITCODE -ne 0) {
    if (Test-Path -LiteralPath (Join-Path $BuildDir 'CMakeCache.txt')) {
        Write-Host "`nOnceki bir derlemeden kalan onbellek olabilir; -Clean ile yeniden deneyin."
    }
    Write-Error 'CMake yapilandirmasi basarisiz oldu.'
}

# --- 5) Derleme --------------------------------------------------------------

Write-Host "`n--- Derleme ($Configuration) ---"
& $cmake --build $BuildDir --config $Configuration
if ($LASTEXITCODE -ne 0) {
    Write-Error 'Derleme basarisiz oldu. Yukaridaki ilk hata satirina bakin.'
}

# --- 6) Cikti dogrulama ------------------------------------------------------

$apxPath = Join-Path $BuildDir "$Configuration\$AddOnName.apx"
if (-not (Test-Path -LiteralPath $apxPath)) {
    Write-Error @"
Derleme hatasiz bitti ancak beklenen dosya olusmadi:
  $apxPath
$BuildDir altinda uretilen dosyalari kontrol edin.
"@
}

$apx = Get-Item -LiteralPath $apxPath
Write-Host ''
Write-Host '============================================================'
Write-Host ' BASARILI'
Write-Host '============================================================'
Write-Host ' Add-On dosyasi:'
Write-Host "   $($apx.FullName)  ($([math]::Round($apx.Length / 1KB)) KB)"
Write-Host ''
Write-Host " Archicad $AcVersion icinde:"
Write-Host '   Secenekler > Eklenti Yoneticisi > Ekle'
Write-Host ' yolundan yukaridaki .apx dosyasini secin.'
Write-Host ''
