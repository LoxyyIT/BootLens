[CmdletBinding()]
param([string]$Version = '0.2.6')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$artifactRoot = Join-Path $root 'artifacts'
$portable = Join-Path $artifactRoot "BootLens-$Version-win-x64"
New-Item -ItemType Directory -Force -Path $artifactRoot | Out-Null
if (Test-Path -LiteralPath $portable) { Remove-Item -LiteralPath $portable -Recurse -Force }
dotnet restore (Join-Path $root 'BootLens.sln')
dotnet build (Join-Path $root 'BootLens.sln') -c Release --no-restore
dotnet test (Join-Path $root 'BootLens.sln') -c Release --no-build --no-restore
dotnet publish (Join-Path $root 'src\BootLens.App\BootLens.App.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $portable
Get-ChildItem -LiteralPath $portable -Filter '*.pdb' -File | Remove-Item -Force
$exe = Join-Path $portable 'BootLens.exe'
if (-not (Test-Path -LiteralPath $exe)) { throw "Publish did not produce the expected executable: $exe" }
$releaseExe = Join-Path $artifactRoot 'BootLens.exe'
Copy-Item -LiteralPath $exe -Destination $releaseExe -Force
$zip = Join-Path $artifactRoot "BootLens-$Version-win-x64.zip"
if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip -Force }
Compress-Archive -Path (Join-Path $portable '*') -DestinationPath $zip
$checksumFile = Join-Path $artifactRoot "BootLens-$Version-SHA256SUMS.txt"
$checksumLines = @(
    "$( (Get-FileHash -LiteralPath $releaseExe -Algorithm SHA256).Hash.ToLowerInvariant() )  BootLens.exe",
    "$( (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant() )  BootLens-$Version-win-x64.zip"
)
Set-Content -LiteralPath $checksumFile -Value $checksumLines -Encoding ascii
$compiler = @('C:\Program Files (x86)\Inno Setup 6\ISCC.exe', 'C:\Program Files\Inno Setup 6\ISCC.exe') | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if ($compiler) { & $compiler "/DAppVersion=$Version" "/DPortableDir=$portable" (Join-Path $root 'deploy\BootLens.iss') } else { Write-Warning 'Inno Setup non trovato: ZIP creato, installer EXE non generato.' }
Write-Output "Portable: $portable"
Write-Output "ZIP: $zip"
Write-Output "Checksums: $checksumFile"
