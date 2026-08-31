<#
    Builds VaultAdmin.dll and stages a release archive.

    No .NET SDK required: the plugin targets the Unity Mono profile, so it is compiled with
    the C# compiler that ships with the .NET Framework, against the game's own assemblies.

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

$modName = "VaultAdmin"

$managed = Join-Path $GamePath "FalloutShelter_Data\Managed"
$core    = Join-Path $GamePath "BepInEx\core"
$src     = Join-Path $PSScriptRoot "src\VaultAdminPlugin.cs"
$outDir  = Join-Path $PSScriptRoot "build"
$distDir = Join-Path $PSScriptRoot "dist"
$outDll  = Join-Path $outDir "$modName.dll"

# The version lives in the source and nowhere else. A second copy in this script is what once let
# a build carrying unreleased code overwrite a release archive under a name that no longer
# described it.
$srcText = Get-Content $src -Raw
if ($srcText -notmatch 'PluginVersion\s*=\s*"([0-9.]+)"') { throw "Could not read PluginVersion from $src" }
$version = $Matches[1]

if (-not (Test-Path $GamePath)) { throw "Game folder not found: $GamePath  (pass -GamePath)" }
if (-not (Test-Path $core)) { throw "BepInEx not found in the game folder. Install BepInEx 5.x (Unity Mono, x64) first." }
if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir | Out-Null }
if (-not (Test-Path $distDir)) { New-Item -ItemType Directory -Path $distDir | Out-Null }

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
    (Join-Path $managed "UnityEngine.IMGUIModule.dll"),   # scaffold panel; goes when NGUI lands
    (Join-Path $managed "Unity.InputSystem.dll"),         # legacy UnityEngine.Input throws here
    (Join-Path $managed "UnityEngine.ImageConversionModule.dll"),  # PNG -> Texture2D for the button icon
    (Join-Path $managed "UnityEngine.TextRenderingModule.dll"),   # Font, for borrowing the game's
    (Join-Path $managed "UnityEngine.PhysicsModule.dll")          # BoxCollider: NGUI routes clicks through colliders
)
foreach ($r in $refs) { if (-not (Test-Path $r)) { throw "Missing reference: $r" } }
$refArgs = $refs | ForEach-Object { "/reference:$_" }

& $csc /target:library /optimize+ /nostdlib+ /noconfig /out:$outDll @refArgs $src
if ($LASTEXITCODE -ne 0) { throw "Compilation failed with exit code $LASTEXITCODE" }
Write-Host "Built $outDll ($version)" -ForegroundColor Green

# Stage the archive with the same folder layout a player extracts into the game root.
$stage = Join-Path $outDir "stage"
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Path (Join-Path $stage "BepInEx\plugins") | Out-Null
Copy-Item $outDll (Join-Path $stage "BepInEx\plugins") -Force

# The button icon travels with the DLL: the plugin looks for it beside itself.
$assets = Join-Path $PSScriptRoot "assets"
if (Test-Path $assets) { Get-ChildItem $assets -File | ForEach-Object { Copy-Item $_.FullName (Join-Path $stage "BepInEx\plugins") -Force } }
foreach ($doc in @("README.md", "CHANGELOG.md", "LICENSE")) {
    $p = Join-Path $PSScriptRoot $doc
    if (Test-Path $p) { Copy-Item $p $stage -Force }
}

$zip = Join-Path $distDir "$modName-$version.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $stage "*") -DestinationPath $zip
Write-Host "Packaged $zip" -ForegroundColor Green

if ($Install) {
    # The game memory-maps its plugins, so copying over a loaded DLL fails with an IOException
    # that is easy to skim past in a build log. Say so plainly instead.
    if (Get-Process -Name "FalloutShelter*" -ErrorAction SilentlyContinue) {
        throw "Fallout Shelter is running. Close it before installing, or the DLL cannot be replaced."
    }

    $plugins = Join-Path $GamePath "BepInEx\plugins"
    if (-not (Test-Path $plugins)) { New-Item -ItemType Directory -Path $plugins | Out-Null }
    Copy-Item $outDll $plugins -Force
    if (Test-Path $assets) { Get-ChildItem $assets -File | ForEach-Object { Copy-Item $_.FullName $plugins -Force } }

    # Verify the artefact rather than trusting the copy: a success line has been wrong here before.
    $landed = Get-Item (Join-Path $plugins "$modName.dll")
    if ($landed.Length -ne (Get-Item $outDll).Length) {
        throw "Install did not take: $($landed.FullName) is $($landed.Length) bytes, expected $((Get-Item $outDll).Length)."
    }
    Write-Host "Installed to $plugins ($($landed.Length) bytes)" -ForegroundColor Green
}
