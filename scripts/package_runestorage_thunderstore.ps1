# Builds the RuneStorage Thunderstore package.
#
# Output: MODS\thunderstore\RuneStorage-<version>.zip (MODS/ is git-ignored).
# Contents: manifest.json, README.md, icon.png, BepInEx\plugins\RuneStorage.dll
# Nothing else. No PDB, no source, no bundles outside the DLL.

$ErrorActionPreference = "Stop"

$repo = Split-Path -Parent $PSScriptRoot
$dotnet = "C:\Program Files\dotnet\dotnet.exe"
$project = Join-Path $repo "src\RuneStorage\RuneStorage.csproj"
$pluginSource = Join-Path $repo "src\RuneStorage\Plugin.cs"
$readmeSource = Join-Path $repo "packaging\thunderstore\README.md"
$iconSource = Join-Path $repo "src\RuneStorage\Assets\icon.png"
$websiteUrl = "https://github.com/ucwxcato/RuneStorage"
$description = "A buildable runestone terminal that gives a searchable view of a nearby storage core. Deposit, withdraw, craft from stored items, and upgrade the core."
$dependencies = @("denikson-BepInExPack_Valheim-5.4.2333", "ValheimModding-Jotunn-2.30.2")

# 1. Version must agree between the plugin and the project file.
$pluginText = Get-Content -LiteralPath $pluginSource -Raw
$pluginVersion = [regex]::Match($pluginText, 'VERSION = "([0-9]+\.[0-9]+\.[0-9]+)"').Groups[1].Value
$csprojText = Get-Content -LiteralPath $project -Raw
$projectVersion = [regex]::Match($csprojText, "<Version>([0-9]+\.[0-9]+\.[0-9]+)</Version>").Groups[1].Value
if ([string]::IsNullOrEmpty($pluginVersion)) { throw "Could not read VERSION from Plugin.cs." }
if ($pluginVersion -ne $projectVersion) { throw "Version mismatch: Plugin.cs is $pluginVersion, csproj is $projectVersion." }
$version = $pluginVersion

# 2. Package text checks.
if ($description.Length -gt 250) { throw "Description is $($description.Length) characters; Thunderstore allows 250." }
if (-not (Test-Path -LiteralPath $readmeSource)) { throw "Missing $readmeSource" }
$readmeText = Get-Content -LiteralPath $readmeSource -Raw
if ($readmeText -match "(?i)license") { throw "The Thunderstore README must not contain license text." }

# 3. Icon must be a 256x256 PNG.
Add-Type -AssemblyName System.Drawing
$icon = [System.Drawing.Image]::FromFile($iconSource)
try {
    if ($icon.Width -ne 256 -or $icon.Height -ne 256) { throw "icon.png must be 256x256; it is $($icon.Width)x$($icon.Height)." }
} finally {
    $icon.Dispose()
}

# 4. Clean Release build.
& $dotnet build $project -c Release --no-incremental
if ($LASTEXITCODE -ne 0) { throw "dotnet build failed with exit code $LASTEXITCODE." }

$dll = Join-Path $repo "src\RuneStorage\bin\Release\net48\net48\RuneStorage.dll"
if (-not (Test-Path -LiteralPath $dll)) { throw "Built DLL not found at $dll" }

# 5. Stage the package.
$outputRoot = Join-Path $repo "MODS\thunderstore"
$stage = Join-Path $outputRoot "RuneStorage-$version"
$zip = Join-Path $outputRoot "RuneStorage-$version.zip"
if (Test-Path -LiteralPath $stage) { Remove-Item -LiteralPath $stage -Recurse -Force }
if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip -Force }
New-Item -ItemType Directory -Force -Path (Join-Path $stage "BepInEx\plugins") | Out-Null

Copy-Item -LiteralPath $dll -Destination (Join-Path $stage "BepInEx\plugins\RuneStorage.dll")
Copy-Item -LiteralPath $readmeSource -Destination (Join-Path $stage "README.md")
Copy-Item -LiteralPath $iconSource -Destination (Join-Path $stage "icon.png")

$manifest = [ordered]@{
    name = "RuneStorage"
    version_number = $version
    website_url = $websiteUrl
    description = $description
    dependencies = $dependencies
}
$manifestJson = $manifest | ConvertTo-Json -Depth 4
[System.IO.File]::WriteAllText((Join-Path $stage "manifest.json"), $manifestJson, (New-Object System.Text.UTF8Encoding($false)))

# 6. Zip with forward-slash entry names, which Thunderstore and r2modman expect.
# CreateFromDirectory on .NET Framework writes backslashes, so entries are added explicitly.
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::Open($zip, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    Get-ChildItem -LiteralPath $stage -Recurse -File | ForEach-Object {
        $relative = $_.FullName.Substring($stage.Length).TrimStart('\', '/')
        $entryName = $relative -replace '\\', '/'
        [void][System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $_.FullName, $entryName, [System.IO.Compression.CompressionLevel]::Optimal)
    }
} finally {
    $archive.Dispose()
}

# 7. Verify the archive contents.
$expected = @("BepInEx/plugins/RuneStorage.dll", "README.md", "icon.png", "manifest.json")
$archive = [System.IO.Compression.ZipFile]::OpenRead($zip)
try {
    $entries = @($archive.Entries | ForEach-Object { $_.FullName })
} finally {
    $archive.Dispose()
}
foreach ($name in $expected) {
    if ($entries -notcontains $name) { throw "Package is missing $name." }
}
if ($entries.Count -ne $expected.Count) { throw "Package has unexpected entries: $($entries -join ', ')" }

$hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash
Write-Output "Package: $zip"
Write-Output "Entries: $($entries -join ', ')"
Write-Output "SHA256:  $hash"
