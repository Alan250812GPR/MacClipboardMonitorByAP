#!/bin/bash
# Incrementa patch M.M.P en MacClipboardMonitor.csproj (1.0.0 -> 1.0.1)
set -e
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CSPROJ="$SCRIPT_DIR/MacClipboardMonitor.csproj"

if [ ! -f "$CSPROJ" ]; then
  echo "No se encontró $CSPROJ"
  exit 1
fi

# Extraer versión actual (compatible macOS BSD grep/sed)
CURRENT=$(sed -n 's/.*<Version>\(.*\)<\/Version>.*/\1/p' "$CSPROJ" | head -n1 | tr -d '[:space:]')
if [ -z "$CURRENT" ]; then
  echo "No se encontró <Version> en $CSPROJ, inicializando 1.0.0"
  CURRENT="1.0.0"
fi

# Validar M.M.M
if ! echo "$CURRENT" | grep -Eq '^[0-9]+\.[0-9]+\.[0-9]+$'; then
  echo "Versión inválida: $CURRENT"
  exit 1
fi

MAJOR=$(echo "$CURRENT" | cut -d. -f1)
MINOR=$(echo "$CURRENT" | cut -d. -f2)
PATCH=$(echo "$CURRENT" | cut -d. -f3)
NEW_PATCH=$((PATCH + 1))
NEW_VERSION="$MAJOR.$MINOR.$NEW_PATCH"
NEW_ASSEMBLY="$MAJOR.$MINOR.$NEW_PATCH.0"

echo "Version: $CURRENT -> $NEW_VERSION"

# Reemplazar en csproj (solo primera ocurrencia de cada tag)
# Usar sed compatible macOS (BSD)
sed -i '' "s|<Version>.*</Version>|<Version>$NEW_VERSION</Version>|" "$CSPROJ"
sed -i '' "s|<AssemblyVersion>.*</AssemblyVersion>|<AssemblyVersion>$NEW_ASSEMBLY</AssemblyVersion>|" "$CSPROJ"
sed -i '' "s|<FileVersion>.*</FileVersion>|<FileVersion>$NEW_ASSEMBLY</FileVersion>|" "$CSPROJ"
sed -i '' "s|<InformationalVersion>.*</InformationalVersion>|<InformationalVersion>$NEW_VERSION</InformationalVersion>|" "$CSPROJ"

echo "Actualizado $CSPROJ a $NEW_VERSION"

# Exportar para uso en scripts llamantes
echo "$NEW_VERSION"
