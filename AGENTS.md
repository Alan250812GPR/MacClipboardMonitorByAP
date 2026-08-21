# AGENTS.md — MacClipboardMonitor

Reglas de estructura y convenciones para que cualquier modelo o agente entienda,
modifique y extienda esta aplicación sin romper su lógica ni funcionalidad.

## Qué es la app

Gestor de historial de portapapeles (clipboard manager) para macOS, inspirado en
el gestor de copiar/pegar de Windows. Escucha el portapapeles (texto, imágenes y
archivos), guarda el historial en SQLite y permite re-copiar o borrar entradas desde
una ventana flotante que se alterna con `Ctrl+Cmd+V` o con el ícono de la barra de menú.

## Stack técnico

| Área        | Tecnología                                            |
|-------------|-------------------------------------------------------|
| UI          | Avalonia 11.3.6 (XAML) + FluentTheme + Inter font    |
| MVVM        | ReactiveUI (`ReactiveObject`, `ReactiveCommand`, Rx)  |
| Persistencia| EF Core 9 + SQLite (`MacClipboardMonitor.db` en `~/`) |
| Teclado global | SharpHook (hook global de teclado)                |
| Bandeja     | `TrayIcon` de Avalonia (NSStatusItem en macOS)        |
| Target      | `net8.0`                                              |

## Estructura del proyecto

```
MacClipboardMonitor/
├── Program.cs                          # Punto de entrada (Main) + AppBuilder
├── App.axaml / App.axaml.cs            # Tema (FluentTheme), ViewLocator, DI manual, TrayIcon
├── ViewLocator.cs                      # Resuelve ViewModel -> View por convención
├── Views/
│   └── MainWindow.axaml(.cs)           # Única ventana (UI + hook de teclado global)
├── ViewModels/
│   ├── ViewModelBase.cs                # Base = ReactiveObject
│   └── MainWindowViewModel.cs          # Toda la lógica de presentación/historial
├── Models/
│   └── ClipboardItem.cs                # Entidad EF Core + enum ClipboardItemType + props UI
├── Repositories/
│   ├── IClipboardRepository.cs         # Contrato de persistencia
│   └── ClipboardRepository.cs          # Implementación EF Core (dedupe + caducidad)
├── Services/
│   ├── IClipboardMonitorService.cs     # Contrato de monitor de portapapeles
│   ├── ClipboardCapture.cs             # DTO emitido por el monitor (tipo + payload)
│   ├── PollingClipboardMonitorService.cs # Polling 750ms con Rx
│   ├── IPasteService.cs                # Contrato de "pegar directo" (Cmd+V)
│   ├── MacPasteService.cs              # CGEvent Cmd+V + permiso de Accesibilidad
│   ├── CodeDetectionService.cs         # Detección heurística de lenguaje (JSON/SQL/...)
│   ├── AppConfigService.cs             # Config JSON en ~/ (hotkey configurable)
│   ├── EncryptionService.cs            # AES-256-CBC para entradas encriptadas
│   └── AutoStartManager.cs             # LaunchAgent para iniciar al login
├── Data/
│   └── AppDbContext.cs                 # DbContext + EnsureCreated + columnas (sin migraciones)
├── Assets/
│   ├── avalonia-logo.ico               # Ícono de la ventana
│   └── tray.png                        # Ícono de la barra de menú (template)
├── run_app.sh                          # Publica y abre el .app sin terminal
├── compiler.sh / CompilerJustMac.sh    # Publicar/empaquetar
└── build_installer.sh                  # Genera .app + .dmg
```

## Arquitectura y flujo de datos

```
Program.cs (Main)
  -> App.axaml.cs (OnFrameworkInitializationCompleted)
       crea: MainWindow, AppDbContext, ClipboardRepository,
             PollingClipboardMonitorService, MainWindowViewModel
       asigna DataContext + SetupTrayIcon + AutoStartManager.RegisterAutoStart()

PollingClipboardMonitorService (Rx, cada 750ms)
  -> IObservable<ClipboardCapture> ClipboardChanged
       detecta formato del portapapeles: Archivo > Imagen > Texto
  -> MainWindowViewModel se suscribe
       -> dedupe en memoria según tipo (texto: OrdinalIgnoreCase, imagen: hash, archivo: rutas)
       -> ClipboardRepository.AddItemAsync (SQLite)
       -> inserta en SourceList<ClipboardItem> (History)

MainWindow (SharpHook global)
  -> Ctrl+Cmd+V alterna Show/Hide de la ventana

TrayIcon (App.axaml.cs)
  -> Menú: Mostrar/Ocultar, Borrar historial, Salir
```

## Reglas de retención

| Tipo        | Deduplicación            | Caducidad | Límite |
|-------------|--------------------------|-----------|--------|
| Texto       | `Content` ToLower        | 48 horas  | 100 global |
| Imagen      | `ImageHash` (SHA256)     | 1 hora    | 100 global |
| Archivo     | `FilePaths` exacto       | 1 hora    | 100 global |
| Encriptado  | no aplica                | nunca     | 100 global (no se poda) |

Las entradas encriptadas (`IsEncrypted=true`) son texto marcado por el usuario que
**nunca caduca** ni es recortado por el límite; solo se borran manualmente. Se guardan
cifradas en `CipherText` y se descifran al vuelo al copiar (la UI solo muestra `••••••••`).

## Responsabilidades de cada archivo (clave)

- **MainWindowViewModel.cs**: ÚNICO lugar con lógica de negocio de UI. Contiene:
  - `History` (`ReadOnlyObservableCollection<ClipboardItem>`) ligada a un `SourceList`.
  - `SelectedItem` (setter dispara `OnItemSelected` -> re-copia según tipo).
  - `ClearHistoryCommand`, `DeleteItemCommand`, `EncryptItemCommand`, `OpenImagePreviewCommand`
    y comandos de Ajustes (hotkey) (ReactiveCommand).
  - Búsqueda (`SearchText`) con filtro dinámico (`.Filter()` + throttle 250ms).
  - `CopyItem` descifra entradas encriptadas y suprime su re-captura en texto plano.
  - Dedupe en memoria según tipo y limpieza periódica (timer 1 min) de caducados.
- **ClipboardRepository.cs**: reglas de persistencia: anti-duplicados según tipo,
  caducidad (texto 48h, imagen/archivo 1h), límite global 100. Las encriptadas
  no caducan ni se podan (`MarkEncryptedAsync`).
- **PollingClipboardMonitorService.cs**: detecta tipo de contenido (archivo > imagen
  > texto) vía formatos del portapapeles y emite `ClipboardCapture`.
- **MainWindow.axaml**: SOLO presentación. Toda la lógica vive en el ViewModel.
- **MainWindow.axaml.cs**: SOLO drag de ventana, hide/show, y hook global de teclado.
- **App.axaml.cs**: DI manual + creación del `TrayIcon` (menú de bandeja).

## Reglas y convenciones (RESPETAR SIEMPRE)

1. **No mezclar lógica con UI.** La UI (`.axaml`) solo declara bindings y estilos.
   No agregar lógica en code-behind más allá de interacción de ventana/teclado.
2. **MVVM estricto con ReactiveUI.** Los ViewModels derivan de `ViewModelBase`.
   Usar `RaiseAndSetIfChanged`, `ReactiveCommand.CreateFromTask`, y `SourceList`
   + `.Bind(out _history)` para colecciones observables.
3. **Bindings compilados.** `AvaloniaUseCompiledBindingsByDefault=true` y cada
   DataTemplate declara `x:DataType`. Mantener `x:DataType` correcto al editar XAML.
4. **Vista única resuelta por convención.** `ViewLocator` mapea
   `...ViewModels.FooViewModel` -> `...Views.FooView`. No hay ventanas adicionales.
5. **Binding a la ventana raíz.** Los comandos dentro de DataTemplates se enlazan al
   DataContext de la ventana vía `#RootWindow.((vm:MainWindowViewModel)DataContext).<Comando>`.
   `RootWindow` es el `x:Name` del `<Window>`. NO renombrar ni eliminar ese `x:Name`.
6. **Colores de tema.** Usar `{DynamicResource SystemControl...Brush}` (tema Fluent)
   para que la UI se adapte a modo claro/oscuro. No hardcodear colores de fondo/texto.
7. **Entidades con UI.** `ClipboardItem` mezcla entidad EF y props de vista; las
   propiedades de solo-UI van marcadas `[NotMapped]` (ej: `IsImage`, `AvaloniaImage`).
8. **Idioma del código:** comentarios y strings de UI en español.
9. **No tocar funcionalidad al retocar UI.** Al cambiar `.axaml` preservar siempre
   `x:Name`, `Command`, `ItemsSource`, `SelectedItem`, handlers de eventos
   (`OnPointerPressed`, `OnHideClick`) y los bindings existentes.

## Comandos de build y verificación

```bash
# Compilar
dotnet build MacClipboardMonitor.sln

# Ejecutar (desarrollo; muestra terminal)
dotnet run --project MacClipboardMonitor

# Ejecutar sin terminal (publica y abre el .app)
./MacClipboardMonitor/run_app.sh

# Publicar/empaquetar (scripts existentes)
./MacClipboardMonitor/compiler.sh
./MacClipboardMonitor/CompilerJustMac.sh
./MacClipboardMonitor/build_installer.sh
```

## Notas técnicas importantes

- **DB sin migraciones:** `AppDbContext` usa `Database.EnsureCreated()` y ruta fija
  `~/MacClipboardMonitor.db`. Al iniciar ejecuta `ALTER TABLE` idempotente (en
  try/catch) para agregar columnas nuevas si la DB ya existía. NO hay migraciones EF.
- **Autostart:** `AutoStartManager` crea un LaunchAgent plist (`launchctl`). Ya no
  usa `KeepAlive` para permitir cerrar la app manualmente.
- **Hook global:** `SharpHook` (`TaskPoolGlobalHook`) se inicializa en el constructor
  de `MainWindow` y se libera en `OnClosed`. La combinación por defecto es Ctrl+Cmd+V,
  pero es **configurable** desde Ajustes (⚙️) y se persiste en `~/MacClipboardMonitor.config.json`.
- **Pegar directo:** el doble clic en una tarjeta copia y simula Cmd+V (CGEvent) hacia
  la app activa; requiere permiso de **Accesibilidad** (`AXIsProcessTrusted`).
- **Encriptación:** `EncryptionService` usa AES-256-CBC con clave derivada (PBKDF2) de
  una contraseña fija y sal fija. Las columnas `IsEncrypted` y `CipherText` se agregan
  con `ALTER TABLE` idempotente.
- **Overlays internos:** la vista previa de imágenes y el panel de Ajustes son overlays
  dentro de la misma ventana (no son ventanas nuevas); se controlan con
  `IsImagePreviewOpen` / `IsSettingsOpen`.
- **TrayIcon:** se crea en `App.SetupTrayIcon` y se guarda en un campo para que no sea
  recolectado por el GC. El ícono es un PNG de plantilla (`MacOSProperties.IsTemplateIcon`).
- **Ventana sin marco:** `SystemDecorations=None`, `Topmost=True`, `CornerRadius=12`;
  el arrastre se logra con `BeginMoveDrag` en `OnPointerPressed`.
- **Terminal:** `OutputType=WinExe` evita la consola; la terminal solo aparece con
  `dotnet run`. Para abrir sin terminal usar `run_app.sh` (el `.app` lleva `LSUIElement=true`).
