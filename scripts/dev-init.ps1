# dev-init.ps1 - prepare a dev machine for TaladMarget (api: .NET 10 , web: Next.js 16 , db: PostgreSQL 18)
# Drafted by Claude at /dev:stack, accepted by a person. Edit freely - this is the project's file, not the plugin's.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$failed = $false

function Need($cmd, $hint) {
    if (-not (Get-Command $cmd -ErrorAction SilentlyContinue)) {
        Write-Host "MISSING: $cmd - $hint" -ForegroundColor Red
        $script:failed = $true
    } else {
        Write-Host "ok  $cmd"
    }
}

Need 'dotnet' 'install the .NET 10 SDK'
Need 'node'   'install Node.js LTS (Next.js 16)'
Need 'npm'    'comes with Node.js'
Need 'docker' 'install Docker Desktop (PostgreSQL 18 runs in compose)'
if ($failed) { exit 1 }

$sdk = (& dotnet --list-sdks) -join "`n"
if ($sdk -notmatch '(?m)^10\.') { Write-Host "MISSING: .NET 10 SDK - dotnet --list-sdks shows none" -ForegroundColor Red; exit 1 }

# api - restore only once the solution exists
$sln = Get-ChildItem -Path (Join-Path $root 'src/api') -File -ErrorAction SilentlyContinue | Where-Object { $_.Extension -in '.sln', '.slnx' } | Select-Object -First 1
if ($sln) {
    & dotnet restore $sln.FullName
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
} else {
    Write-Host "SKIP: src/api has no solution yet - nothing to restore" -ForegroundColor Yellow
}

# web - install only once package.json exists
$web = Join-Path $root 'src/web'
if (Test-Path (Join-Path $web 'package.json')) {
    Push-Location $web
    try {
        if (Test-Path 'package-lock.json') { & npm ci } else { & npm install }
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    } finally { Pop-Location }
} else {
    Write-Host "SKIP: src/web has no package.json yet - nothing to install" -ForegroundColor Yellow
}

# database - bring up PostgreSQL 18 only once compose exists (/dev:deploy writes it)
$compose = Join-Path $root 'docker/docker-compose.yml'
if (Test-Path $compose) {
    & docker compose -f $compose up -d db
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
} else {
    Write-Host "SKIP: docker/docker-compose.yml not written yet - /dev:deploy puts it there" -ForegroundColor Yellow
}

Write-Host "init done"
