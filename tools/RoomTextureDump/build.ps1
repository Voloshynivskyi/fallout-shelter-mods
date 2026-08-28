<#
    Builds RoomTextureDump.dll — a development tool, not a released mod.

    It writes a room's textures to disk as PNG so they can be repainted against the real
    UV layout. There is no release archive: this is never shipped to players.

    Usage:
        .\build.ps1
        .\build.ps1 -Install          # copies the DLL into the game
        .\build.ps1 -Uninstall        # removes it again
#>
param(
    [string]$GamePath = "D:\SteamLibrary\steamapps\common\Fallout Shelter",
    [switch]$Install,
    [switch]$Uninstall
)

$ErrorActionPreference = "Stop"

$modName = "RoomTextureDump"
$managed = Join-Path $GamePath "FalloutShelter_Data\Managed"
$core    = Join-Path $GamePath "BepInEx\core"
$plugins = Join-Path $GamePath "BepInEx\plugins"
$src     = Join-Path $PSScriptRoot "src\RoomTextureDumpPlugin.cs"
$outDir  = Join-Path $PSScriptRoot "build"
$outDll  = Join-Path $outDir "$modName.dll"

if ($Uninstall) {
    $installed = Join-Path $plugins "$modName.dll"
    if (Test-Path $installed) {
        Remove-Item $installed -Force
        Write-Host "Removed $installed" -ForegroundColor Green
    } else {
        Write-Host "Not installed." -ForegroundColor Yellow
    }
    return
}

if (-not (Test-Path $GamePath)) { throw "Game folder not found: $GamePath  (pass -GamePath)" }
if (-not (Test-Path $core)) { throw "BepInEx not found in the game folder. Install BepInEx 5.x (Unity Mono, x64) first." }
if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir | Out-Null }

$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) { throw "C# compiler not found at $csc" }

# Reference the game's own Mono assemblies rather than the .NET Framework ones (hence
# /nostdlib+), so the plugin binds against the runtime Unity loads it into.
$refs = @(
    (Join-Path $managed "mscorlib.dll"),
    (Join-Path $managed "System.dll"),
    (Join-Path $managed "System.Core.dll"),
    (Join-Path $managed "netstandard.dll"),
    (Join-Path $core    "BepInEx.dll"),
    (Join-Path $managed "Assembly-CSharp.dll"),
    (Join-Path $managed "UnityEngine.dll"),
    (Join-Path $managed "UnityEngine.CoreModule.dll"),
    (Join-Path $managed "UnityEngine.ImageConversionModule.dll")   # EncodeToPNG
)
foreach ($r in $refs) { if (-not (Test-Path $r)) { throw "Missing reference: $r" } }
$refArgs = $refs | ForEach-Object { "/reference:$_" }

& $csc /target:library /optimize+ /nostdlib+ /noconfig /out:$outDll @refArgs $src
if ($LASTEXITCODE -ne 0) { throw "Compilation failed with exit code $LASTEXITCODE" }
Write-Host "Built $outDll" -ForegroundColor Green

if ($Install) {
    if (-not (Test-Path $plugins)) { New-Item -ItemType Directory -Path $plugins | Out-Null }
    Copy-Item $outDll $plugins -Force
    Write-Host "Installed to $plugins" -ForegroundColor Green
    Write-Host "Remove it with:  .\build.ps1 -Uninstall" -ForegroundColor Yellow
}
