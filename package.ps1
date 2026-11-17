# Builds the plugin and zips a Thunderstore / r2modman "Import local mod" package into dist/.
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

# The three version numbers must agree before anything is built or zipped.
$manifestVersion = (Get-Content "$root\thunderstore\manifest.json" -Raw | ConvertFrom-Json).version_number
$pluginMatch = [regex]::Match((Get-Content "$root\src\Plugin\Plugin.cs" -Raw), 'Version\s*=\s*"([^"]+)"')
$csprojMatch = [regex]::Match((Get-Content "$root\src\Plugin\SpellBrigadeMinimap.csproj" -Raw), '<Version>([^<]+)</Version>')
if (-not $pluginMatch.Success) { throw "Version check: could not find Plugin.Version in src\Plugin\Plugin.cs" }
if (-not $csprojMatch.Success) { throw "Version check: could not find <Version> in src\Plugin\SpellBrigadeMinimap.csproj" }
$pluginVersion = $pluginMatch.Groups[1].Value
$csprojVersion = $csprojMatch.Groups[1].Value
if ($manifestVersion -ne $pluginVersion -or $manifestVersion -ne $csprojVersion) {
    throw "Version mismatch: manifest.json version_number is $manifestVersion, Plugin.cs Version is $pluginVersion, SpellBrigadeMinimap.csproj <Version> is $csprojVersion. Make all three the same."
}
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
