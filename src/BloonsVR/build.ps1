<#
.SYNOPSIS
    Builds BloonsVR and drops the DLL into the BTD6 Mods folder.

.DESCRIPTION
    BTD6 lives in a folder that contains spaces and is under Program Files (x86), so this script
    resolves the path once and fails loudly instead of guessing.

.EXAMPLE
    .\build.ps1
    .\build.ps1 -Configuration Debug -Deploy:$false
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release',

    [string] $BloonsTD6Dir = 'C:\Program Files (x86)\Steam\steamapps\common\BloonsTD6',

    [switch] $Deploy
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $BloonsTD6Dir)) {
    throw "BTD6 not found at '$BloonsTD6Dir'. Pass -BloonsTD6Dir <path>."
}

$modsDir = Join-Path $BloonsTD6Dir 'Mods'
$melonDir = Join-Path $BloonsTD6Dir 'MelonLoader'
$assemblies = Join-Path $melonDir 'Il2CppAssemblies'

foreach ($required in @(
        $modsDir,
        (Join-Path $melonDir 'net6\MelonLoader.dll'),
        (Join-Path $assemblies 'Assembly-CSharp.dll'),
        (Join-Path $modsDir 'Btd6ModHelper.dll'))) {
    if (-not (Test-Path $required)) {
        throw "Missing '$required'. Launch BTD6 once with MelonLoader installed so it generates Il2CppAssemblies, and keep Btd6ModHelper.dll in Mods\."
    }
}

$project = Join-Path $PSScriptRoot 'BloonsVR.csproj'

Write-Host "==> Building BloonsVR ($Configuration)" -ForegroundColor Cyan
& dotnet build $project -c $Configuration "-p:BloonsTD6Dir=$BloonsTD6Dir" -v minimal
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build failed with exit code $LASTEXITCODE."
}

$dll = Join-Path $PSScriptRoot "bin\$Configuration\BloonsVR.dll"
if (-not (Test-Path $dll)) {
    throw "Build succeeded but '$dll' is missing."
}

if ($Deploy) {
    Write-Host "==> Deploying to $modsDir" -ForegroundColor Cyan
    Copy-Item $dll (Join-Path $modsDir 'BloonsVR.dll') -Force
    Write-Host "    Mods\BloonsVR.dll" -ForegroundColor Green
}

Write-Host 'Done.' -ForegroundColor Green