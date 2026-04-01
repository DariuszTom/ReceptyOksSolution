param(
    [bool]$OpenReport = $true
)

Write-Host "🧪 Running Code Coverage..." -ForegroundColor Cyan

# Clean
if (Test-Path "./coverage") { Remove-Item -Recurse -Force "./coverage" }
if (Test-Path "./coverage-report") { Remove-Item -Recurse -Force "./coverage-report" }

# Build
Write-Host "🔨 Building..." -ForegroundColor Yellow
dotnet build ReceptyOks_UnitTests/ReceptyOks_UnitTests.csproj -c Release

# Test with coverage
Write-Host "🧪 Running tests..." -ForegroundColor Yellow
dotnet test ReceptyOks_UnitTests/ReceptyOks_UnitTests.csproj --no-build -c Release --collect:"XPlat Code Coverage" --results-directory ./coverage

# Check for ReportGenerator
$hasReportGen = dotnet tool list -g | Select-String "dotnet-reportgenerator-globaltool"
if (-not $hasReportGen) {
    Write-Host "📦 Installing ReportGenerator..." -ForegroundColor Yellow
    dotnet tool install -g dotnet-reportgenerator-globaltool
}

# Generate report
Write-Host "📊 Generating report..." -ForegroundColor Yellow
reportgenerator -reports:./coverage/**/coverage.cobertura.xml -targetdir:./coverage-report -reporttypes:"Html;Cobertura"

# Show results
$coverageFile = Get-ChildItem -Path "./coverage-report" -Filter "Cobertura.xml" | Select-Object -First 1
if ($coverageFile) {
    [xml]$coverage = Get-Content "$($coverageFile.FullName)"
    $lineRate = [double]$coverage.coverage.'line-rate'
    $linePercent = [math]::Round($lineRate * 100, 2)
    
    Write-Host ""
    Write-Host "📊 Line Coverage: $linePercent%" -ForegroundColor $(if ($linePercent -ge 25) { "Green" } else { "Yellow" })
    Write-Host "📁 Report: ./coverage-report/index.html" -ForegroundColor Cyan
    
    if ($OpenReport) {
        Start-Process (Resolve-Path "./coverage-report/index.html")
    }
} else {
    Write-Error "Coverage file not found!"
}
