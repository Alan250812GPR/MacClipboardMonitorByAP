using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace MacClipboardMonitor.Services;

// Pega el contenido del portapapeles en la aplicación activa simulando Cmd+V
// mediante CoreGraphics (requiere permiso de Accesibilidad).
public class MacPasteService : IPasteService
{
    private const uint kCGHIDEventTap = 0;
    private const ulong kCGEventFlagMaskCommand = 1UL << 20;
    private const ushort KeyV = 9; // Código virtual de la tecla V

    public bool CanPaste()
    {
        return AXIsProcessTrusted();
    }

    public async Task PasteToActiveAppAsync(int delayMilliseconds = 200)
    {
        if (!CanPaste())
        {
            return;
        }

        // Espera a que la app anterior recupere el foco tras ocultar nuestra ventana.
        await Task.Delay(delayMilliseconds);

        PostKey(KeyV, keyDown: true);
        PostKey(KeyV, keyDown: false);
    }

    private static void PostKey(ushort keyCode, bool keyDown)
    {
        IntPtr evt = CGEventCreateKeyboardEvent(IntPtr.Zero, keyCode, keyDown);
        if (evt == IntPtr.Zero)
        {
            return;
        }

        CGEventSetFlags(evt, kCGEventFlagMaskCommand);
        CGEventPost(kCGHIDEventTap, evt);
        CFRelease(evt);
    }

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern IntPtr CGEventCreateKeyboardEvent(IntPtr source, ushort virtualKey, bool keyDown);

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern void CGEventSetFlags(IntPtr @event, ulong flags);

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern void CGEventPost(uint tap, IntPtr @event);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern void CFRelease(IntPtr cf);

    [DllImport("/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices")]
    private static extern bool AXIsProcessTrusted();
}
