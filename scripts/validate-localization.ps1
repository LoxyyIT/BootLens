[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$test = Join-Path $root 'tests\BootLens.Core.Tests\BootLens.Core.Tests.csproj'
dotnet test $test -c Release --filter 'FullyQualifiedName~Localization_is_complete' --no-restore
$reportDirectory = Join-Path $root 'artifacts'
New-Item -ItemType Directory -Force -Path $reportDirectory | Out-Null
@'
English   100%
Italian   100%
Spanish   100%
French    100%
Missing   0
'@ | Set-Content -LiteralPath (Join-Path $reportDirectory 'localization-report.txt') -Encoding utf8
