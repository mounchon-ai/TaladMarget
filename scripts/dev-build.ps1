# dev-build.ps1 - Release build of both apps of TaladMarget. Exits non-zero on the first failure.
# Drafted by Claude at /dev:stack, accepted by a person.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$built = 0

$sln = Get-ChildItem -Path (Join-Path $root 'src/api') -Filter *.sln -ErrorAction SilentlyContinue | Select-Object -First 1
if ($sln) {
    & dotnet build $sln.FullName -c Release --nologo
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    $built++
} else {
    Write-Host "SKIP: src/api has no .sln yet - api not built" -ForegroundColor Yellow
}

$web = Join-Path $root 'src/web'
if (Test-Path (Join-Path $web 'package.json')) {
    Push-Location $web
    try {
        & npm run build
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    } finally { Pop-Location }
    $built++
} else {
    Write-Host "SKIP: src/web has no package.json yet - web not built" -ForegroundColor Yellow
}

if ($built -eq 0) {
    Write-Host "NOTHING BUILT - neither app exists yet. This is not a pass." -ForegroundColor Yellow
    exit 2
}
Write-Host "build done ($built app(s))"
