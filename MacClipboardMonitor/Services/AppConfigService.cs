using System;
using System.IO;
using System.Text.Json;

namespace MacClipboardMonitor.Services;

// Configuración persistente de la app (JSON en la carpeta del usuario).
public class AppConfigService
{
    private static readonly string ConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "MacClipboardMonitor.config.json");

    // Atajo global: modificadores separados por "|" y tecla individual.
    // Modificadores válidos: Control, Meta (Cmd), Shift, Alt. Tecla: A-Z o F1-F12.
    public string HotkeyModifiers { get; set; } = "Control|Meta";
    public string HotkeyKey { get; set; } = "V";

    public static AppConfigService Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var loaded = JsonSerializer.Deserialize<AppConfigService>(File.ReadAllText(ConfigPath));
                if (loaded != null) return loaded;
            }
        }
        catch
        {
            // Config corrupta o ilegible: usar valores por defecto.
        }

        return new AppConfigService();
    }

    public void Save()
    {
        try
        {
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // Sin permisos de escritura: la configuración no se persiste en esta sesión.
        }
    }
}
