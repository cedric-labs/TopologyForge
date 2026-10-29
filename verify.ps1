[CmdletBinding()]
param(
    [string]$ProjectKey = "TopologyForge",
    [string]$SonarHost = $(if ($env:SONAR_HOST_URL) { $env:SONAR_HOST_URL } else { "http://localhost:9000" }),
    [string]$SonarToken,
    [switch]$SkipFormat,
    [switch]$SkipSonar
)

$ErrorActionPreference = "Stop"
$ScriptRoot = $PSScriptRoot
Set-Location $ScriptRoot

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "  Quality & SonarQube pipeline - TopologyForge" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

$SolutionPath = Join-Path $ScriptRoot "TopologyForge.slnx"

# Automatic detection of the SonarQube token
if (-not $SonarToken) {
    if ($env:SONAR_TOKEN) {
        $SonarToken = $env:SONAR_TOKEN
    } elseif (Test-Path "$ScriptRoot/.sonar-token") {
        $SonarToken = (Get-Content "$ScriptRoot/.sonar-token" -Raw).Trim()
    } elseif (Test-Path "$ScriptRoot/../.sonar-token") {
        $SonarToken = (Get-Content "$ScriptRoot/../.sonar-token" -Raw).Trim()
    }
}

# 1. Restore the local dotnet tools
Write-Host "`n[1/6] Restoring local tools..." -ForegroundColor Yellow
dotnet tool restore
if ($LASTEXITCODE -ne 0) {
    Write-Error "'dotnet tool restore' failed."
    exit $LASTEXITCODE
}

# 2. Check the code formatting
if (-not $SkipFormat) {
    Write-Host "`n[2/6] Checking code formatting (dotnet format)..." -ForegroundColor Yellow
    dotnet format $SolutionPath --verify-no-changes
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Formatting check failed. Run 'dotnet format' to fix the differences."
        exit $LASTEXITCODE
    }
    Write-Host "  Formatage conforme." -ForegroundColor Green
} else {
    Write-Host "`n[2/6] Formatting check skipped." -ForegroundColor DarkGray
}

# Remove the results of previous test runs and analyses
Remove-Item -Path "$ScriptRoot/TestResults" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -Path "$ScriptRoot/.sonarqube" -Recurse -Force -ErrorAction SilentlyContinue

# Coverage of the map script, measured by the Playwright E2E tests (see TopologyForge.Tests/E2E/ScriptCoverage)
$JsCoverageReport = Join-Path $ScriptRoot "TestResults/js-coverage/lcov.info"
$env:TOPOLOGYFORGE_JS_LCOV_PATH = $JsCoverageReport

# 3. SonarScanner Begin
$useSonar = (-not $SkipSonar) -and (-not [string]::IsNullOrWhiteSpace($SonarToken))
if ($useSonar) {
    Write-Host "`n[3/6] Initializing SonarScanner on $SonarHost..." -ForegroundColor Yellow
    $sonarArgs = @(
        "begin",
        "/k:$ProjectKey",
        "/n:$ProjectKey",
        "/d:sonar.host.url=$SonarHost",
        "/d:sonar.token=$SonarToken",
        "/d:sonar.qualitygate.wait=true",
        "/d:sonar.cs.opencover.reportsPaths=**/TestResults/**/coverage.opencover.xml",
        "/d:sonar.cs.vstest.reportsPaths=**/TestResults/**/*.trx",
        "/d:sonar.javascript.lcov.reportPaths=$JsCoverageReport",
        "/d:sonar.exclusions=**/bin/**,**/obj/**,verify.ps1",
        "/d:sonar.coverage.exclusions=verify.ps1",
        "/d:sonar.cpd.exclusions=verify.ps1",
        "/d:sonar.cs.roslyn.ignoreIssues=true"
    )
    dotnet sonarscanner @sonarArgs
    if ($LASTEXITCODE -ne 0) {
        Write-Error "SonarScanner initialization failed."
        exit $LASTEXITCODE
    }
} else {
    Write-Host "`n[3/6] SonarScanner skipped (no token found or -SkipSonar set)." -ForegroundColor DarkGray
}

# 4. Release build with zero warnings
Write-Host "`n[4/6] Non-incremental Release build (-warnaserror)..." -ForegroundColor Yellow
dotnet build $SolutionPath -warnaserror --configuration Release --no-incremental
if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed or warnings were reported."
    exit $LASTEXITCODE
}
Write-Host "  Build succeeded with no warnings." -ForegroundColor Green

# 5. Run the tests and collect OpenCover coverage
Write-Host "`n[5/6] Running unit tests with OpenCover coverage..." -ForegroundColor Yellow
$settingsArg = @()
if (Test-Path "$ScriptRoot/coverlet.runsettings") {
    $settingsArg = @("--settings", "$ScriptRoot/coverlet.runsettings")
}
dotnet test $SolutionPath --no-build --configuration Release --logger "trx" --collect:"XPlat Code Coverage;Format=opencover" --results-directory "$ScriptRoot/TestResults" @settingsArg
if ($LASTEXITCODE -ne 0) {
    Write-Error "Unit or architecture tests failed."
    exit $LASTEXITCODE
}
Write-Host "  All tests passed." -ForegroundColor Green

# 6. SonarScanner End
if ($useSonar) {
    Write-Host "`n[6/6] Finishing the SonarScanner analysis..." -ForegroundColor Yellow
    dotnet sonarscanner end /d:sonar.token=$SonarToken
    if ($LASTEXITCODE -ne 0) {
        Write-Error "SonarScanner end step failed or the Quality Gate did not pass."
        exit $LASTEXITCODE
    }
    Write-Host "  Quality Gate passed on SonarQube." -ForegroundColor Green
} else {
    Write-Host "`n[6/6] SonarScanner end step skipped." -ForegroundColor DarkGray
}

Write-Host "`n==========================================" -ForegroundColor Green
Write-Host "  Pipeline passed." -ForegroundColor Green
Write-Host "==========================================" -ForegroundColor Green
