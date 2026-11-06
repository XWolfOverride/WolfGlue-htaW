using System.Runtime.CompilerServices;
using HtaW.Package;

namespace HtaW;

/// <summary>Resultado de <see cref="Bundler.Bundle"/>.</summary>
public sealed record BundleResult(string Output, long Size, bool IconSet, string? IconNote);

/// <summary>
/// <c>htaw bundle</c>: une un ejecutable de htaW y una app (carpeta, .htaw zip o HTML suelto) en un solo ejecutable
/// portable. En Windows, si la app declara <c>icon</c>, el ejecutable lleva ese icono en sus recursos.
/// </summary>
public static class Bundler
{
    /// <param name="source">Carpeta con htaw.json, o archivo .htaw / .html.</param>
    /// <param name="output">Ejecutable de salida. Por defecto, junto al origen con su nombre (+ .exe si la base lo es;
    /// + .bin si sin extension chocaria con una carpeta).</param>
    /// <param name="baseExe">Ejecutable de htaW publicado (AOT) que se usa como base. Por defecto, el que se esta ejecutando.</param>
    public static BundleResult Bundle(string source, string? output = null, string? baseExe = null)
    {
        source = Path.GetFullPath(source).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        // 1. La app: se valida como al abrirla y se obtiene su contenido y su icono.
        byte[] app;
        EmbeddedKind kind;
        byte[]? icon;
        bool folder = Directory.Exists(source);
        using (var pkg = HtawPackage.Open(source, allowFolder: true))
        {
            if (pkg.IconProblem is { } problem) throw new HtawFormatException(problem);
            icon = pkg.Icon;
            if (folder)
            {
                using var ms = new MemoryStream();
                Packer.WriteZip(source, ms);
                (app, kind) = (ms.ToArray(), EmbeddedKind.Zip);
            }
            else (app, kind) = (File.ReadAllBytes(source), pkg.Kind == PackageKind.Zip ? EmbeddedKind.Zip : EmbeddedKind.Html);
        }

        // 2. La base: un htaW publicado, sin otra app dentro.
        baseExe = baseExe is null ? CurrentExe() : Path.GetFullPath(baseExe);
        if (!File.Exists(baseExe)) throw new HtawFormatException(Strings.FileNotFound(baseExe));
        var exeBytes = File.ReadAllBytes(baseExe);
        using (var ms = new MemoryStream(exeBytes))
            if (EmbeddedApp.Find(ms) is { } previous) exeBytes = exeBytes[..(int)previous.Offset];
        bool pe = exeBytes.Length > 64 && exeBytes[0] == 'M' && exeBytes[1] == 'Z';

        var name = folder ? Path.GetFileName(source) : Path.GetFileNameWithoutExtension(source);
        output = Path.GetFullPath(output ?? Path.Combine(Path.GetDirectoryName(source)!, name + (pe ? ".exe" : "")));
        // Sin extension (Linux, macOS) el nombre coincidiria con la carpeta de origen.
        if (Directory.Exists(output)) output += ".bin";
        if (string.Equals(output, baseExe, StringComparison.OrdinalIgnoreCase) || string.Equals(output, source, StringComparison.OrdinalIgnoreCase))
            throw new HtawFormatException(Strings.BundleSameFile(output));

        // 3. Copia de la base, icono en sus recursos (antes de anadir la app: UpdateResource reescribe el archivo)
        //    y la app al final.
        var tmp = output + ".tmp";
        File.WriteAllBytes(tmp, exeBytes);
        bool iconSet = false;
        string? note = null;
        try
        {
            if (icon is not null && pe)
            {
                if (OperatingSystem.IsWindows())
                {
                    ExeIcon.Replace(tmp, icon);
                    iconSet = true;
                }
                else note = Strings.BundleIconWindowsOnly;
            }
            using (var f = new FileStream(tmp, FileMode.Append, FileAccess.Write))
                EmbeddedApp.Append(f, app, kind);
            File.Move(tmp, output, true);
        }
        catch
        {
            try { File.Delete(tmp); } catch { /* ya no importa */ }
            throw;
        }
        CopyExecutableBit(baseExe, output);
        return new BundleResult(output, new FileInfo(output).Length, iconSet, note);
    }

    /// <summary>
    /// El ejecutable actual, si sirve como base: solo un htaW publicado con Native AOT es un unico archivo. Con
    /// <c>dotnet run</c> el .exe es un lanzador que necesita las DLL de al lado, asi que hay que indicar la base.
    /// </summary>
    private static string CurrentExe()
    {
        if (RuntimeFeature.IsDynamicCodeSupported) throw new HtawFormatException(Strings.BundleNeedsAot);
        return Environment.ProcessPath!;
    }

    private static void CopyExecutableBit(string from, string to)
    {
        if (OperatingSystem.IsWindows()) return;
        File.SetUnixFileMode(to, File.GetUnixFileMode(from) | UnixFileMode.UserExecute);
    }
}
