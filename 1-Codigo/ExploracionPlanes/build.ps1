<#
.SYNOPSIS
    Compila ExploracionPlanes para una version de Eclipse, generando ambos outputs:
    ExploracionPlanes.esapi.dll (plugin, se copia en Eclipse) y ExploracionPlanes.exe (standalone).

.EXAMPLE
    .\build.ps1 13_6
    .\build.ps1 15_6 -Configuration Release
    .\build.ps1 18_2
#>
param(
    [Parameter(Mandatory)]
    [ValidateSet("13_6", "15_6", "18_2")]
    [string]$Version,

    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"

$msbuild = "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\amd64\MSBuild.exe"
if (-not (Test-Path $msbuild)) {
    throw "No se encontro MSBuild en '$msbuild'. Ajustar la ruta en build.ps1 si cambio la instalacion de Visual Studio."
}

$proj = "ExploracionPlanes.Eclipse$Version.csproj"
$common = @("-p:Configuration=$Configuration", "-p:Platform=x64", "-nologo", "-v:minimal")

# IntermediateOutputPath separado por tipo de output: si comparten obj\, el "incremental clean" de
# MSBuild borra los archivos de la build anterior (distinto AssemblyName) al no reconocerlos como
# output de la build actual.
Write-Host "=== $proj ($Configuration) : DLL plugin (ExploracionPlanes.esapi.dll) ===" -ForegroundColor Cyan
& $msbuild $proj @common "-p:OutputType=Library" "-p:AssemblyName=ExploracionPlanes.esapi" "-p:IntermediateOutputPath=obj\Eclipse$Version\x64\$Configuration\dll\"
if ($LASTEXITCODE -ne 0) { throw "Fallo la compilacion de la DLL" }

Write-Host "=== $proj ($Configuration) : EXE standalone (ExploracionPlanes.exe) ===" -ForegroundColor Cyan
& $msbuild $proj @common "-p:OutputType=Exe" "-p:AssemblyName=ExploracionPlanes" "-p:StartupObject=ExploracionPlanes.Program" "-p:IntermediateOutputPath=obj\Eclipse$Version\x64\$Configuration\exe\"
if ($LASTEXITCODE -ne 0) { throw "Fallo la compilacion del EXE" }

Write-Host "OK -> bin\Eclipse$Version\x64\$Configuration\" -ForegroundColor Green
