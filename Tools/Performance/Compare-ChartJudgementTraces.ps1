param(
    [Parameter(Mandatory = $true)][string]$BaselineDirectory,
    [Parameter(Mandatory = $true)][string]$CandidateDirectory
)

$ErrorActionPreference = 'Stop'
$baseline = (Resolve-Path -LiteralPath $BaselineDirectory).Path
$candidate = (Resolve-Path -LiteralPath $CandidateDirectory).Path
$files = @(Get-ChildItem -LiteralPath $baseline -File |
    Where-Object { $_.Name -like '*-inputs.csv' -or $_.Name -like '*-trace.csv' })
if ($files.Count -eq 0) { throw "No input or trace files in $baseline" }
$mismatches = @()
$baselineNames = @($files | ForEach-Object { $_.Name })
$candidateNames = @(Get-ChildItem -LiteralPath $candidate -File |
    Where-Object { $_.Name -like '*-inputs.csv' -or $_.Name -like '*-trace.csv' } |
    ForEach-Object { $_.Name })
foreach ($extra in $candidateNames) {
    if ($extra -notin $baselineNames) { $mismatches += "Unexpected: $extra" }
}
foreach ($file in $files) {
    $other = Join-Path $candidate $file.Name
    if (-not (Test-Path -LiteralPath $other)) {
        $mismatches += "Missing: $($file.Name)"
        continue
    }
    $leftHash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
    $rightHash = (Get-FileHash -LiteralPath $other -Algorithm SHA256).Hash
    if ($leftHash -ne $rightHash) {
        $mismatches += "Different: $($file.Name)"
    }
}
if ($mismatches.Count -gt 0) {
    $mismatches | ForEach-Object { Write-Warning $_ }
    throw "$($mismatches.Count) input/trace file(s) differ."
}
Write-Host "$($files.Count) input/trace files match byte for byte."
