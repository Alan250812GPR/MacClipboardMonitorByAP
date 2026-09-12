#!/bin/bash
set -e
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
bash "$SCRIPT_DIR/increment_version.sh"
dotnet publish -c Release -r osx-arm64 --self-contained true -p:PublishSingleFile=true