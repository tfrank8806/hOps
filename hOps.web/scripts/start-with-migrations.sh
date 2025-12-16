#!/usr/bin/env bash
set -euo pipefail

cd /src/hOps.web

echo "Running database migrations..."
dotnet ef database update --configuration Release --no-build
echo "Migrations applied."

cd /app
exec dotnet hOps.web.dll
