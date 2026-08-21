---
name: macclipboardmonitor
description: Use when working on the MacClipboardMonitor app (Avalonia + ReactiveUI clipboard manager for macOS). Covers its MVVM architecture, EF Core/SQLite persistence, global keyboard hook, tray icon, clipboard capture of text/images/files, and the strict rules to modify the UI or logic without breaking functionality.
---

# MacClipboardMonitor

Guía para entender, modificar y extender **MacClipboardMonitor** sin romper su
lógica ni su funcionalidad. Lee `AGENTS.md` (raíz del repo) junto con esta skill.

## Qué es

Gestor de historial de portapapeles (clipboard manager) para macOS. Escucha el
portapapeles (texto, imágenes y archivos), guarda el historial en SQLite y permite
re-copiar o borrar entradas desde una ventana flotante que se alterna con
`Ctrl+Cmd+V` o con el ícono de la barra de menú.

## Stack

- **UI:** Avalonia 11.3.6 (XAML) + `FluentTheme` + fuente Inter.
- **MVVM:** ReactiveUI (`ReactiveObject`, `ReactiveCommand`, `SourceList`, Rx).
- **Persistencia:** EF Core 9 + SQLite (`~/MacClipboardMonitor.db`), sin migraciones.
- **Teclado global:** SharpHook (`TaskPoolGlobalHook`).
- **Bandeja:** `TrayIcon` (NSStatusItem en macOS).
- **Target:** `net8.0`.

## Mapa de archivos (responsabilidades)

| Archivo | Responsabilidad |
|---|---|
| `Program.cs` | `Main` + `AppBuilder` (`.UseReactiveUI()`) |
| `App.axaml.cs` | DI manual + `TrayIcon` (menú de bandeja) |
| `ViewLocator.cs` | `FooViewModel` -> `FooView` por convención de nombres |
| `Views/MainWindow.axaml` | SOLO presentación (bindings + estilos) |
| `Views/MainWindow.axaml.cs` | SOLO drag de ventana, hide/show y hook global |
| `ViewModels/MainWindowViewModel.cs` | TODA la lógica de UI (historial, comandos, dedupe, timer) |
| `Models/ClipboardItem.cs` | Entidad EF + `ClipboardItemType` + props UI `[NotMapped]` |
| `Repositories/ClipboardRepository.cs` | Reglas de persistencia (dedupe por tipo, caducidad, límite 50) |
| `Services/PollingClipboardMonitorService.cs` | Polling 750ms; detecta archivo>imagen>texto y emite `ClipboardCapture` |
| `Services/ClipboardCapture.cs` | DTO (Tipo + Texto + ImageBytes + FilePaths + Fingerprint) |
| `Services/AutoStartManager.cs` | LaunchAgent plist para iniciar al login |
| `Data/AppDbContext.cs` | `EnsureCreated()` + `ALTER TABLE` idempotente para columnas nuevas |

## Reglas de retención

| Tipo | Deduplicación | Caducidad | Límite |
|---|---|---|---|
| Texto | `Content` ToLower | 48 h | 50 global |
| Imagen | `ImageHash` (SHA256) | 1 h | 50 global |
| Archivo | `FilePaths` exacto | 1 h | 50 global |

## Reglas críticas (no romper)

1. **Lógica solo en el ViewModel.** `MainWindow.axaml` no contiene lógica; todo se
   resuelve por bindings. `MainWindow.axaml.cs` solo maneja ventana/teclado.
2. **Preservar bindings y nombres al retocar UI:** `x:Name="RootWindow"`, `ItemsSource`,
   `SelectedItem`, `Command`, handlers `OnPointerPressed` y `OnHideClick`. No renombrar
   `RootWindow`: los comandos de dentro de los `DataTemplate` se enlazan vía
   `#RootWindow.((vm:MainWindowViewModel)DataContext).<Comando>`.
3. **Bindings compilados:** cada `DataTemplate` lleva `x:DataType`; mantenlo correcto.
4. **Colores:** usar `{DynamicResource SystemControl...Brush}` del tema Fluent para
   soportar claro/oscuro; no hardcodear fondos/textos.
5. **Comandos:** `ReactiveCommand.CreateFromTask` y `SourceList` + `.Bind(out _history)`.
6. **UI-only props:** marcar con `[NotMapped]` (ej. `IsImage`, `IsFile`, `AvaloniaImage`, `FileList`).
7. **Idioma:** comentarios y strings de UI en español.

## Flujo de datos

`PollingClipboardMonitorService` (Rx, 750ms) -> `ClipboardChanged`
(`ClipboardCapture`, detección archivo>imagen>texto) -> `MainWindowViewModel`
(dedupe en memoria por tipo -> `AddItemAsync` -> inserta en `SourceList`).
`Ctrl+Cmd+V` (SharpHook) o el `TrayIcon` alternan `Show`/`Hide`.

## Comandos

```bash
dotnet build MacClipboardMonitor.sln        # compilar
dotnet run --project MacClipboardMonitor    # ejecutar (con terminal)
./MacClipboardMonitor/run_app.sh            # ejecutar sin terminal (publica y abre .app)
./MacClipboardMonitor/compiler.sh           # empaquetar
./MacClipboardMonitor/CompilerJustMac.sh    # empaquetar solo macOS
./MacClipboardMonitor/build_installer.sh    # instalador
```

## Errores comunes a evitar

- No añadir ventanas nuevas: solo existe `MainWindow` (resuelta por `ViewLocator`).
- No introducir migraciones EF: la DB se crea con `EnsureCreated()` y columnas nuevas
  se agregan con `ALTER TABLE` idempotente en `AppDbContext.EnsureSchemaColumns`.
- No usar `BoxShadowTransition`: no existe en Avalonia 11.3.6 (usar `BrushTransition`).
- El separador del menú nativo es `NativeMenuItemSeparator` (no `NativeMenuSeparator`).
- Los `async void` de `SetText`/`SetImage`/`SetFiles` son intencionales (fire-and-forget).
- No quitar el `LSUIElement=true` del `.app`: mantiene la app en la barra de menú sin Dock.
