param(
    [string]$RunName = (Get-Date -Format 'yyyyMMdd-HHmmss'),
    [switch]$PrepareOnly
)

$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
if ($RunName -notmatch '^[A-Za-z0-9][A-Za-z0-9_.-]{0,63}$') {
    throw 'RunName must contain only letters, digits, dots, underscores, and hyphens.'
}
$runDirectory = Join-Path $projectRoot "Logs/Performance-$RunName"
if (Test-Path -LiteralPath $runDirectory) {
    throw "Output directory already exists: $runDirectory"
}
New-Item -ItemType Directory -Path $runDirectory | Out-Null
$projectFile = Join-Path $runDirectory 'ChartJudgementBenchmark.csproj'
@'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>disable</Nullable>
    <Optimize>true</Optimize>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="../../Tools/Performance/Program.cs" />
    <Compile Include="../../Assets/Scripts/ChartCoreDomain/*.cs" />
  </ItemGroup>
</Project>
'@ | Set-Content -LiteralPath $projectFile -Encoding utf8

$coreDirectory = Join-Path $projectRoot 'Assets/Scripts/ChartCoreDomain'
function Get-CoreSourceManifest {
    @(Get-ChildItem -LiteralPath $coreDirectory -Filter '*.cs' -File |
        Sort-Object Name | ForEach-Object {
            [ordered]@{
                Path = 'Assets/Scripts/ChartCoreDomain/' + $_.Name
                Sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
            }
        })
}
$sourceFiles = @(Get-CoreSourceManifest)
$cpuName = (Get-ItemProperty -Path 'HKLM:\HARDWARE\DESCRIPTION\System\CentralProcessor\0' `
    -ErrorAction SilentlyContinue).ProcessorNameString
$unityIds = @((Get-Process -Name Unity -ErrorAction SilentlyContinue |
    Select-Object -ExpandProperty Id))
$meta = [ordered]@{
    TimestampUtc = (Get-Date).ToUniversalTime().ToString('o')
    GitHead = (git -C $projectRoot rev-parse HEAD).Trim()
    DotNet = (dotnet --version).Trim()
    CpuName = $cpuName
    ProcessorIdentifier = $env:PROCESSOR_IDENTIFIER
    LogicalProcessors = [Environment]::ProcessorCount
    OsArchitecture = [Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString()
    UnityProcessIdsStart = $unityIds
    DotNetProcessCountStart = @((Get-Process -Name dotnet -ErrorAction SilentlyContinue)).Count
    MSBuildProcessCountStart = @((Get-Process -Name MSBuild -ErrorAction SilentlyContinue)).Count
    TieredCompilation = 'disabled for the benchmark process'
    SourceFiles = $sourceFiles
    HarnessSha256 = (Get-FileHash -Algorithm SHA256 (Join-Path $PSScriptRoot 'Program.cs')).Hash
    RunnerSha256 = (Get-FileHash -Algorithm SHA256 $PSCommandPath).Hash
    Status = if ($PrepareOnly) { 'prepared-only' } else { 'running' }
    Note = 'Standalone .NET ChartCore measurement; not Unity Player frame time.'
}
$environmentFile = Join-Path $runDirectory 'environment.json'
$meta | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $environmentFile -Encoding utf8
if ($PrepareOnly) {
    Write-Host "Prepared without measurement: $runDirectory"
    return
}
$previousTieredCompilation = $env:COMPlus_TieredCompilation
$previousDotNetTieredCompilation = $env:DOTNET_TieredCompilation
try {
    $env:COMPlus_TieredCompilation = '0'
    $env:DOTNET_TieredCompilation = '0'
    dotnet run --project $projectFile --configuration Release -- $runDirectory |
        Tee-Object -FilePath (Join-Path $runDirectory 'console.txt')
    if ($LASTEXITCODE -ne 0) { throw "Benchmark failed: exit $LASTEXITCODE" }
    $meta.Status = 'complete'
} catch {
    $meta.Status = 'failed'
    throw
} finally {
    if ($null -eq $previousTieredCompilation) {
        Remove-Item Env:COMPlus_TieredCompilation -ErrorAction SilentlyContinue
    } else {
        $env:COMPlus_TieredCompilation = $previousTieredCompilation
    }
    if ($null -eq $previousDotNetTieredCompilation) {
        Remove-Item Env:DOTNET_TieredCompilation -ErrorAction SilentlyContinue
    } else {
        $env:DOTNET_TieredCompilation = $previousDotNetTieredCompilation
    }
    $meta.UnityProcessIdsEnd = @((Get-Process -Name Unity -ErrorAction SilentlyContinue |
        Select-Object -ExpandProperty Id))
    $meta.DotNetProcessCountEnd = @((Get-Process -Name dotnet -ErrorAction SilentlyContinue)).Count
    $meta.MSBuildProcessCountEnd = @((Get-Process -Name MSBuild -ErrorAction SilentlyContinue)).Count
    $meta.SourceChangedDuringRun =
        ($sourceFiles | ConvertTo-Json -Compress -Depth 3) -ne
        ((Get-CoreSourceManifest) | ConvertTo-Json -Compress -Depth 3)
    $interference = $meta.UnityProcessIdsStart.Count -gt 0 -or
        $meta.UnityProcessIdsEnd.Count -gt 0 -or
        $meta.DotNetProcessCountStart -gt 0 -or
        $meta.DotNetProcessCountEnd -gt 0 -or
        $meta.MSBuildProcessCountStart -gt 0 -or
        $meta.MSBuildProcessCountEnd -gt 0
    $meta.TimingInterpretation = if ($meta.SourceChangedDuringRun) {
        'invalid: source changed during run'
    } elseif ($interference) {
        'provisional: Unity or build-related process present'
    } else {
        'no recorded Unity or build-related interference'
    }
    $meta | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $environmentFile -Encoding utf8
}
Write-Host "Results: $runDirectory"
