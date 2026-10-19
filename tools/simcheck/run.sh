#!/usr/bin/env bash
# Sim tests without Unity (needs the .NET 8 SDK). Usage: tools/simcheck/run.sh [extra dotnet test args]
set -euo pipefail
cd "$(dirname "$0")"
dotnet test -c Release --nologo "$@"
