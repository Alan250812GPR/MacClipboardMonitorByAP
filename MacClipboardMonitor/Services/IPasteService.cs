using System.Threading.Tasks;

namespace MacClipboardMonitor.Services;

public interface IPasteService
{
    // Indica si el sistema permite simular teclado (permiso de Accesibilidad en macOS).
    bool CanPaste();

    // Envía Cmd+V a la aplicación con el foco (tras un delay para el cambio de foco).
    Task PasteToActiveAppAsync(int delayMilliseconds = 200);
}
