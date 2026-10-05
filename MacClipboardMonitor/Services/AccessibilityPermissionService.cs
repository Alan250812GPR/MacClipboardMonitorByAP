using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace MacClipboardMonitor.Services;

/// <summary>
/// Gestiona la solicitud y verificación del permiso de Accesibilidad de macOS.
/// Es necesario para: (a) el hook global de teclado (Ctrl+Cmd+V) y (b) el pegado directo.
/// En macOS NO hay API para "pedir" el permiso: se verifica su estado y se abre
/// directamente Ajustes del Sistema → Privacidad y seguridad → Accesibilidad.
/// </summary>
public static class AccessibilityPermissionService
{
    [DllImport("/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices")]
    private static extern bool AXIsProcessTrusted();

    /// <summary>Verifica si la app tiene el permiso de Accesibilidad concedido.</summary>
    public static bool IsTrusted() => AXIsProcessTrusted();

    /// <summary>Abre directamente Ajustes del Sistema en la pantalla de Accesibilidad.</summary>
    public static void OpenSettings()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "open",
                Arguments = "x-apple.systempreferences:com.apple.preference.security?Privacy_Accessibility",
                UseShellExecute = true
            });
        }
        catch
        {
            // Si falla abrir Ajustes, el banner sigue haciendo su trabajo.
        }
    }

    /// <summary>Espera <paramref name="delayMs"/> y vuelve a verificar.</summary>
    public static async Task<bool> CheckAfterDelay(int delayMs = 1500)
    {
        await Task.Delay(delayMs);
        return IsTrusted();
    }
}
