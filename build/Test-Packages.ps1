[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $Version,

    [string] $ReplayPath
)

$ErrorActionPreference = 'Stop'

if ($Version -notmatch '^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?$') {
    throw "Version '$Version' is not a valid SemVer version."
}

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$artifactRoot = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot 'artifacts'))
$packageDirectory = [System.IO.Path]::GetFullPath((Join-Path $artifactRoot 'packages'))
$smokeDirectory = [System.IO.Path]::GetFullPath((Join-Path $artifactRoot 'package-smoke'))
$separator = [System.IO.Path]::DirectorySeparatorChar
$repositoryPrefix = $repositoryRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar) + $separator

function Assert-PathInsideRepository([string] $path) {
    $fullPath = [System.IO.Path]::GetFullPath($path)
    if (-not $fullPath.StartsWith($repositoryPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to modify a path outside the checkout: $fullPath"
    }

    if (Test-Path -LiteralPath $fullPath) {
        $resolvedPath = (Resolve-Path -LiteralPath $fullPath).Path
        $resolvedFullPath = [System.IO.Path]::GetFullPath($resolvedPath)
        if (-not $resolvedFullPath.StartsWith($repositoryPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing to modify a path that resolves outside the checkout: $resolvedFullPath"
        }
        if (-not $resolvedFullPath.Equals($fullPath, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing to clean an artifact path that resolves through a link: $fullPath"
        }
    }
}

Assert-PathInsideRepository $artifactRoot
Assert-PathInsideRepository $packageDirectory
Assert-PathInsideRepository $smokeDirectory

foreach ($directory in @($packageDirectory, $smokeDirectory)) {
    if (Test-Path -LiteralPath $directory) {
        Remove-Item -LiteralPath $directory -Recurse -Force
    }
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
}

if ([string]::IsNullOrWhiteSpace($ReplayPath)) {
    $ReplayPath = Join-Path $repositoryRoot 'tests/Test.Integration/Replays/12974d2b-848f-490d-80ba-5f03a033c2d5.13_00.vrf'
}
elseif (-not [System.IO.Path]::IsPathRooted($ReplayPath)) {
    $ReplayPath = Join-Path $repositoryRoot $ReplayPath
}

$ReplayPath = [System.IO.Path]::GetFullPath($ReplayPath)
if (-not (Test-Path -LiteralPath $ReplayPath -PathType Leaf)) {
    throw "Replay fixture not found: $ReplayPath"
}

$gitOutput = & git -C $repositoryRoot rev-parse HEAD
if ($LASTEXITCODE -ne 0) {
    throw 'Could not resolve the checked-out Git commit for package metadata.'
}
$repositoryCommit = ($gitOutput | Out-String).Trim()
if ($repositoryCommit -notmatch '^[0-9a-fA-F]{40,64}$') {
    throw "Git returned an invalid repository commit: $repositoryCommit"
}

function Invoke-CheckedCommand([string] $FilePath, [string[]] $ArgumentList) {
    & $FilePath @ArgumentList
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code $LASTEXITCODE`: $FilePath $($ArgumentList -join ' ')"
    }
}

$packageProjects = @(
    @{ Id = 'ValorantReplayParser.Models'; Project = 'src/Replay.Models/Replay.Models.csproj'; Assembly = 'Replay.Models' },
    @{ Id = 'ValorantReplayParser.Encoding'; Project = 'src/Replay.Encoding/Replay.Encoding.csproj'; Assembly = 'Replay.Encoding' },
    @{ Id = 'ValorantReplayParser.Unreal'; Project = 'src/Replay.Unreal/Replay.Unreal.csproj'; Assembly = 'Replay.Unreal' },
    @{ Id = 'ValorantReplayParser'; Project = 'src/Replay.Valorant/Replay.Valorant.csproj'; Assembly = 'Replay.Valorant' }
)

foreach ($package in $packageProjects) {
    $projectPath = Join-Path $repositoryRoot $package.Project
    Invoke-CheckedCommand 'dotnet' @(
        'pack', $projectPath,
        '--configuration', 'Release',
        '--output', $packageDirectory,
        '--no-restore',
        "-p:Version=$Version",
        "-p:RepositoryCommit=$repositoryCommit"
    )
}

Add-Type -AssemblyName System.IO.Compression.ZipFile

function Read-PackageArchive([string] $archivePath) {
    $archive = [System.IO.Compression.ZipFile]::OpenRead($archivePath)
    try {
        $nuspecEntry = $archive.Entries | Where-Object { $_.FullName -match '\.nuspec$' } | Select-Object -First 1
        if ($null -eq $nuspecEntry) {
            throw "Package archive has no nuspec: $archivePath"
        }

        $reader = [System.IO.StreamReader]::new($nuspecEntry.Open())
        try {
            [xml] $nuspec = $reader.ReadToEnd()
        }
        finally {
            $reader.Dispose()
        }

        $metadata = $nuspec.SelectSingleNode("//*[local-name()='metadata']")
        if ($null -eq $metadata) {
            throw "Package nuspec has no metadata: $archivePath"
        }

        $dependencies = @($metadata.SelectNodes(".//*[local-name()='dependency']")) | ForEach-Object {
            [pscustomobject]@{
                Id = $_.GetAttribute('id')
                Version = $_.GetAttribute('version')
            }
        }
        $repository = $metadata.SelectSingleNode("./*[local-name()='repository']")
        $license = $metadata.SelectSingleNode("./*[local-name()='license']")

        return [pscustomobject]@{
            Id = $metadata.SelectSingleNode("./*[local-name()='id']").InnerText
            Version = $metadata.SelectSingleNode("./*[local-name()='version']").InnerText
            Authors = $metadata.SelectSingleNode("./*[local-name()='authors']").InnerText
            Description = $metadata.SelectSingleNode("./*[local-name()='description']").InnerText
            Readme = $metadata.SelectSingleNode("./*[local-name()='readme']").InnerText
            ProjectUrl = $metadata.SelectSingleNode("./*[local-name()='projectUrl']").InnerText
            LicenseExpression = if ($null -eq $license) { '' } else { $license.InnerText }
            RepositoryType = if ($null -eq $repository) { '' } else { $repository.GetAttribute('type') }
            RepositoryUrl = if ($null -eq $repository) { '' } else { $repository.GetAttribute('url') }
            RepositoryCommit = if ($null -eq $repository) { '' } else { $repository.GetAttribute('commit') }
            Dependencies = @($dependencies)
            Entries = @($archive.Entries | ForEach-Object { $_.FullName })
            NuspecText = $nuspec.OuterXml
        }
    }
    finally {
        $archive.Dispose()
    }
}

function Require([bool] $condition, [string] $message) {
    if (-not $condition) {
        throw $message
    }
}

$expectedDescriptions = @{
    'ValorantReplayParser.Models' = 'Shared models and public data contracts for VALORANT replay parsing.'
    'ValorantReplayParser.Encoding' = 'Wire decoding primitives, Unreal archives, and VALORANT payload transforms.'
    'ValorantReplayParser.Unreal' = 'VALORANT replay container, header, chunk, and network parsing.'
    'ValorantReplayParser' = 'VALORANT replay (.vrf) parsing with typed gameplay events and replay metadata. Experimental API.'
}

$expectedDependencies = @{
    'ValorantReplayParser.Encoding' = @('ValorantReplayParser.Models')
    'ValorantReplayParser.Unreal' = @('ValorantReplayParser.Encoding', 'ValorantReplayParser.Models')
    'ValorantReplayParser' = @('ValorantReplayParser.Models', 'ValorantReplayParser.Unreal')
}

$nupkgFiles = @(Get-ChildItem -LiteralPath $packageDirectory -Filter '*.nupkg' -File)
$snupkgFiles = @(Get-ChildItem -LiteralPath $packageDirectory -Filter '*.snupkg' -File)
Require ($nupkgFiles.Count -eq 4) "Expected exactly four .nupkg files, found $($nupkgFiles.Count)."
Require ($snupkgFiles.Count -eq 4) "Expected exactly four .snupkg files, found $($snupkgFiles.Count)."

$packageMetadata = @{}
foreach ($package in $packageProjects) {
    $packageFileName = "$($package.Id).$Version.nupkg"
    $symbolFileName = "$($package.Id).$Version.snupkg"
    $packageFile = Join-Path $packageDirectory $packageFileName
    $symbolFile = Join-Path $packageDirectory $symbolFileName
    Require (Test-Path -LiteralPath $packageFile -PathType Leaf) "Missing package file $packageFileName."
    Require (Test-Path -LiteralPath $symbolFile -PathType Leaf) "Missing symbol package $symbolFileName."

    $metadata = Read-PackageArchive $packageFile
    $packageMetadata[$package.Id] = $metadata
    Require ($metadata.Id -eq $package.Id) "Package ID mismatch in ${packageFileName}: $($metadata.Id)."
    Require ($metadata.Version -eq $Version) "Version mismatch in ${packageFileName}: $($metadata.Version)."
    Require ($metadata.Authors -eq 'Michel Giehl') "Authors metadata is missing or incorrect in $packageFileName."
    Require ($metadata.Description -eq $expectedDescriptions[$package.Id]) "Description metadata is missing or incorrect in $packageFileName."
    Require ($metadata.LicenseExpression -eq 'MIT') "MIT license expression is missing from $packageFileName."
    Require ($metadata.Readme -eq 'README.md') "README package metadata is missing from $packageFileName."
    Require ($metadata.ProjectUrl -eq 'https://github.com/michel-giehl/ValorantReplayParser') "Project URL is incorrect in $packageFileName."
    Require ($metadata.RepositoryType -eq 'git') "Repository type is incorrect in $packageFileName."
    Require ($metadata.RepositoryUrl -eq 'https://github.com/michel-giehl/ValorantReplayParser') "Repository URL is incorrect in $packageFileName."
    Require ($metadata.RepositoryCommit -eq $repositoryCommit) "Repository commit is missing or incorrect in $packageFileName."
    Require ($metadata.Entries -contains 'README.md') "README.md is not at the package root in $packageFileName."
    Require ($metadata.Entries -contains 'LICENSE') "LICENSE is not at the package root in $packageFileName."
    Require ($metadata.Entries -contains "lib/net10.0/$($package.Assembly).xml") "XML documentation is missing from $packageFileName."
    Require (-not $metadata.NuspecText.Contains($repositoryRoot, [System.StringComparison]::OrdinalIgnoreCase)) "A workspace path leaked into metadata for $packageFileName."

    $forbiddenEntry = $metadata.Entries | Where-Object {
        $_ -match '(?i)(\.vrf$|\.dmp$|VAL_12_07_DUMP|(^|/)(tests?|CliReader|NetGuidCacheReader|bin|obj)/)'
    } | Select-Object -First 1
    Require ($null -eq $forbiddenEntry) "Development content '$forbiddenEntry' was included in $packageFileName."

    $expectedSiblingDependencies = @()
    if ($expectedDependencies.ContainsKey($package.Id)) {
        $expectedSiblingDependencies = @($expectedDependencies[$package.Id])
    }
    foreach ($dependencyId in $expectedSiblingDependencies) {
        $dependency = $metadata.Dependencies | Where-Object { $_.Id -eq $dependencyId } | Select-Object -First 1
        Require ($null -ne $dependency) "$packageFileName does not depend on renamed package $dependencyId."
        Require ($dependency.Version.Contains($Version)) "$packageFileName references $dependencyId at unexpected version '$($dependency.Version)'."
    }
    $oldPackageDependency = $metadata.Dependencies | Where-Object {
        $_.Id -in @('Replay.Models', 'Replay.Encoding', 'Replay.Unreal', 'Replay.Valorant')
    } | Select-Object -First 1
    Require ($null -eq $oldPackageDependency) "$packageFileName still references an old default project package ID."

    $symbolArchive = [System.IO.Compression.ZipFile]::OpenRead($symbolFile)
    try {
        $portablePdbs = @($symbolArchive.Entries | Where-Object { $_.FullName -match '\.pdb$' })
        Require ($portablePdbs.Count -gt 0) "Portable PDB symbols are missing from $symbolFileName."
        foreach ($portablePdb in $portablePdbs) {
            $pdbStream = $portablePdb.Open()
            try {
                $signature = [byte[]]::new(4)
                $bytesRead = $pdbStream.Read($signature, 0, $signature.Length)
                Require ($bytesRead -eq 4 -and [System.Text.Encoding]::ASCII.GetString($signature) -eq 'BSJB') "Symbol file '$($portablePdb.FullName)' is not a portable PDB."
            }
            finally {
                $pdbStream.Dispose()
            }
        }
    }
    finally {
        $symbolArchive.Dispose()
    }
}

$encodingDependencies = $packageMetadata['ValorantReplayParser.Encoding'].Dependencies
$oozSharp = $encodingDependencies | Where-Object { $_.Id -eq 'OozSharp' } | Select-Object -First 1
Require ($null -ne $oozSharp -and $oozSharp.Version.Contains('3.0.1')) 'The Encoding package does not retain the OozSharp 3.0.1 dependency.'

$nugetConfigPath = Join-Path $smokeDirectory 'NuGet.Config'
$localFeedXml = $packageDirectory.Replace('&', '&amp;').Replace('<', '&lt;').Replace('"', '&quot;')
$nugetConfig = @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local-packages" value="$localFeedXml" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="local-packages">
      <package pattern="ValorantReplayParser*" />
    </packageSource>
    <packageSource key="nuget.org">
      <package pattern="*" />
    </packageSource>
  </packageSourceMapping>
</configuration>
"@
Set-Content -LiteralPath $nugetConfigPath -Value $nugetConfig -Encoding utf8

$smokeProject = Join-Path $repositoryRoot 'tests/PackageConsumerSmoke/PackageConsumerSmoke.csproj'
$isolatedPackages = Join-Path $smokeDirectory 'packages'
$smokeOutput = Join-Path $repositoryRoot 'tests/PackageConsumerSmoke/bin/Release/net10.0/PackageConsumerSmoke.dll'
New-Item -ItemType Directory -Path $isolatedPackages -Force | Out-Null

Invoke-CheckedCommand 'dotnet' @(
    'restore', $smokeProject,
    '--configfile', $nugetConfigPath,
    '--packages', $isolatedPackages,
    '--force',
    '--no-cache',
    "-p:PackageUnderTestVersion=$Version"
)
Invoke-CheckedCommand 'dotnet' @(
    'build', $smokeProject,
    '--configuration', 'Release',
    '--no-incremental',
    '--no-restore',
    "-p:PackageUnderTestVersion=$Version"
)
if (-not (Test-Path -LiteralPath $smokeOutput -PathType Leaf)) {
    throw "Package consumer build did not produce its executable assembly: $smokeOutput"
}
Invoke-CheckedCommand 'dotnet' @($smokeOutput, $ReplayPath)

Write-Host "Verified four $Version packages and an isolated consumer using $ReplayPath."
