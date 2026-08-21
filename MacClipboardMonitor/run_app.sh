#!/bin/bash
# Ejecuta MacClipboardMonitor sin terminal: publica y abre el .app.
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

APP_NAME="MacClipboardMonitor"
PUBLISH_DIR="./bin/Release/net8.0/osx-arm64/publish"
APP_BUNDLE_DIR="./InstallerBuild/${APP_NAME}.app"

echo "🔨 Publicando para macOS (arm64)..."
dotnet publish -c Release -r osx-arm64 --self-contained true -p:PublishSingleFile=true

echo "📦 Creando .app..."
rm -rf "$APP_BUNDLE_DIR"
mkdir -p "$APP_BUNDLE_DIR/Contents/MacOS"
mkdir -p "$APP_BUNDLE_DIR/Contents/Resources"

cp "$PUBLISH_DIR/$APP_NAME" "$APP_BUNDLE_DIR/Contents/MacOS/"
chmod +x "$APP_BUNDLE_DIR/Contents/MacOS/$APP_NAME"

cat > "$APP_BUNDLE_DIR/Contents/Info.plist" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleExecutable</key>
    <string>$APP_NAME</string>
    <key>CFBundleIdentifier</key>
    <string>com.smartraccoon.macclipboardmonitor</string>
    <key>CFBundleName</key>
    <string>$APP_NAME</string>
    <key>CFBundleShortVersionString</key>
    <string>1.0</string>
    <key>LSUIElement</key>
    <true/>
</dict>
</plist>
EOF

echo "🚀 Abriendo la app (sin terminal)..."
open "$APP_BUNDLE_DIR"
