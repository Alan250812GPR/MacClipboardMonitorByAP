#!/bin/bash

APP_NAME="MacClipboardMonitor"
PUBLISH_DIR="./bin/Release/net8.0/osx-arm64/publish"
APP_BUNDLE_DIR="./InstallerBuild/${APP_NAME}.app"

echo "🧹 Limpiando compilaciones anteriores..."
rm -rf ./InstallerBuild
mkdir -p "$APP_BUNDLE_DIR/Contents/MacOS"
mkdir -p "$APP_BUNDLE_DIR/Contents/Resources"

echo "🔨 Compilando el proyecto en modo Release para ARM64..."
dotnet publish -c Release -r osx-arm64 --self-contained true -p:PublishSingleFile=true

echo "📦 Copiando binarios al paquete .app..."
cp "$PUBLISH_DIR/$APP_NAME" "$APP_BUNDLE_DIR/Contents/MacOS/"
chmod +x "$APP_BUNDLE_DIR/Contents/MacOS/$APP_NAME"

# Si tienes un ícono, descomenta la siguiente línea y asegúrate de tener icon.icns en la raíz
# cp ./icon.icns "$APP_BUNDLE_DIR/Contents/Resources/"

echo "📝 Generando Info.plist..."
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
    <string>icon.icns</string>
    <key>CFBundleShortVersionString</key>
    <string>1.0</string>
    <key>LSUIElement</key>
    <true/> </dict>
</plist>
EOF

echo "💿 Creando imagen de disco (.dmg)..."
hdiutil create -volname "$APP_NAME" -srcfolder "./InstallerBuild" -ov -format UDZO "${APP_NAME}_Installer.dmg"

echo "✅ ¡Listo! El instalador se llama ${APP_NAME}_Installer.dmg"