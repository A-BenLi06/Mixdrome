param(
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Debug',
    [ValidateSet('AnyCPU', 'x86', 'x64', 'ARM')][string]$Architecture = 'x86',
    [string]$MSBuildPath = $env:MIXDROME_MSBUILD,
    [switch]$NoBuild,
    [switch]$NoLaunch,
    [switch]$NativeUiProbe,
    [switch]$NativePlaybackProbe,
    [switch]$NativeUiObserve
)

$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$clientRoot = Join-Path $projectRoot 'Client'
if ($NativePlaybackProbe) { $NativeUiProbe = $true }
if (($NativeUiProbe -or $NativeUiObserve) -and ($Configuration -ne 'Debug' -or $NoBuild)) { throw 'Native UI diagnostics require a fresh Debug build.' }
if ($NativeUiProbe -and $NativeUiObserve) { throw 'Choose either the scripted probe or the passive observer.' }
function Stop-WorkspaceClient {
    $clientProject = [xml](Get-Content -LiteralPath (Join-Path $clientRoot 'Mixdrome.csproj') -Raw)
    $processName = $clientProject.SelectSingleNode('//*[local-name()="AssemblyName"]').InnerText
    Get-Process -Name $processName -ErrorAction SilentlyContinue |
        Where-Object { $_.Path -and $_.Path.StartsWith($projectRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) } |
        Stop-Process -Force
}
if (-not $MSBuildPath) {
    $MSBuildPath = Join-Path ${env:ProgramFiles(x86)} 'MSBuild\14.0\Bin\MSBuild.exe'
}
$outputRoot = Join-Path $projectRoot '.tmp\desktop'
New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
$modeFile = Join-Path $outputRoot "build-mode-$Architecture-$Configuration.txt"
$buildMode = if ($NativeUiObserve) { 'native-ui-observer' } elseif ($NativePlaybackProbe) { 'native-ui-playback-probe' } elseif ($NativeUiProbe) { 'native-ui-probe' } else { 'normal' }
$previousMode = if (Test-Path -LiteralPath $modeFile) { (Get-Content -LiteralPath $modeFile -Raw).Trim() } else { '' }
if ($NoBuild -and $previousMode -like 'native-ui-*') { throw 'The last build contains native UI diagnostics. Run a normal build before using -NoBuild.' }
if (-not $NoBuild) {
    Stop-WorkspaceClient
    if (-not (Test-Path -LiteralPath $MSBuildPath)) { throw 'Supply -MSBuildPath for the legacy Windows 8.1 build toolchain.' }
    $buildLog = Join-Path $outputRoot 'build.log'
    # MSBuild 14 can reuse CoreCompile outputs and previously packaged appx files
    # across incremental edits or probe define toggles. Always rebuild and pack.
    $buildTarget = 'Rebuild'
    & $MSBuildPath (Join-Path $clientRoot 'Mixdrome.csproj') "/t:$buildTarget" "/p:Configuration=$Configuration" "/p:Platform=$Architecture" "/p:GenerateAppxPackageOnBuild=true" "/p:NativeUiProbe=$(($NativeUiProbe -or $NativeUiObserve).ToString().ToLowerInvariant())" "/p:NativeUiObserve=$($NativeUiObserve.IsPresent.ToString().ToLowerInvariant())" "/p:NativePlaybackProbe=$($NativePlaybackProbe.IsPresent.ToString().ToLowerInvariant())" /m /v:quiet /nologo /fl "/flp:logfile=$buildLog;verbosity=normal"
    if ($LASTEXITCODE -ne 0) { throw "Desktop build failed. See $buildLog" }
    Set-Content -LiteralPath $modeFile -Value $buildMode -Encoding utf8NoBOM
}
$builtOutput = if ($Architecture -eq 'AnyCPU') { "bin\$Configuration" } else { "bin\$Architecture\$Configuration" }
$builtManifest = Join-Path $clientRoot (Join-Path $builtOutput 'AppxManifest.xml')
if (-not (Test-Path -LiteralPath $builtManifest)) { throw 'Build output is missing.' }
$identity = ([xml](Get-Content -LiteralPath $builtManifest -Raw)).Package.Identity
Import-Module Appx -UseWindowsPowerShell -WarningAction SilentlyContinue
$existingRegistration = Get-AppxPackage -Name $identity.Name -ErrorAction Stop | Sort-Object Version -Descending | Select-Object -First 1
$package = $null
foreach ($candidate in (Get-ChildItem -LiteralPath (Join-Path $clientRoot 'AppPackages') -Filter '*.appx' -Recurse | Sort-Object LastWriteTime -Descending)) {
    $archive = [IO.Compression.ZipFile]::OpenRead($candidate.FullName)
    try {
        $entry = $archive.GetEntry('AppxManifest.xml')
        if (-not $entry) { continue }
        $reader = [IO.StreamReader]::new($entry.Open())
        try { $candidateIdentity = ([xml]$reader.ReadToEnd()).Package.Identity } finally { $reader.Dispose() }
        if ($candidateIdentity.Name -eq $identity.Name -and $candidateIdentity.Version -eq $identity.Version -and
            $candidateIdentity.ProcessorArchitecture -eq $identity.ProcessorArchitecture) {
            $package = $candidate
            break
        }
    } finally { $archive.Dispose() }
}
if (-not $package) { throw 'No appx matches the built package identity; stale packages will not be deployed.' }
$builtExe = Join-Path $clientRoot (Join-Path $builtOutput 'Mixdrome.exe')
if (-not (Test-Path -LiteralPath $builtExe)) { throw "Built binary is missing: $builtExe" }
$builtExeHash = (Get-FileHash -LiteralPath $builtExe -Algorithm SHA256).Hash
$archive = [IO.Compression.ZipFile]::OpenRead($package.FullName)
try {
    $exeEntry = $archive.GetEntry('Mixdrome.exe')
    if (-not $exeEntry) { throw "Package $($package.FullName) does not contain Mixdrome.exe." }
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        $entryStream = $exeEntry.Open()
        try {
            $packagedHash = [BitConverter]::ToString($sha.ComputeHash($entryStream)).Replace('-', '')
        } finally {
            $entryStream.Dispose()
        }
    } finally {
        $sha.Dispose()
    }
} finally {
    $archive.Dispose()
}
if ($packagedHash -ne $builtExeHash) {
    throw "Selected appx $($package.FullName) holds stale Mixdrome.exe ($packagedHash != $builtExeHash)."
}
$layoutRoot = Join-Path $outputRoot ([Guid]::NewGuid().ToString('N'))
if ($existingRegistration) {
    $registeredRoot = [IO.Path]::GetFullPath($existingRegistration.InstallLocation)
    if ($registeredRoot.StartsWith($projectRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        $layoutRoot = $registeredRoot
        Stop-WorkspaceClient
    } elseif ([version]$identity.Version -le [version]$existingRegistration.Version) {
        throw 'The existing app is registered outside this workspace. Build a higher package version before changing its registration.'
    }
}
New-Item -ItemType Directory -Path $layoutRoot -Force | Out-Null
$archive = [IO.Compression.ZipFile]::OpenRead($package.FullName)
$unchangedFiles = 0
$updatedFiles = 0
try {
    foreach ($entry in $archive.Entries) {
        if (-not $entry.Name) { continue }
        if ($entry.FullName -in @('AppxBlockMap.xml', 'AppxSignature.p7x', 'AppxMetadata/CodeIntegrity.cat')) { continue }
        $destination = [IO.Path]::GetFullPath((Join-Path $layoutRoot $entry.FullName))
        if (-not $destination.StartsWith($layoutRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
            throw 'An appx entry escapes the deployment layout.'
        }
        New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($destination)) -Force | Out-Null
        if ((Test-Path -LiteralPath $destination) -and (Get-Item -LiteralPath $destination).Length -eq $entry.Length) {
            $entryStream = $entry.Open()
            $hasher = [Security.Cryptography.SHA256]::Create()
            try { $entryHash = [BitConverter]::ToString($hasher.ComputeHash($entryStream)).Replace('-', '') }
            finally { $entryStream.Dispose(); $hasher.Dispose() }
            if ((Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash -eq $entryHash) {
                $unchangedFiles++
                continue
            }
        }
        [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $destination, $true)
        $updatedFiles++
    }
} finally { $archive.Dispose() }
$layoutManifest = Join-Path $layoutRoot 'AppxManifest.xml'
$registrationReused = $existingRegistration -and $existingRegistration.IsDevelopmentMode -and
    ([version]$existingRegistration.Version -eq [version]$identity.Version) -and ($updatedFiles -eq 0)
if (-not $registrationReused) {
    Add-AppxPackage -Register $layoutManifest -ForceApplicationShutdown -ForceUpdateFromAnyVersion -ErrorAction Stop
}
$registered = Get-AppxPackage -Name $identity.Name -ErrorAction Stop | Where-Object Version -eq $identity.Version | Select-Object -First 1
if (-not $registered) { throw 'Package registration did not produce the expected identity.' }
if ([IO.Path]::GetFullPath($registered.InstallLocation) -ne $layoutRoot) { throw 'The active registration points to a different layout.' }
$applicationId = @(([xml](Get-Content -LiteralPath $layoutManifest -Raw)).Package.Applications.Application)[0].Id
$activationId = $registered.PackageFamilyName + '!' + $applicationId
if (-not $NoLaunch) { Start-Process -FilePath ('shell:AppsFolder\' + $activationId) -WindowStyle Hidden }
[pscustomobject]@{ Package=$package.FullName; Layout=$layoutRoot; Manifest=$layoutManifest; ActivationId=$activationId; RegistrationReused=[bool]$registrationReused; UnchangedFiles=$unchangedFiles; Launched=(-not $NoLaunch) } | ConvertTo-Json
