param([string]$CompilerPath = $env:MIXDROME_CSC)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
if (-not $CompilerPath) { $CompilerPath = Join-Path ${env:ProgramFiles(x86)} 'MSBuild\14.0\Bin\csc.exe' }
$outputDirectory = Join-Path $projectRoot '.tmp\phone-offline-tests'
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
$storageDirectory = Join-Path $outputDirectory ([guid]::NewGuid().ToString('N').Substring(0, 8))
$caliburn = Get-ChildItem -LiteralPath (Join-Path $projectRoot 'packages\Caliburn.Micro.Core.3.2.0\lib') -Filter 'Caliburn.Micro.dll' -Recurse | Sort-Object FullName | Select-Object -First 1
if (-not $caliburn) { throw 'Restore Caliburn.Micro.Core 3.2.0 before running phone source tests.' }
Copy-Item -LiteralPath $caliburn.FullName -Destination $outputDirectory -Force
$sources = Get-Content -LiteralPath (Join-Path $projectRoot 'tools\serializer-tests\sources.txt') | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
$sources += @(
    'Client.Common\Models\Navidrome\NavidromeModels.cs', 'Client.Common\Results\NavidromeResult.cs',
    'Client.Common\Services\Navidrome\NavidromeApi.cs', 'Client.Common\Services\Navidrome\MediaDownload.cs',
    'Client.Common\Services\Navidrome\LocalXml.cs', 'Client.Common\Models\Navidrome\OfflineCatalog.cs',
    'Client.Phone\Services\PhoneOfflineManifest.cs', 'Client.Phone\Services\PhoneOfflineMixes.cs',
    'Client.Phone\Common\PhoneText.cs', 'tools\serializer-tests\VisualStateResultStub.cs',
    'tools\navidrome-tests\ProtocolServer.cs', 'tools\phone\OfflineStorageAdapters.cs', 'tools\phone\OfflineMixTests.cs'
)
$sources = $sources | ForEach-Object { [IO.Path]::GetFullPath((Join-Path $projectRoot $_)) } | Select-Object -Unique
$executable = Join-Path $outputDirectory 'phone-offline.exe'
& $CompilerPath /nologo /main:PhoneOfflineMixTests "/out:$executable" @sources "/r:$($caliburn.FullName)" /r:System.Net.Http.dll /r:System.ServiceModel.dll /r:System.Runtime.Serialization.dll /r:System.Xml.dll /r:System.Xml.Linq.dll /r:Microsoft.CSharp.dll
if ($LASTEXITCODE -ne 0) { throw 'Production phone offline service compilation failed.' }
& $executable $storageDirectory
if ($LASTEXITCODE -ne 0) { throw 'Production phone offline service assertions failed.' }

# Exercise the actual page's asynchronous selection and refresh methods with
# controlled validation completions; the fixture supplies only managed controls.
$pageSource = [IO.File]::ReadAllText((Join-Path $projectRoot 'Client.Phone\Views\OfflineMixesPage.xaml.cs'))
$pageMethods = @('OpenMixAsync', 'CurrentSelection', 'Refresh', 'OnNavigatedFrom', 'SetStatus')
$extractedMethods = foreach ($method in $pageMethods) {
    $pattern = '(?ms)^        (?:private|protected) [^\r\n]*\b' + [regex]::Escape($method) + '\([^\r\n]*\)\r?\n        \{.*?^        \}'
    $pageMethodMatches = [regex]::Matches($pageSource, $pattern)
    if ($pageMethodMatches.Count -ne 1) { throw "Expected one production OfflineMixesPage.$method method." }
    $pageMethodMatches[0].Value
}
$fixture = [IO.File]::ReadAllText((Join-Path $projectRoot 'tools\phone\OfflinePageSelectionTests.cs'))
$marker = '/* PRODUCTION_PAGE_METHODS */'
if ($fixture.Split(@($marker), [StringSplitOptions]::None).Length -ne 2) { throw 'Offline page fixture must contain one method insertion marker.' }
$pageHarness = Join-Path $outputDirectory 'OfflinePageHarness.cs'
[IO.File]::WriteAllText($pageHarness, $fixture.Replace($marker, ($extractedMethods -join [Environment]::NewLine)), [Text.UTF8Encoding]::new($false))
$pageExecutable = Join-Path $outputDirectory 'phone-offline-page.exe'
& $CompilerPath /nologo /main:PhoneOfflinePageSelectionTests "/out:$pageExecutable" $pageHarness
if ($LASTEXITCODE -ne 0) { throw 'Production phone offline page compilation failed.' }
& $pageExecutable
if ($LASTEXITCODE -ne 0) { throw 'Production phone offline page selection assertions failed.' }
