using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace MacClipboardMonitor.Services;

/// <summary>
/// Guard de versión única: solo la última M.M.M puede correr.
/// - Sin versión o versión menor que LatestVersion → salida silenciosa.
/// - Al arrancar la última, mata cualquier otro proceso MacClipboardMonitor y toma el lock.
/// - Segunda instancia de la misma última versión → salida silenciosa.
/// </summary>
public static class VersionGuard
{
    private static FileStream? _lockStream;
    private static string LockPath => Path.Combine(Path.GetTempPath(), "MacClipboardMonitor.lock");

    /// <summary>Llamar al inicio de Program.Main, antes de Avalonia. Retorna false si debe abortar.</summary>
    public static bool Enforce()
    {
        try
        {
            var current = AppVersion.Current;
            // Sin versión → bloqueo silencioso
            if (!AppVersion.IsValid(current))
                Environment.Exit(0);

            var config = AppConfigService.Load();

            // Si hay LatestVersion registrada y current es menor → bloqueo silencioso
            if (AppVersion.TryParse(config.LatestVersion, out var latest))
            {
                if (AppVersion.Compare(current, latest) < 0)
                    Environment.Exit(0);

                // Si current > latest → actualizar latest (nueva última versión)
                if (AppVersion.Compare(current, latest) > 0)
                {
                    config.LatestVersion = AppVersion.CurrentString;
                    config.Save();
                    // Matar procesos viejos (cualquier otro MacClipboardMonitor)
                    KillOtherProcesses();
                }
            }
            else
            {
                // Primera ejecución o config sin versión: registrar current como latest
                // Si LatestVersion es vacío/invalido, no bloquear; solo registrar.
                if (string.IsNullOrWhiteSpace(config.LatestVersion))
                {
                    config.LatestVersion = AppVersion.CurrentString;
                    config.Save();
                }
                else
                {
                    // LatestVersion presente pero inválido → tratar como sin versión en config
                    // y registrar current
                    config.LatestVersion = AppVersion.CurrentString;
                    config.Save();
                }
            }

            // Si llegamos aquí, current == latest (o recién promovido a latest)
            // 1) Matar otros procesos huérfanos de versiones anteriores que sigan vivos
            // 2) Intentar tomar lock exclusivo; si falla, otra instancia de la misma última ya corre → salir
            KillOtherProcesses();

            if (!TryAcquireLock())
                Environment.Exit(0);

            return true;
        }
        catch
        {
            // Cualquier error inesperado no debe crashear con diálogo; salida silenciosa
            try { Environment.Exit(0); } catch { }
            return false;
        }
    }

    private static void KillOtherProcesses()
    {
        try
        {
            var currentPid = Environment.ProcessId;
            var procs = Process.GetProcessesByName("MacClipboardMonitor");
            foreach (var p in procs)
            {
                try
                {
                    if (p.Id == currentPid) continue;
                    p.Kill();
                    p.WaitForExit(1000);
                }
                catch { /* ignorar */ }
                finally { p.Dispose(); }
            }
        }
        catch { }
    }

    private static bool TryAcquireLock()
    {
        try
        {
            // Intentar crear lock exclusivo. Si otro proceso lo tiene, lanzará IOException.
            _lockStream = new FileStream(LockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            // Mantener el stream abierto todo el ciclo de vida; se libera al salir.
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch
        {
            return false;
        }
    }

    public static void Release()
    {
        try
        {
            _lockStream?.Dispose();
            _lockStream = null;
            if (File.Exists(LockPath))
                File.Delete(LockPath);
        }
        catch { }
    }
}
