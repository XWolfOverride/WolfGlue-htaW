using System.IO.Compression;
using HtaW.Package;

namespace HtaW;

/// <summary><c>htaw pack</c>: valida una carpeta y la empaqueta como .htaw (zip).</summary>
public static class Packer
{
    private static readonly string[] ExcludedDirs = [".git", ".vs", ".vscode", ".idea", "node_modules"];

    /// <summary>Crea el .htaw y devuelve la ruta y el numero de archivos.</summary>
    public static (string output, int count) Pack(string folder, string? output = null)
    {
        folder = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        // Valida manifiesto y entrada exactamente como lo hara htaW al abrirlo.
        // El icono declarado tiene que poder usarse: al abrir solo es un aviso, al empaquetar es un error.
        using (var pkg = HtawPackage.Open(folder, allowFolder: true))
            if (pkg.IconProblem is { } problem) throw new HtawFormatException(problem);

        output = Path.GetFullPath(output ?? folder + ".htaw");
        var tmp = output + ".tmp";
        int count;
        using (var file = File.Create(tmp)) count = WriteZip(folder, file, output);
        File.Move(tmp, output, true);
        return (output, count);
    }

    /// <summary>Escribe el zip de <paramref name="folder"/> (validada antes) y devuelve el numero de archivos.</summary>
    internal static int WriteZip(string folder, Stream output, string? skip = null)
    {
        int count = 0;
        using var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);
        foreach (var file in Files(folder).Order(StringComparer.Ordinal))
        {
            var full = Path.Combine(folder, file);
            if (skip is not null && Path.GetFullPath(full).Equals(skip, StringComparison.OrdinalIgnoreCase)) continue;
            zip.CreateEntryFromFile(full, file, CompressionLevel.Optimal);
            count++;
        }
        return count;
    }

    /// <summary>Rutas relativas con <c>/</c>, sin carpetas de herramientas ni otros .htaw.</summary>
    internal static IEnumerable<string> Files(string folder)
    {
        var stack = new Stack<string>([folder]);
        while (stack.Count > 0)
        {
            var dir = stack.Pop();
            foreach (var d in Directory.EnumerateDirectories(dir))
                if (!ExcludedDirs.Contains(Path.GetFileName(d), StringComparer.OrdinalIgnoreCase)) stack.Push(d);
            foreach (var f in Directory.EnumerateFiles(dir))
            {
                if (f.EndsWith(".htaw", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".htaw.tmp", StringComparison.OrdinalIgnoreCase)) continue;
                yield return Path.GetRelativePath(folder, f).Replace('\\', '/');
            }
        }
    }
}
