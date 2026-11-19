param([string]$CompilerPath = $env:MIXDROME_CSC)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
if (-not $CompilerPath) { $CompilerPath = Join-Path ${env:ProgramFiles(x86)} 'MSBuild\14.0\Bin\csc.exe' }
$outputDirectory = Join-Path $projectRoot '.tmp\phone-collection-tests'
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
$caliburn = Get-ChildItem -LiteralPath (Join-Path $projectRoot 'packages\Caliburn.Micro.Core.3.2.0\lib') -Filter 'Caliburn.Micro.dll' -Recurse | Sort-Object FullName | Select-Object -First 1
if (-not $caliburn) { throw 'Restore Caliburn.Micro.Core 3.2.0 before running phone source tests.' }
Copy-Item -LiteralPath $caliburn.FullName -Destination $outputDirectory -Force
$sources = Get-Content -LiteralPath (Join-Path $projectRoot 'tools\serializer-tests\sources.txt') | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
$sources += @(
    'Client.Common\Models\Navidrome\NavidromeModels.cs', 'Client.Common\Results\NavidromeResult.cs',
    'Client.Common\Services\Navidrome\NavidromeApi.cs', 'Client.Phone\Services\PhoneCollectionPresentation.cs',
    'Client.Phone\Services\BackgroundAudioMessages.cs', 'Client.Phone\Services\PhoneSettings.cs',
    'Client.Phone\Services\PhoneSubsonicServiceFactory.cs', 'Client.Phone\Services\WP8SubsonicService.cs',
    'tools\serializer-tests\VisualStateResultStub.cs', 'tools\navidrome-tests\ProtocolServer.cs',
    'tools\phone\ConnectionTests.cs', 'tools\phone\CollectionTests.cs'
)
$sources = $sources | ForEach-Object { [IO.Path]::GetFullPath((Join-Path $projectRoot $_)) } | Select-Object -Unique
$albumSource = [IO.File]::ReadAllText((Join-Path $projectRoot 'Client.Phone\Views\AlbumPage.xaml.cs'))
$model = [regex]::Match($albumSource, '(?ms)^    public class SongDisplayItem\r?\n    \{.*?^    \}')
if (-not $model.Success) { throw 'Expected the production SongDisplayItem model.' }
$modelPath = Join-Path $outputDirectory 'SongDisplayItem.cs'
[IO.File]::WriteAllText($modelPath, ('namespace Mixdrome.Phone.Views {' + [Environment]::NewLine + $model.Value + [Environment]::NewLine + '}'), [Text.UTF8Encoding]::new($false))
$executable = Join-Path $outputDirectory 'phone-collections.exe'
& $CompilerPath /nologo /main:PhoneCollectionTests "/out:$executable" @sources $modelPath "/r:$($caliburn.FullName)" /r:System.Net.Http.dll /r:System.ServiceModel.dll /r:System.Runtime.Serialization.dll /r:System.Xml.dll /r:System.Xml.Linq.dll /r:Microsoft.CSharp.dll
if ($LASTEXITCODE -ne 0) { throw 'Production phone collection compilation failed.' }
& $executable
if ($LASTEXITCODE -ne 0) { throw 'Production phone collection assertions failed.' }
