# dev-test.ps1 - run the unit tests of TaladMarget. Exits non-zero on the first red suite.
# Drafted by Claude at /dev:stack, accepted by a person. /dev:build reads this exit code as proof, so never swallow it.
#   -App    api | web       run one app only (default: both)
#   -Filter FE-talad-001    run only the tests traced to one unit ([Trait("feature", ...)] on the api side)
param([string]$App = '', [string]$Filter = '')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$ran = 0

# api - xUnit under src/api/tests
if ($App -eq '' -or $App -eq 'api') {
    $sln = Get-ChildItem -Path (Join-Path $root 'src/api') -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Extension -in '.sln', '.slnx' } | Select-Object -First 1
    if ($sln) {
        $testArgs = @('test', $sln.FullName, '--nologo')
        if ($Filter -ne '') { $testArgs += @('--filter', "feature=$Filter") }
        & dotnet @testArgs
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        $ran++
    } else {
        Write-Host "SKIP: src/api has no solution yet - no api tests ran" -ForegroundColor Yellow
    }
}

# web - `npm test` under src/web
if ($App -eq '' -or $App -eq 'web') {
    $web = Join-Path $root 'src/web'
    if (Test-Path (Join-Path $web 'package.json')) {
        Push-Location $web
        try {
            if ($Filter -ne '') { & npm test --silent -- -t $Filter } else { & npm test --silent }
            if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        } finally { Pop-Location }
        $ran++
    } else {
        Write-Host "SKIP: src/web has no package.json yet - no web tests ran" -ForegroundColor Yellow
    }
}

if ($ran -eq 0) {
    Write-Host "NOTHING RAN - no selected app exists yet. This is not a pass." -ForegroundColor Yellow
    exit 2
}
Write-Host "tests green ($ran suite(s))"
