<#
    Builds NukaColaQuantumProduction.dll and stages a Nexus-ready release archive.

    No .NET SDK required: the plugin targets the Unity Mono profile, so it is compiled
    with the C# compiler that ships with the .NET Framework (present on every Windows
    install) against the game's own assemblies.

    Usage:
        .\build.ps1
        .\build.ps1 -GamePath "C:\Path\To\Fallout Shelter"
        .\build.ps1 -Install          # also copies the DLL into the game
#>
param(
    [string]$GamePath = "D:\SteamLibrary\steamapps\common\Fallout Shelter",
    [switch]$Install
)

$ErrorActionPreference = "Stop"

$version = "1.12.1"
$modName = "NukaColaQuantumProduction"

$managed = Join-Path $GamePath "FalloutShelter_Data\Managed"
$core    = Join-Path $GamePath "BepInEx\core"
$src     = Join-Path $PSScriptRoot "src\NukaColaQuantumPlugin.cs"
$outDir  = Join-Path $PSScriptRoot "build"
$distDir = Join-Path $PSScriptRoot "dist"
$outDll  = Join-Path $outDir "$modName.dll"

if (-not (Test-Path $GamePath)) { throw "Game folder not found: $GamePath  (pass -GamePath)" }
if (-not (Test-Path $core)) { throw "BepInEx not found in the game folder. Install BepInEx 5.x (Unity Mono, x64) first." }
if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir | Out-Null }
if (-not (Test-Path $distDir)) { New-Item -ItemType Directory -Path $distDir | Out-Null }

$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) { throw "C# compiler not found at $csc" }

# Reference the game's own Mono/.NET Standard assemblies rather than the .NET Framework
# ones (hence /nostdlib+), so the plugin binds against the runtime Unity loads it into.
$refs = @(
    (Join-Path $managed "mscorlib.dll"),
    (Join-Path $managed "System.dll"),
    (Join-Path $managed "System.Core.dll"),
    (Join-Path $managed "netstandard.dll"),
    (Join-Path $core    "BepInEx.dll"),
    (Join-Path $core    "0Harmony.dll"),
    (Join-Path $managed "Assembly-CSharp.dll"),
    (Join-Path $managed "UnityEngine.dll"),
    (Join-Path $managed "UnityEngine.CoreModule.dll")
)
foreach ($r in $refs) { if (-not (Test-Path $r)) { throw "Missing reference: $r" } }
$refArgs = $refs | ForEach-Object { "/reference:$_" }

& $csc /target:library /optimize+ /nostdlib+ /noconfig /out:$outDll @refArgs $src
if ($LASTEXITCODE -ne 0) { throw "Compilation failed with exit code $LASTEXITCODE" }
Write-Host "Built $outDll" -ForegroundColor Green

# Stage the archive with the same folder layout the player extracts into the game root.
$stage = Join-Path $outDir "stage"
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Path (Join-Path $stage "BepInEx\plugins") | Out-Null
Copy-Item $outDll (Join-Path $stage "BepInEx\plugins") -Force
Copy-Item (Join-Path $PSScriptRoot "README.md") $stage -Force
Copy-Item (Join-Path $PSScriptRoot "LICENSE") $stage -Force
Copy-Item (Join-Path $PSScriptRoot "CHANGELOG.md") $stage -Force

$zip = Join-Path $distDir "$modName-$version.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $stage "*") -DestinationPath $zip
Write-Host "Packaged $zip" -ForegroundColor Green

if ($Install) {
    $plugins = Join-Path $GamePath "BepInEx\plugins"
    if (-not (Test-Path $plugins)) { New-Item -ItemType Directory -Path $plugins | Out-Null }
    Copy-Item $outDll $plugins -Force
    Write-Host "Installed to $plugins" -ForegroundColor Green
}
