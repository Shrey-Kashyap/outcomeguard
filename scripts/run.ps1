param([string]$Dotnet = 'dotnet')
$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
Push-Location frontend
try {
    & npm.cmd ci
    if ($LASTEXITCODE -ne 0) { throw 'Frontend dependency installation failed.' }
    & npm.cmd run build
    if ($LASTEXITCODE -ne 0) { throw 'Frontend build failed.' }
} finally { Pop-Location }
New-Item -ItemType Directory -Force src/OutcomeGuard.Api/wwwroot | Out-Null
Copy-Item -Path frontend/dist/* -Destination src/OutcomeGuard.Api/wwwroot -Recurse -Force
$env:ASPNETCORE_ENVIRONMENT = 'Development'
& $Dotnet run --project src/OutcomeGuard.Api --no-launch-profile --urls http://127.0.0.1:5080
exit $LASTEXITCODE

