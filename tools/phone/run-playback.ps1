param([string]$CompilerPath = $env:MIXDROME_CSC)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
if (-not $CompilerPath) { $CompilerPath = Join-Path ${env:ProgramFiles(x86)} 'MSBuild\14.0\Bin\csc.exe' }
if (-not (Test-Path -LiteralPath $CompilerPath -PathType Leaf)) { throw 'Supply -CompilerPath or MIXDROME_CSC for the C# compiler.' }
$outputDirectory = Join-Path $projectRoot '.tmp\phone-playback-tests'
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
$sources = @(
    'Client.Phone\Services\BackgroundAudioMessages.cs',
    'Client.Phone\Services\AudioQueueState.cs',
    'tools\phone\PlaybackTests.cs'
) | ForEach-Object { [IO.Path]::GetFullPath((Join-Path $projectRoot $_)) }
$executable = Join-Path $outputDirectory 'phone-playback.exe'
& $CompilerPath /nologo /main:PhonePlaybackTests "/out:$executable" @sources /r:System.Runtime.Serialization.dll
if ($LASTEXITCODE -ne 0) { throw 'Production phone audio compilation failed.' }
& $executable
if ($LASTEXITCODE -ne 0) { throw 'Production phone audio assertions failed.' }

# Compile the actual page methods, not a second implementation of the state
# reducer. Indented method boundaries keep the extracted bodies byte-for-byte
# intact; changing a signature or adding an overload requires reviewing this list.
$pageSource = [IO.File]::ReadAllText((Join-Path $projectRoot 'Client.Phone\Views\PlaybackPage.xaml.cs'))
$pageMethods = @('ApplyStateAsync', 'ShowTrack', 'OnArtworkImageFailed', 'DetachArtwork', 'ResetDisplay', 'UpdatePlaybackState', 'UpdateProgress', 'ApplyProgress', 'ResetProgress', 'IsMediaNotReady', 'OnProgressTimerTick', 'FormatTime', 'Send', 'OnNavigatedTo', 'OnNavigatedFrom', 'OnQueueListLoaded', 'OnQueueListUnloaded', 'DetachQueueItems', 'OnQueueItemsChanged', 'SynchronizeQueueSelection', 'BeginTrackPresentation', 'IsCurrentPresentation', 'MatchesPlaybackAccount', 'OnArtistImageFailed', 'DetachArtistImage', 'UpdateFeedbackControls', 'SaveFeedbackAsync', 'LoadLyricsAsync', 'OnShareDataRequested', 'CreateLocalQueueServiceAsync', 'OnArtistImageOpened', 'OnPendingArtistImageFailed', 'DetachPendingArtistImage', 'ClearArtistBackground', 'CanSaveOfflineMix', 'UpdateOfflineMixCommand', 'StartOfflineMixAsync')
$extractedMethods = foreach ($method in $pageMethods) {
    $methodPattern = '(?ms)^        (?:private|protected) [^\r\n]*\b' + [regex]::Escape($method) + '\([^\r\n]*\)\r?\n        \{.*?^        \}'
    $methodMatches = [regex]::Matches($pageSource, $methodPattern)
    if ($methodMatches.Count -ne 1) { throw "Expected one production PlaybackPage.$method method." }
    $methodMatches[0].Value
}
$fixture = [IO.File]::ReadAllText((Join-Path $projectRoot 'tools\phone\PlayerStateTests.cs'))
$marker = '/* PRODUCTION_PAGE_METHODS */'
if ($fixture.Split(@($marker), [StringSplitOptions]::None).Length -ne 2) { throw 'Player state fixture must contain exactly one method insertion marker.' }
$pageHarness = Join-Path $outputDirectory 'PlayerStateHarness.cs'
[IO.File]::WriteAllText($pageHarness, $fixture.Replace($marker, ($extractedMethods -join [Environment]::NewLine)), [Text.UTF8Encoding]::new($false))
$pageExecutable = Join-Path $outputDirectory 'phone-player-state.exe'
$pageSources = @(
    (Join-Path $projectRoot 'Client.Phone\Services\BackgroundAudioMessages.cs'),
    (Join-Path $projectRoot 'Client.Phone\Services\AudioQueueState.cs'),
    (Join-Path $projectRoot 'Client.Common\Services\AccountIdentity.cs'),
    $pageHarness
)
& $CompilerPath /nologo /main:PhonePlayerStateTests "/out:$pageExecutable" @pageSources /r:System.Runtime.Serialization.dll
if ($LASTEXITCODE -ne 0) { throw 'Production phone player state compilation failed.' }
& $pageExecutable
if ($LASTEXITCODE -ne 0) { throw 'Production phone player state assertions failed.' }
