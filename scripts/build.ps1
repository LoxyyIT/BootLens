[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
dotnet restore (Join-Path $root 'BootLens.sln')
dotnet build (Join-Path $root 'BootLens.sln') -c Release --no-restore
dotnet test (Join-Path $root 'BootLens.sln') -c Release --no-build --no-restore
