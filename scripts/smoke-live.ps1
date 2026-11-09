param([string]$Filter = '')
$ErrorActionPreference = 'Stop'
$repository = Split-Path -Parent $PSScriptRoot
# No running host or live connection is used. Fixtures enforce disposable database names,
# select Development email, and disable Demo Mode.
& node (Join-Path $repository 'figma-reference/verify.cjs')
if ($LASTEXITCODE -ne 0) { throw 'Figma reference checks failed.' }
$testArguments = @('test', (Join-Path $repository 'CampusGear/CampusGear.IntegrationTests'), '--verbosity', 'quiet')
if ($Filter) { $testArguments += @('--filter', $Filter) }
& dotnet @testArguments
if ($LASTEXITCODE -ne 0) { throw 'Isolated CampusGear checks failed.' }
Write-Host 'Current registration approval, permissions, HTTP/SQL, and architecture checks passed in disposable databases.'
