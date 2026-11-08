#!/usr/bin/env bash
# Builds the engine-independent assemblies and runs the EditMode test sources outside Unity.
set -euo pipefail
cd "$(dirname "$0")"
dotnet build Realmkeeper.Verification.sln -c Release -nologo
dotnet test Realmkeeper.Verification.sln -c Release --no-build -nologo \
  --logger "trx;LogFileName=editmode-results.trx" --results-directory TestResults "$@"
