# MacClipboardMonitor — Gestor de historial de portapapeles para macOS

[![Version](https://img.shields.io/badge/version-1.0.6-blue)](MacClipboardMonitor/MacClipboardMonitor.csproj)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com)
[![Avalonia](https://img.shields.io/badge/Avalonia-11.3.6-8A2BE2)](https://avaloniaui.net)
[![Platform](https://img.shields.io/badge/platform-macOS%2013%2B-lightgrey)](https://www.apple.com/macos)
[![Licencia](https://img.shields.io/badge/licencia-MS--PL-green)](LICENCE)

[English](README.md) | **Español**

> La experiencia `Win + V` de Windows, nativa para macOS. Captura texto, imágenes y archivos automáticamente, organízalos cronológicamente y pégalos donde quieras con una sola combinación de teclas.

---

## Capturas de pantalla

<!-- TODO: Añade tus capturas en docs/screenshots/ — los placeholders se mostrarán cuando añadas las imágenes -->
| Ventana principal | Vista previa completa (clic derecho) | Diálogo de tag |
|---|---|---|
| ![Ventana principal](docs/screenshots/main.png) | ![Vista previa](docs/screenshots/preview.png) | ![Diálogo tag](docs/screenshots/tag-dialogue.png) |

*Coloca tus capturas en `docs/screenshots/`. Tamaño recomendado: 350×550 @2x.*

---

## Características

### Captura e historial
- **Captura automática por polling Rx (750 ms)** — detecta el formato del portapapeles con prioridad `Archivo > Imagen > Texto`.
- **Orden cronológico** — `CreatedAt DESC` + `Id DESC` como desempate; el elemento más reciente siempre arriba. Los duplicados no se descartan: se promocionan arriba (LIFO).
- **Deduplicación inteligente**
  - Texto: `Content` insensible a mayúsculas (`OrdinalIgnoreCase`)
  - Imagen: hash `SHA-256` (`ImageHash`)
  - Archivo: coincidencia exacta de `FilePaths`
- **Retención universal**
  | Tipo | Deduplicación | Caducidad | Límite global |
  |---|---|---|---|
  | Texto | `Content` ToLower | 48 horas | 100 |
  | Imagen | `ImageHash` | 1 hora | 100 |
  | Archivo | `FilePaths` | 1 hora | 100 |
  | Encriptado | n/a | **nunca** | exento de purga |
- **Purga automática** — cada 60 segundos en memoria y en SQLite; las entradas encriptadas nunca caducan.

### Búsqueda y organización
- **Búsqueda instantánea** — debounce de 250 ms, filtra texto y nombres de archivo; las entradas encriptadas se buscan por su tag/título, no por el contenido.
- **Detección de lenguaje** — identificación heurística de fragmentos de código (`JSON`, `SQL`, `XML`, etc.) con distintivo en la tarjeta.
- **Tag/título para entradas encriptadas** — asigna un título legible (`EncryptedTag`) para identificar secretos sin revelarlos.

### Vistas previas (solo lectura)
- **Vista previa completa con clic derecho** — overlay con el contenido íntegro (sin truncado de `60 px`). Soporta texto, código, imágenes (zoom `Ctrl + rueda` 0.5×–4×) y listas de archivos. **Las entradas encriptadas están exentas** — el clic derecho no hace nada.
- **Vista previa de imágenes** — overlay dedicado con `LayoutTransformControl` y zoom. Se mantiene como overlay separado para un control más fino.

### Acciones
- **Clic simple** — copia al portapapeles (las encriptadas se descifran al vuelo; se suprime la re-captura del secreto en texto plano mediante huella SHA-256).
- **Doble clic** — copia y pega directamente en la app activa (`CGEvent Cmd+V`). Requiere permiso de Accesibilidad.
- **Borrar individual** y **Borrar historial**.

### Integración con el sistema
- **Atajo global configurable** — por defecto `Ctrl + Cmd + V` vía `SharpHook` (`TaskPoolGlobalHook`). Reconfigurable desde Ajustes (⚙️) a cualquier `Ctrl`/`Cmd` + letra o `F1–F12`. Persistido en `~/MacClipboardMonitor.config.json`.
- **`TrayIcon` en la barra de menú** — `NSStatusItem` con ícono plantilla (`tray.png`), menú: Mostrar/Ocultar, Borrar historial, Salir. Clic en el ícono alterna la ventana.
- **Inicio al iniciar sesión** — `LaunchAgent` plist vía `AutoStartManager` (sin `KeepAlive`, así la app se puede cerrar manualmente).
- **Ventana flotante sin marco** — `SystemDecorations=None`, `Topmost=True`, `CornerRadius=12`, arrastre con `BeginMoveDrag`, `LSUIElement=true` (sin ícono en el Dock).

---

## Requisitos

- macOS 13 o superior
- Permiso de Accesibilidad (Ajustes del Sistema → Privacidad y seguridad → Accesibilidad) — solo para el pegado directo
- .NET 8 SDK — solo si quieres compilar desde el código fuente

---

## Instalación

### Opción A — Para usuarios (recomendado)

```bash
# Genera el .app y el .dmg (scripts incluidos)
./MacClipboardMonitor/build_installer.sh
# o
./MacClipboardMonitor/compiler.sh
./MacClipboardMonitor/CompilerJustMac.sh
```
El `.app` se publica como `WinExe` (`LSUIElement=true`) — no aparece ventana de Terminal.

### Opción B — Desde el código fuente

```bash
# Compilar
dotnet build MacClipboardMonitor.sln -c Release

# Ejecutar con Terminal (desarrollo)
dotnet run --project MacClipboardMonitor

# Ejecutar sin Terminal (publica y abre el .app)
./MacClipboardMonitor/run_app.sh
```

---

## Uso

1. Copia cualquier cosa — texto, una imagen o archivos — aparecerá arriba del historial.
2. Pulsa `Ctrl + Cmd + V` (o tu atajo personalizado) o haz clic en el ícono de la barra de menú para mostrar/ocultar la ventana.
3. **Clic** en una tarjeta para copiar · **Doble clic** para pegar directamente · **Clic derecho** para vista previa completa de solo lectura.
4. Pulsa **🔒** en una tarjeta de texto para encriptarla — se te pedirá un tag/título. Las tarjetas encriptadas muestran el tag (o `••••••••` si no tiene) y el distintivo `🔒 Encriptado`.
5. Pulsa **✎** en una tarjeta encriptada para editar su tag.
6. Usa el campo de búsqueda para filtrar. Pulsa `⚙️` para cambiar el atajo, `🗑️` para borrar todo, `✕` para ocultar.

**Atajos de teclado**

| Tecla | Acción |
|---|---|
| `↑` / `↓` | Navegar historial |
| `Enter` | Copiar elemento seleccionado |
| `Esc` | Cerrar diálogo de tag → vista previa completa → vista previa de imagen → ajustes → ocultar ventana |
| `Ctrl + rueda` | Zoom en vistas previas de imágenes |

---

## Configuración y datos

| Archivo | Ubicación | Propósito |
|---|---|---|
| Base de datos | `~/MacClipboardMonitor.db` | SQLite vía EF Core 9 (`EnsureCreated` + `ALTER TABLE` idempotente — sin migraciones) |
| Configuración | `~/MacClipboardMonitor.config.json` | `HotkeyModifiers`, `HotkeyKey`, `LatestVersion` |

Para reiniciar: cierra la app y borra cualquiera de los dos archivos — se recrearán al iniciar.

---

## Arquitectura

```
Program.cs (Main + guard de instancia única)
 → App.axaml.cs (OnFrameworkInitializationCompleted)
     crea: MainWindow, AppDbContext, ClipboardRepository,
           PollingClipboardMonitorService, MainWindowViewModel
     conecta: DataContext + TrayIcon + AutoStartManager + HotkeyChanged

PollingClipboardMonitorService (Rx, 750 ms)
 → IObservable<ClipboardCapture> ClipboardChanged
     detecta: Archivo > Imagen > Texto
 → MainWindowViewModel suscrito
     → dedupe en memoria → Repository.AddItemAsync → SourceList<ClipboardItem> (History)

MainWindow (SharpHook global hook)
 → Ctrl+Cmd+V alterna visibilidad de la ventana

TrayIcon (App.axaml.cs)
 → Menú: Mostrar/Ocultar, Borrar historial, Salir
```

> Reglas completas para desarrolladores y responsabilidades por archivo: ver [AGENTS.md](AGENTS.md).

---

## Stack técnico

| Área | Tecnología |
|---|---|
| UI | Avalonia 11.3.6 (XAML) + FluentTheme + fuente Inter |
| MVVM | ReactiveUI (`ReactiveObject`, `ReactiveCommand`, Rx) + DynamicData |
| Persistencia | EF Core 9 + SQLite |
| Atajo global | SharpHook 7.1.1 (`TaskPoolGlobalHook`) |
| Bandeja | Avalonia `TrayIcon` (`NSStatusItem`) |
| Target | `net8.0` (`WinExe`) |

---

## Estructura del proyecto

<details>
<summary>Clic para expandir</summary>

```
MacClipboardMonitor/
├── Program.cs
├── App.axaml / App.axaml.cs        # FluentTheme, ViewLocator, DI, TrayIcon
├── ViewLocator.cs
├── Views/MainWindow.axaml(.cs)     # Única ventana + hook global + arrastre
├── ViewModels/
│   ├── ViewModelBase.cs
│   └── MainWindowViewModel.cs      # Toda la lógica de presentación (historial, búsqueda, previews, encriptación, hotkey)
├── Models/ClipboardItem.cs         # Entidad EF + enum + props UI [NotMapped] + EncryptedTag
├── Repositories/
│   ├── IClipboardRepository.cs
│   └── ClipboardRepository.cs      # Dedupe + caducidad + límite 100 + manejo de encriptadas
├── Services/
│   ├── PollingClipboardMonitorService.cs
│   ├── MacPasteService.cs          # CGEvent Cmd+V
│   ├── CodeDetectionService.cs
│   ├── AppConfigService.cs
│   ├── EncryptionService.cs        # AES-256-CBC, PBKDF2
│   └── AutoStartManager.cs
├── Data/AppDbContext.cs
├── Assets/tray.png, avalonia-logo.ico
├── run_app.sh / compiler.sh / CompilerJustMac.sh / build_installer.sh
└── MacClipboardMonitor.csproj      # Versión 1.0.6
```

</details>

---

## Permisos

**Accesibilidad** solo es necesario para el doble clic de pegado directo. Si aparece el aviso:

`Ajustes del Sistema → Privacidad y seguridad → Accesibilidad → activa MacClipboardMonitor`

Sin él, la copia con clic simple funciona igualmente.

---

## Changelog

### 1.0.6
- Overlay de vista previa completa con clic derecho (solo lectura, encriptadas exentas)
- Entradas encriptadas con tag/título personalizado + editor `✎` en la tarjeta + búsqueda por tag
- Orden estable `CreatedAt DESC, Id DESC`
- Previews separadas (imagen vs completa)
- Atajo global configurable persistido en `~/MacClipboardMonitor.config.json`

Ver `MacClipboardMonitor.csproj` (`Version`/`AssemblyVersion`) para el historial de versiones.

---

## Contribuir

¡Las contribuciones son bienvenidas! Por favor:

1. Lee [AGENTS.md](AGENTS.md) — MVVM estricto, bindings compilados (`x:DataType`), convención de ventana única, binding `#RootWindow`, colores `DynamicResource` de Fluent.
2. Mantén la lógica en `MainWindowViewModel.cs`; `.axaml` es solo presentación.
3. Ejecuta `dotnet build MacClipboardMonitor.sln -c Release` antes de enviar tu PR.

Reporta incidencias indicando versión de macOS, pasos para reproducir y si Accesibilidad está activado.

---

## Licencia

Licenciado bajo [Microsoft Public License (MS-PL)](LICENCE) — ver `LICENCE` para los detalles. Conserva todos los avisos de copyright y atribución al distribuir.

---

## Donaciones — Apoya el proyecto

MacClipboardMonitor es gratuito, de código abierto y sin telemetría. Si te ahorra tiempo, considera apoyar su desarrollo.

<!-- TODO: Reemplaza # con tus URLs reales -->

- **PayPal:** [PayPal](#) <!-- ej. https://paypal.me/tuNombre -->
- **Buy Me a Coffee:** [Buy Me a Coffee](#) <!-- ej. https://buymeacoffee.com/tuNombre -->

Cada donación — por pequeña que sea — se agradece sinceramente y ayuda a mantener el proyecto.

Contacto: [alan.2500gpr@gmail.com](mailto:alan.2500gpr@gmail.com)

---

## Agradecimientos

Construido con [Avalonia UI](https://avaloniaui.net), [ReactiveUI](https://reactiveui.net), [DynamicData](https://github.com/RolandPheasant/DynamicData), [Entity Framework Core](https://learn.microsoft.com/es-es/ef/core/) y [SharpHook](https://github.com/TolikPylypchuk/SharpHook). Gracias a todos los colaboradores y testers.

---

*Hecho para macOS, inspirado en Windows. Creado con dedicación.*
