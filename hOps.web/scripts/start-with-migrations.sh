#!/usr/bin/env bash
set -euo pipefail

cd /src/hOps.web

# Ensure design-time assets exist where dotnet-ef expects them
if [ ! -d "bin/Debug/net8.0" ] && [ -d "bin/Release/net8.0" ]; then
  mkdir -p bin/Debug/net8.0
  cp -r bin/Release/net8.0/* bin/Debug/net8.0/
fi

echo "Running database migrations..."
dotnet ef database update --configuration Release --no-build
echo "Migrations applied."

cd /app
exec dotnet hOps.web.dll
