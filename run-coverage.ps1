param(
    [bool]$OpenReport = $true,
    [int]$MinimumCoverage = 25
)

Write-Host "Code Coverage Runner" -ForegroundColor Cyan
Write-Host ""

# Check for ReportGenerator
$reportGenInstalled = dotnet tool list -g | Select-String "dotnet-reportgenerator-globaltool"
if (-not $reportGenInstalled) {
    Write-Host "Installing ReportGenerator..." -ForegroundColor Yellow
    dotnet tool install --global dotnet-reportgenerator-globaltool
}

# Clean previous reports
if (Test-Path "./coverage") { Remove-Item -Recurse -Force "./coverage" }
if (Test-Path "./coverage-report") { Remove-Item -Recurse -Force "./coverage-report" }

Write-Host "Building test project..." -ForegroundColor Yellow
dotnet build ReceptyOks_UnitTests/ReceptyOks_UnitTests.csproj --configuration Release

Write-Host "Running tests with coverage..." -ForegroundColor Yellow
dotnet test ReceptyOks_UnitTests/ReceptyOks_UnitTests.csproj --no-build --configuration Release --collect:"XPlat Code Coverage" --results-directory ./coverage --settings .github/coverlet.runsettings

Write-Host "Generating coverage report..." -ForegroundColor Yellow
$coverageFile = Get-ChildItem -Path "./coverage" -Filter "coverage.cobertura.xml" -Recurse | Select-Object -First 1

if ($coverageFile) {
    reportgenerator -reports:"$($coverageFile.FullName)" -targetdir:./coverage-report -reporttypes:"Html;MarkdownSummaryGithub;Badges;Cobertura"
    
    [xml]$coverageXml = Get-Content "$($coverageFile.FullName)"
    $lineRate = [double]$coverageXml.coverage.'line-rate'
    $linePercent = [math]::Round($lineRate * 100, 2)
    
    Write-Host ""
    Write-Host "Line Coverage: $linePercent%" -ForegroundColor Green
    Write-Host "Report: ./coverage-report/index.html" -ForegroundColor Cyan
    
    if ($OpenReport) {
        $reportPath = Resolve-Path "./coverage-report/index.html"
        Start-Process $reportPath
    }
} else {
    Write-Error "Coverage file not found!"
}
