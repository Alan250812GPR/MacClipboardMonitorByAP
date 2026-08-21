using System;
using System.Linq;
using System.Text.Json;

namespace MacClipboardMonitor.Services;

// Detección heurística de código en texto del portapapeles.
public static class CodeDetectionService
{
    public static string? DetectLanguage(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var trimmed = text.Trim();
        if (trimmed.Length < 12) return null;

        // JSON: objeto o arreglo que parsea correctamente.
        if (trimmed[0] is '{' or '[' && IsValidJson(trimmed)) return "JSON";

        // XML / HTML.
        if (trimmed.StartsWith('<'))
        {
            if (ContainsAny(trimmed, "<html", "<div", "<span", "<body", "<p>", "<a ")) return "HTML";
            return "XML";
        }

        // Shell: shebang, prompt o comandos comunes en la primera línea.
        if (trimmed.StartsWith("#!")) return "Shell";
        var firstLine = trimmed.Split('\n')[0].TrimStart();
        if (firstLine.StartsWith("$ ") ||
            ContainsAny(firstLine, "git ", "docker ", "npm ", "npx ", "curl ", "dotnet ", "brew ", "kubectl ", "sudo "))
        {
            return "Shell";
        }

        // SQL: dos o más palabras clave típicas.
        var upper = trimmed.ToUpperInvariant();
        int sqlHits = CountContains(upper,
            "SELECT ", "INSERT INTO", "UPDATE ", "DELETE FROM",
            "WHERE ", "JOIN ", "GROUP BY", "ORDER BY", "CREATE TABLE");
        if (sqlHits >= 2) return "SQL";

        // C#, JS/TS y Python: palabras clave + densidad de símbolos para evitar falsos positivos.
        bool hasSymbols = CountChars(trimmed, ';', '{', '}', '=') >= 3;

        int csHits = CountContains(trimmed,
            "public ", "private ", "class ", "namespace ", "using ",
            "void ", "async ", "await ", "new(", "=>");
        if (hasSymbols && csHits >= 2) return "C#";

        int jsHits = CountContains(trimmed,
            "function ", "const ", "let ", "var ", "=>", "console.log", "return ");
        if (hasSymbols && jsHits >= 2) return "JS";

        int pyHits = CountContains(trimmed,
            "def ", "import ", "from ", "print(", "self.", "elif", "__init__");
        if (pyHits >= 2 && trimmed.Contains(':')) return "Python";

        return null;
    }

    private static bool IsValidJson(string text)
    {
        try
        {
            using var _ = JsonDocument.Parse(text);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool ContainsAny(string text, params string[] needles) =>
        needles.Any(n => text.Contains(n, StringComparison.OrdinalIgnoreCase));

    private static int CountContains(string text, params string[] needles) =>
        needles.Count(n => text.Contains(n, StringComparison.OrdinalIgnoreCase));

    private static int CountChars(string text, params char[] chars) =>
        chars.Sum(c => text.Count(x => x == c));
}
