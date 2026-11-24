param([string]$Dotnet = 'dotnet')
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:OutcomeGuard__DemoEnabled = 'true'
& $Dotnet ./OutcomeGuard.Api.dll --urls http://127.0.0.1:5080
exit $LASTEXITCODE
