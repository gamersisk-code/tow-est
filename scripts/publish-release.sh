#!/usr/bin/env bash
set -euo pipefail

dotnet publish TowEstimator.csproj -c Release -r win-x64
