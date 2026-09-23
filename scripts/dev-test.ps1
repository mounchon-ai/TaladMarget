# dev-test.ps1 - run every unit test of TaladMarget. Exits non-zero on the first red suite.
# Drafted by Claude at /dev:stack, accepted by a person. /dev:build reads this exit code as proof, so never swallow it.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$ran = 0

# api - xUnit (or whatever the solution declares) under src/api/tests
$sln = Get-ChildItem -Path (Join-Path $root 'src/api') -Filter *.sln -ErrorAction SilentlyContinue | Select-Object -First 1
if ($sln) {
    & dotnet test $sln.FullName --nologo
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    $ran++
} else {
    Write-Host "SKIP: src/api has no .sln yet - no api tests ran" -ForegroundColor Yellow
}

# web - `npm test` under src/web
$web = Join-Path $root 'src/web'
if (Test-Path (Join-Path $web 'package.json')) {
    Push-Location $web
    try {
        & npm test --silent
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    } finally { Pop-Location }
    $ran++
} else {
    Write-Host "SKIP: src/web has no package.json yet - no web tests ran" -ForegroundColor Yellow
}

if ($ran -eq 0) {
    Write-Host "NOTHING RAN - neither app exists yet. This is not a pass." -ForegroundColor Yellow
    exit 2
}
Write-Host "tests green ($ran suite(s))"
