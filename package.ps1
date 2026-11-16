# Builds the plugin and zips a Thunderstore / r2modman "Import local mod" package into dist/.
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
dotnet build "$root\src\Plugin\SpellBrigadeMinimap.csproj" -c Release
if ($LASTEXITCODE -ne 0) { throw "Build failed" }

$manifest = Get-Content "$root\thunderstore\manifest.json" -Raw | ConvertFrom-Json
$stage = Join-Path $root "dist\stage"
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Force $stage | Out-Null
Copy-Item "$root\thunderstore\manifest.json", "$root\thunderstore\README.md", "$root\thunderstore\icon.png" $stage
Copy-Item "$root\src\Plugin\bin\Release\net6.0\SpellBrigadeMinimap.dll" $stage

$zip = Join-Path $root ("dist\SpellBrigadeMinimap-" + $manifest.version_number + ".zip")
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path "$stage\*" -DestinationPath $zip
Remove-Item $stage -Recurse -Force
Write-Host "Packaged $zip"
