#!/usr/bin/env bash
set -euo pipefail

dotnet publish TowEstimator.csproj -c Release -r win-x64 > /dev/null

publish_dir="bin/Release/net8.0-windows/win-x64/publish"
output_dir="dist/portable"

if [[ ! -d "$publish_dir" ]]; then
  echo "Publish output not found at $publish_dir" >&2
  exit 1
fi

mkdir -p "$output_dir/app-data"

exe_path="$(ls "$publish_dir"/*.exe | head -n 1)"
if [[ -z "$exe_path" ]]; then
  echo "No .exe found in $publish_dir" >&2
  exit 1
fi

cp -f "$exe_path" "$output_dir/"
echo "Portable package created at $output_dir"
