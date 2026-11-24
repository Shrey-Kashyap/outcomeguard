#!/usr/bin/env sh
set -eu
cd "$(dirname "$0")/.."
(cd frontend && npm ci && npm run build)
mkdir -p src/OutcomeGuard.Api/wwwroot
cp -R frontend/dist/. src/OutcomeGuard.Api/wwwroot/
ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/OutcomeGuard.Api --no-launch-profile --urls http://127.0.0.1:5080

