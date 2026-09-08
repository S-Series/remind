[CmdletBinding()]
param(
    [string]$UnityPath = '',
    [string]$ResultsDirectory = '',
    [switch]$CoreOnly
)

$ErrorActionPreference = 'Stop'
$projectDirectory = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$versionFile = Join-Path $projectDirectory 'ProjectSettings/ProjectVersion.txt'
$versionMatch = Select-String -LiteralPath $versionFile -Pattern '^m_EditorVersion: (.+)$'
$projectVersion = $versionMatch.Matches[0].Groups[1].Value.Trim()
if ([string]::IsNullOrWhiteSpace($UnityPath)) {
    $UnityPath = Join-Path ${env:ProgramFiles} "Unity/Hub/Editor/$projectVersion/Editor/Unity.exe"
}
if (!(Test-Path -LiteralPath $UnityPath -PathType Leaf)) {
    throw "Unity $projectVersion was not found. Supply -UnityPath to the existing matching installation."
}
if (Get-Process -Name Unity -ErrorAction SilentlyContinue) {
    throw 'Close Unity before running the batch baseline. This script never terminates an open editor.'
}
if ([string]::IsNullOrWhiteSpace($ResultsDirectory)) {
    $runName = (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
    # Unity may clean its Temp directory while exiting, including a test XML it
    # just wrote. Logs is ignored by git but persists after the batch process.
    $ResultsDirectory = Join-Path $projectDirectory "Logs/EffectBaselineResults/$runName"
}
$resultDirectory = [IO.Path]::GetFullPath($ResultsDirectory)
if (Test-Path -LiteralPath $resultDirectory) {
    throw 'Use a new results directory so an old report cannot be mistaken for this run.'
}
New-Item -ItemType Directory -Path $resultDirectory | Out-Null
$xmlPath = Join-Path $resultDirectory 'results.xml'
$logPath = Join-Path $resultDirectory 'unity.log'
# Start-Process joins arguments into a command line; quote each filesystem argument explicitly.
$arguments = @('-batchmode', '-nographics', '-projectPath', ('"' + $projectDirectory + '"'),
    '-runTests', '-testPlatform', 'EditMode', '-testResults', ('"' + $xmlPath + '"'),
    '-logFile', ('"' + $logPath + '"'))
if ($CoreOnly) { $arguments += @('-assemblyNames', 'REmind.ChartCore.Tests') }
Write-Host "Running Unity $projectVersion EditMode baseline. Log: $logPath"
$testProcess = Start-Process -FilePath $UnityPath -ArgumentList $arguments -WindowStyle Hidden -PassThru
# Start-Process -Wait can wait for Unity's persistent licensing descendants on
# Windows. Wait on the editor process itself so batch completion is deterministic.
$testProcess.WaitForExit()
$testExitCode = $testProcess.ExitCode
if (!(Test-Path -LiteralPath $xmlPath)) {
    throw "Unity exited with code $testExitCode without test results. Inspect $logPath"
}
[xml]$results = Get-Content -LiteralPath $xmlPath -Raw
$run = $results.'test-run'
Write-Host "Result=$($run.result) Total=$($run.total) Passed=$($run.passed) Failed=$($run.failed) Skipped=$($run.skipped)"
foreach ($failure in $results.SelectNodes('//test-case[@result="Failed"]')) {
    Write-Host "FAIL: $($failure.fullname)"
    Write-Host $failure.failure.message
}
Write-Host "Report: $xmlPath"
# Known defects remain genuine failures. Do not hide them or mark them passed/ignored.
if ([int]$run.failed -gt 0 -or $run.result -ne 'Passed') { exit 2 }
if ($testExitCode -ne 0) { exit $testExitCode }
exit 0
