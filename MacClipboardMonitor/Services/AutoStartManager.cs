using System;
using System.Diagnostics;
using System.IO;

namespace MacClipboardMonitor.Services;

public static class AutoStartManager
{
    public static void RegisterAutoStart()
    {
        try
        {
            // 1. Obtener la ruta real del ejecutable, sin importar dónde instaló el usuario la app
            string? executablePath = Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(executablePath)) return;

            string plistName = "com.smartraccoon.macclipboardmonitor.plist";
            string launchAgentsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "LaunchAgents");
            string plistPath = Path.Combine(launchAgentsDir, plistName);

            if (!Directory.Exists(launchAgentsDir))
            {
                Directory.CreateDirectory(launchAgentsDir);
            }

            // 2. Construir el XML de configuración. 
            // NOTA: Se eliminó <key>KeepAlive</key> para que el usuario pueda cerrar la app manualmente.
            string plistContent = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<!DOCTYPE plist PUBLIC ""-//Apple//DTD PLIST 1.0//EN"" ""http://www.apple.com/DTDs/PropertyList-1.0.dtd"">
<plist version=""1.0"">
<dict>
    <key>Label</key>
    <string>com.smartraccoon.macclipboardmonitor</string>
    <key>ProgramArguments</key>
    <array>
        <string>{executablePath}</string>
    </array>
    <key>RunAtLoad</key>
    <true/>
</dict>
</plist>";

            // 3. Si el archivo no existe o la ruta cambió, lo actualizamos
            if (!File.Exists(plistPath) || !File.ReadAllText(plistPath).Contains(executablePath) || File.ReadAllText(plistPath).Contains("KeepAlive"))
            {
                // Si existía una versión anterior agresiva, intentamos descargarla primero
                if (File.Exists(plistPath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "launchctl",
                        Arguments = $"unload -w {plistPath}",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    })?.WaitForExit(2000); // Esperar un par de segundos máximo a que se descargue
                }

                File.WriteAllText(plistPath, plistContent);
                
                // 4. Le decimos a macOS que cargue el demonio silenciosamente
                Process.Start(new ProcessStartInfo
                {
                    FileName = "launchctl",
                    Arguments = $"load -w {plistPath}",
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
            }
        }
        catch (Exception)
        {
            // En producción, aquí podrías registrar el error en un log local
        }
    }
}