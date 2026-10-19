using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace MacClipboardMcp;

// Herramienta MCP que lleva el último código/texto generado por el chat
// al portapapeles de macOS, para que MacClipboardMonitor lo capture automáticamente.
[McpServerToolType]
public static class CopyTool
{
    private static readonly string ConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "MacClipboardMonitor.config.json");

    [McpServerTool, Description(
        "Copia el último código o texto generado al portapapeles de macOS. " +
        "Úsalo siempre al terminar de generar código o texto para que quede " +
        "listo para pegar sin copiar/pegar manualmente.")]
    public static string CopyToClipboard(string content)
    {
        if (!IsMcpEnabled())
        {
            return "MCP desactivado: actívalo en Ajustes de MacClipboardMonitor.";
        }

        if (string.IsNullOrEmpty(content))
        {
            return "Nada que copiar: el contenido está vacío.";
        }

        var error = WriteToClipboard(content);
        return error is null
            ? $"Copiado al portapapeles ({content.Length} caracteres)."
            : "Error al escribir en el portapapeles: " + error;
    }

    // Lee el flag McpEnabled del config.json compartido con la app (default: activo).
    private static bool IsMcpEnabled()
    {
        try
        {
            if (!File.Exists(ConfigPath)) return true;

            var cfg = JsonSerializer.Deserialize<McpConfig>(File.ReadAllText(ConfigPath));
            return cfg?.McpEnabled ?? true;
        }
        catch
        {
            return true;
        }
    }

    // Escribe en el portapapeles general de macOS mediante pbcopy.
    private static string? WriteToClipboard(string text)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "/usr/bin/pbcopy",
                    RedirectStandardInput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            process.StandardInput.Write(text);
            process.StandardInput.Close();
            process.WaitForExit();

            return process.ExitCode == 0 ? null : $"pbcopy salió con código {process.ExitCode}";
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    private sealed class McpConfig
    {
        public bool McpEnabled { get; set; } = true;
    }
}
