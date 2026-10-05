#!/bin/bash
set -e

APP_NAME="MacClipboardMonitor"
PUBLISH_DIR="./bin/Release/net8.0/osx-arm64/publish"
APP_BUNDLE_DIR="./InstallerBuild/${APP_NAME}.app"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ASSETS_DIR="$SCRIPT_DIR/Assets"

echo "🔢 Incrementando versión patch (M.M.P)..."
bash "$SCRIPT_DIR/increment_version.sh"
APP_VERSION=$(sed -n 's/.*<Version>\(.*\)<\/Version>.*/\1/p' "$SCRIPT_DIR/MacClipboardMonitor.csproj" | head -n1 | tr -d '[:space:]')
echo "📌 Versión actual: $APP_VERSION"

echo "🧹 Limpiando residuos de versiones anteriores..."
rm -f /tmp/MacClipboardMonitor.lock
rm -f "$SCRIPT_DIR/MacClipboardMonitor_v*.dmg"
rm -rf "$APP_BUNDLE_DIR"
mkdir -p "$APP_BUNDLE_DIR/Contents/MacOS"
mkdir -p "$APP_BUNDLE_DIR/Contents/Resources"

echo "🔨 Compilando el proyecto en modo Release (arm64)..."
dotnet publish -c Release -r osx-arm64 --self-contained true -p:PublishSingleFile=true

echo "📦 Copiando binarios al paquete .app..."
cp "$PUBLISH_DIR/$APP_NAME" "$APP_BUNDLE_DIR/Contents/MacOS/"
chmod +x "$APP_BUNDLE_DIR/Contents/MacOS/$APP_NAME"

echo "🎨 Generando icono .icns desde Assets/tray.png..."
ICONSET="$APP_BUNDLE_DIR/Contents/Resources/icon.iconset"
mkdir -p "$ICONSET"
if command -v sips >/dev/null 2>&1; then
  for size in 16 32 128 256 512 1024; do
    cp "$ASSETS_DIR/tray.png" "$ICONSET/icon_${size}x${size}.png"
    sips -z "$size" "$size" "$ICONSET/icon_${size}x${size}.png" >/dev/null 2>&1 || true
  done
elif command -v magick >/dev/null 2>&1; then
  for size in 16 32 128 256 512 1024; do
    magick "$ASSETS_DIR/tray.png" -resize "$size"x"$size" "$ICONSET/icon_${size}x${size}.png"
  done
else
  echo "⚠️  No se encontró 'sips' ni 'magick': el icono se omitirá."
fi
if [ -f "$ICONSET/icon_1024x1024.png" ]; then
  iconutil -c icns "$ICONSET" -o "$APP_BUNDLE_DIR/Contents/Resources/$APP_NAME.icns"
  echo "✅ Icono .icns generado."
fi

echo "📝 Generando Info.plist (versión $APP_VERSION)..."
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
    <key>CFBundleIconFile</key>
    <string>$APP_NAME.icns</string>
    <key>CFBundleShortVersionString</key>
    <string>$APP_VERSION</string>
    <key>CFBundleVersion</key>
    <string>$APP_VERSION</string>
    <key>LSMinimumSystemVersion</key>
    <string>13.0</string>
    <key>LSUIElement</key>
    <true/>
    <key>NSAccessibilityUsageDescription</key>
    <string>Necesario para capturar el atajo global Ctrl+Cmd+V y simular el pegado directo en otras apps.</string>
</dict>
</plist>
EOF

if [ -n "$DEVELOPER_ID_APPLICATION" ]; then
  echo "🔐 Firmar con Developer ID: $DEVELOPER_ID_APPLICATION"
  codesign --deep --force --sign "$DEVELOPER_ID_APPLICATION" "$APP_BUNDLE_DIR/Contents/MacOS/$APP_NAME"
  codesign --deep --force --sign "$DEVELOPER_ID_APPLICATION" "$APP_BUNDLE_DIR"
fi

if [ -n "$NOTARY_API_KEY_ID" ] && [ -n "$NOTARY_API_KEY" ]; then
  echo "☁️  Notarizando..."
  xcrun notarytool submit "$APP_BUNDLE_DIR" \
    --keychain-profile "notary" \
    --wait \
    --apple-id "${NOTARY_EMAIL:-}" \
    --team-id "${NOTARY_TEAM_ID:-}" \
    >/dev/null 2>&1 || true
  xcrun stapler staple "$APP_BUNDLE_DIR" >/dev/null 2>&1 || true
fi

echo "💿 Creando imagen de disco (.dmg)..."
DMG_NAME="${APP_NAME}_v${APP_VERSION}.dmg"
hdiutil create -volname "${APP_NAME} v${APP_VERSION} by AlanParra" \
  -srcfolder "./InstallerBuild" -ov -format UDZO "$DMG_NAME"

if [ -n "$DEVELOPER_ID_APPLICATION" ]; then
  echo "🔍 Validando firma (Gatekeeper)..."
  xcrun spctl --assess --type run "$APP_BUNDLE_DIR/Contents/MacOS/$APP_NAME" && echo "✅ La app pasa Gatekeeper." || echo "⚠️  La app mostró advertencias de seguridad."
fi

echo "✅ ¡Listo! El instalador se llama $DMG_NAME"
ls -lh "$DMG_NAME"
