#!/bin/bash
set -e
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
bash "$SCRIPT_DIR/increment_version.sh"
dotnet publish -r win-x64 -c Release --self-contained && dotnet publish -r linux-x64 -c Release --self-contained && dotnet publish -r osx-x64 -c Release --self-contained