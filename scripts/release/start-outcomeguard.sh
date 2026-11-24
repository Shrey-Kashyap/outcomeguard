#!/usr/bin/env sh
set -eu
cd "$(dirname "$0")"
ASPNETCORE_ENVIRONMENT=Development OutcomeGuard__DemoEnabled=true \
  dotnet ./OutcomeGuard.Api.dll --urls http://127.0.0.1:5080
