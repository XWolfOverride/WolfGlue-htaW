using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace WolfGlue.Generators;

/// <summary>
/// Genera, para las clases con <c>[GlueService]</c>: <c>bridge.Register(servicio)</c> (un metodo del bridge por cada
/// metodo publico, con parametros posicionales) y <c>GlueServicesTypeScript.Definitions</c> (tipos .d.ts de
/// <c>glue.services</c>). Ver DOC/BRIDGE.md.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class GlueServiceGenerator : IIncrementalGenerator
{
    private const string AttributeName = "WolfGlue.Bridge.GlueServiceAttribute";

    private static readonly DiagnosticDescriptor Overload = new(
        "WG001", "Metodo sobrecargado en un GlueService",
        "El metodo '{0}' de '{1}' esta sobrecargado: la web no distingue sobrecargas. Cambia el nombre o marca una con [GlueIgnore].",
        "WolfGlue", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor BadParam = new(
        "WG002", "Parametro no admitido en un GlueService",
        "El parametro '{0}' de '{1}' es ref/out/params: no se puede pasar desde la web",
        "WolfGlue", DiagnosticSeverity.Error, true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var services = context.SyntaxProvider.ForAttributeWithMetadataName(
            AttributeName,
            static (node, _) => node is ClassDeclarationSyntax,
            static (ctx, _) => (INamedTypeSymbol)ctx.TargetSymbol);
        context.RegisterSourceOutput(services.Collect(), static (spc, list) => Emit(spc, list));
    }

    // ------------------------------------------------------------- emision
    private static void Emit(SourceProductionContext spc, ImmutableArray<INamedTypeSymbol> services)
    {
        if (services.IsDefaultOrEmpty) return;
        var ts = new TypeScriptWriter();
        var reg = new StringBuilder();
        var tsServices = new StringBuilder();

        foreach (var svc in services.Distinct<INamedTypeSymbol>(SymbolEqualityComparer.Default).OrderBy(s => s.ToDisplayString()))
        {
            var attr = svc.GetAttributes().First(a => a.AttributeClass?.ToDisplayString() == AttributeName);
            var svcName = attr.ConstructorArguments.Length > 0 && attr.ConstructorArguments[0].Value is string n && n.Length > 0
                ? n
                : Camel(StripSuffix(svc.Name, "Service"));
            var full = svc.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

            var methods = svc.GetMembers().OfType<IMethodSymbol>()
                .Where(m => m.MethodKind == MethodKind.Ordinary && m.DeclaredAccessibility == Accessibility.Public && !m.IsStatic
                            && !m.IsGenericMethod
                            && !m.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == "WolfGlue.Bridge.GlueIgnoreAttribute"))
                .ToList();

            reg.AppendLine($"    /// <summary>Registra los metodos de <see cref=\"{full.Replace("global::", "")}\"/> como \"{svcName}.*\".</summary>");
            reg.AppendLine($"    public static void Register(this global::WolfGlue.Bridge.GlueBridge bridge, {full} service)");
            reg.AppendLine("    {");
            tsServices.Append(Doc(svc, "  "));
            tsServices.AppendLine($"  {TsKey(svcName)}: {{");

            foreach (var group in methods.GroupBy(m => JsName(m.Name)))
            {
                if (group.Count() > 1)
                {
                    foreach (var m in group)
                        spc.ReportDiagnostic(Diagnostic.Create(Overload, m.Locations.FirstOrDefault(), m.Name, svc.Name));
                    continue;
                }
                var method = group.First();
                var bad = method.Parameters.FirstOrDefault(p => p.RefKind != RefKind.None || p.IsParams);
                if (bad is not null)
                {
                    spc.ReportDiagnostic(Diagnostic.Create(BadParam, bad.Locations.FirstOrDefault(), bad.Name, method.Name));
                    continue;
                }
                EmitMethod(reg, svcName, group.Key, method);
                tsServices.Append(Doc(method, "    "));
                var ps = method.Parameters.Where(p => Injected(p.Type) is null)
                    .Select(p => $"{TsIdent(p.Name)}{(p.HasExplicitDefaultValue ? "?" : "")}: {ts.Type(p.Type)}");
                tsServices.AppendLine($"    {TsKey(group.Key)}({string.Join(", ", ps)}): Promise<{ts.Return(method.ReturnType)}>;");
            }
            reg.AppendLine("    }");
            reg.AppendLine();
            tsServices.AppendLine("  };");
        }

        var dts = new StringBuilder();
        dts.AppendLine("// Generado por WolfGlue a partir de las clases con [GlueService]. No editar: se regenera al compilar.");
        dts.AppendLine("// Uso: /// <reference path=\"glue-services.d.ts\" />  y despues  await glue.services.<servicio>.<metodo>(...)");
        dts.AppendLine();
        dts.AppendLine("interface GlueServices {");
        dts.Append(tsServices);
        dts.AppendLine("}");
        dts.AppendLine();
        if (ts.Interfaces.Count > 0)
        {
            dts.AppendLine("declare namespace GlueTypes {");
            foreach (var i in ts.Interfaces) dts.Append(i);
            dts.AppendLine("}");
            dts.AppendLine();
        }
        dts.AppendLine("declare const glue: {");
        dts.AppendLine("  readonly services: GlueServices;");
        dts.AppendLine("  call(name: string, args?: unknown): Promise<any>;");
        dts.AppendLine("  on(name: string, fn: (data: any) => void): () => void;");
        dts.AppendLine("  off(name: string, fn: (data: any) => void): void;");
        dts.AppendLine("  emit(name: string, data?: unknown, options?: { to?: string }): void;");
        dts.AppendLine("  readonly [key: string]: any;");
        dts.AppendLine("};");

        var src = new StringBuilder();
        src.AppendLine("// <auto-generated> WolfGlue.Generators: registro de las clases con [GlueService] </auto-generated>");
        src.AppendLine("#nullable enable");
        src.AppendLine("namespace WolfGlue.Bridge;");
        src.AppendLine();
        src.AppendLine("internal static class GlueServicesRegistration");
        src.AppendLine("{");
        src.Append(reg);
        src.AppendLine("}");
        src.AppendLine();
        src.AppendLine("/// <summary>Tipos TypeScript (.d.ts) de <c>glue.services</c>, generados al compilar.</summary>");
        src.AppendLine("internal static class GlueServicesTypeScript");
        src.AppendLine("{");
        src.AppendLine("    public const string Definitions = " + Verbatim(dts.ToString().Replace("\r\n", "\n")) + ";");
        src.AppendLine();
        src.AppendLine("    /// <summary>Escribe las definiciones en <paramref name=\"path\"/> si han cambiado (pensado para compilaciones de depuracion).</summary>");
        src.AppendLine("    public static void Write(string path)");
        src.AppendLine("    {");
        src.AppendLine("        var text = Definitions.Replace(\"\\n\", global::System.Environment.NewLine);");
        src.AppendLine("        if (global::System.IO.File.Exists(path) && global::System.IO.File.ReadAllText(path) == text) return;");
        src.AppendLine("        var dir = global::System.IO.Path.GetDirectoryName(global::System.IO.Path.GetFullPath(path));");
        src.AppendLine("        if (!string.IsNullOrEmpty(dir)) global::System.IO.Directory.CreateDirectory(dir);");
        src.AppendLine("        global::System.IO.File.WriteAllText(path, text);");
        src.AppendLine("    }");
        src.AppendLine("}");
        spc.AddSource("GlueServices.g.cs", src.ToString());
    }

    private static void EmitMethod(StringBuilder sb, string svcName, string jsName, IMethodSymbol m)
    {
        var args = new List<string>();
        int index = 0;
        foreach (var p in m.Parameters)
        {
            if (Injected(p.Type) is { } inj) { args.Add(inj); continue; }
            var t = p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
                SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier | SymbolDisplayMiscellaneousOptions.UseSpecialTypes));
            args.Add($"bridge.Arg<{t}>(a, {index}, \"{p.Name}\")");
            index++;
        }
        var call = $"service.{m.Name}({string.Join(", ", args)})";
        var name = $"\"{svcName}.{jsName}\"";
        var rt = m.ReturnType;
        string fmt(ITypeSymbol t) => t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
            SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier | SymbolDisplayMiscellaneousOptions.UseSpecialTypes));
        const string Je = "global::System.Text.Json.JsonElement", Ctx = "global::WolfGlue.Bridge.GlueCallContext";
        const string Task = "global::System.Threading.Tasks.Task";

        if (rt.SpecialType == SpecialType.System_Void)
            sb.AppendLine($"        bridge.Register<{Je}>({name}, (global::System.Func<{Je}, {Ctx}, {Task}>)((a, ctx) => {{ {call}; return {Task}.CompletedTask; }}));");
        else if (IsTask(rt, out var inner, out bool valueTask))
        {
            var asTask = valueTask ? ".AsTask()" : "";
            if (inner is null)
                sb.AppendLine($"        bridge.Register<{Je}>({name}, (global::System.Func<{Je}, {Ctx}, {Task}>)((a, ctx) => {call}{asTask}));");
            else
                sb.AppendLine($"        bridge.Register<{Je}, {fmt(inner)}>({name}, (global::System.Func<{Je}, {Ctx}, {Task}<{fmt(inner)}>>)((a, ctx) => {call}{asTask}));");
        }
        else
            sb.AppendLine($"        bridge.Register<{Je}, {fmt(rt)}>({name}, (global::System.Func<{Je}, {Ctx}, {Task}<{fmt(rt)}>>)((a, ctx) => {Task}.FromResult<{fmt(rt)}>({call})));");
    }

    /// <summary>Expresion para un parametro que no viene de la web (contexto, cancelacion, ventana), o null.</summary>
    private static string? Injected(ITypeSymbol t) => t.ToDisplayString() switch
    {
        "WolfGlue.Bridge.GlueCallContext" => "ctx",
        "System.Threading.CancellationToken" => "ctx.Cancellation",
        "WolfGlue.GlueWindow" or "WolfGlue.GlueWindow?" => "ctx.Window!",
        _ => null,
    };

    internal static bool IsTask(ITypeSymbol t, out ITypeSymbol? inner, out bool valueTask)
    {
        inner = null;
        valueTask = false;
        if (t is not INamedTypeSymbol n) return false;
        var name = n.ConstructedFrom.ToDisplayString();
        switch (name)
        {
            case "System.Threading.Tasks.Task": return true;
            case "System.Threading.Tasks.ValueTask": valueTask = true; return true;
            case "System.Threading.Tasks.Task<TResult>": inner = n.TypeArguments[0]; return true;
            case "System.Threading.Tasks.ValueTask<TResult>": inner = n.TypeArguments[0]; valueTask = true; return true;
            default: return false;
        }
    }

    // ------------------------------------------------------------- nombres y textos
    internal static string JsName(string method) => Camel(StripSuffix(method, "Async"));

    private static string StripSuffix(string s, string suffix) =>
        s.Length > suffix.Length && s.EndsWith(suffix, StringComparison.Ordinal) ? s.Substring(0, s.Length - suffix.Length) : s;

    internal static string Camel(string s)
    {
        if (s.Length == 0 || char.IsLower(s[0])) return s;
        int i = 0;
        while (i < s.Length && char.IsUpper(s[i])) i++;
        // "URLParser" -> "urlParser"; "Id" -> "id"
        int cut = i == s.Length ? i : Math.Max(1, i - 1);
        return s.Substring(0, cut).ToLowerInvariant() + s.Substring(cut);
    }

    private static readonly Regex Ident = new("^[A-Za-z_$][A-Za-z0-9_$]*$");
    private static string TsKey(string s) => Ident.IsMatch(s) ? s : "\"" + s.Replace("\"", "\\\"") + "\"";
    private static string TsIdent(string s) => Ident.IsMatch(s) ? s : "_" + s;

    private static string Verbatim(string s) => "@\"" + s.Replace("\"", "\"\"") + "\"";

    /// <summary>Resumen XML del simbolo como comentario JSDoc.</summary>
    internal static string Doc(ISymbol s, string indent)
    {
        var xml = s.GetDocumentationCommentXml();
        if (string.IsNullOrWhiteSpace(xml))
        {
            // Sin GenerateDocumentationFile el compilador no procesa los comentarios (/// es un comentario normal):
            // se leen del codigo.
            var trivia = s.DeclaringSyntaxReferences.Select(r => r.GetSyntax()).FirstOrDefault()?.GetLeadingTrivia()
                .Where(t => t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
                            || t.IsKind(SyntaxKind.SingleLineCommentTrivia) && t.ToString().StartsWith("///", StringComparison.Ordinal))
                .Select(t => t.ToFullString() + "\n");
            xml = trivia is null ? "" : Regex.Replace(string.Concat(trivia), @"^\s*///", "", RegexOptions.Multiline);
        }
        if (string.IsNullOrWhiteSpace(xml)) return "";
        var m = Regex.Match(xml, "<summary>(.*?)</summary>", RegexOptions.Singleline);
        if (!m.Success) return "";
        var text = Regex.Replace(m.Groups[1].Value, "<see cref=\"[A-Z]:([^\"]+)\"\\s*/>", x => x.Groups[1].Value.Split('.').Last());
        text = Regex.Replace(text, "<[^>]+>", "");
        text = Regex.Replace(text, "\\s+", " ").Trim().Replace("*/", "* /");
        return text.Length == 0 ? "" : $"{indent}/** {text} */\n";
    }
}

/// <summary>Traduce tipos de C# a TypeScript segun como los serializa System.Text.Json (camelCase).</summary>
internal sealed class TypeScriptWriter
{
    public readonly List<string> Interfaces = [];
    private readonly Dictionary<ITypeSymbol, string> _names = new(SymbolEqualityComparer.Default);
    private readonly HashSet<string> _used = [];

    public string Return(ITypeSymbol t)
    {
        if (t.SpecialType == SpecialType.System_Void) return "void";
        if (GlueServiceGenerator.IsTask(t, out var inner, out _)) return inner is null ? "void" : Type(inner);
        return Type(t);
    }

    public string Type(ITypeSymbol t)
    {
        // Nullable<T> y referencias anotadas con ?
        if (t is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nv)
            return Type(nv.TypeArguments[0]) + " | null";
        if (t.IsReferenceType && t.NullableAnnotation == NullableAnnotation.Annotated)
            return Type(t.WithNullableAnnotation(NullableAnnotation.NotAnnotated)) + " | null";

        switch (t.SpecialType)
        {
            case SpecialType.System_String: case SpecialType.System_Char: return "string";
            case SpecialType.System_Boolean: return "boolean";
            case SpecialType.System_Byte: case SpecialType.System_SByte: case SpecialType.System_Int16: case SpecialType.System_UInt16:
            case SpecialType.System_Int32: case SpecialType.System_UInt32: case SpecialType.System_Int64: case SpecialType.System_UInt64:
            case SpecialType.System_Single: case SpecialType.System_Double: case SpecialType.System_Decimal:
                return "number";
            case SpecialType.System_Object: return "any";
            case SpecialType.System_DateTime: return "string";
        }
        if (t is IArrayTypeSymbol arr)
            return arr.ElementType.SpecialType == SpecialType.System_Byte ? "string" /* base64 */ : Wrap(Type(arr.ElementType)) + "[]";
        if (t.TypeKind == TypeKind.Enum) return "number";

        var name = t.OriginalDefinition.ToDisplayString();
        switch (name)
        {
            case "System.Text.Json.JsonElement": case "System.Text.Json.Nodes.JsonNode": case "System.Text.Json.Nodes.JsonObject":
                return "any";
            case "System.DateTimeOffset": case "System.Guid": case "System.TimeSpan": case "System.Uri": case "System.DateOnly": case "System.TimeOnly":
                return "string";
        }
        if (t is INamedTypeSymbol named)
        {
            if (Dictionary(named) is { } dv) return $"Record<string, {Type(dv)}>";
            if (Enumerable(named) is { } ev) return Wrap(Type(ev)) + "[]";
        }
        if (t.TypeKind is TypeKind.Class or TypeKind.Struct or TypeKind.Interface) return "GlueTypes." + Interface(t);
        return "any";
    }

    private static string Wrap(string s) => s.Contains("|") ? "(" + s + ")" : s;

    private static ITypeSymbol? Dictionary(INamedTypeSymbol t)
    {
        foreach (var i in new[] { t }.Concat(t.AllInterfaces))
        {
            var n = i.OriginalDefinition.ToDisplayString();
            if (n is "System.Collections.Generic.IDictionary<TKey, TValue>" or "System.Collections.Generic.IReadOnlyDictionary<TKey, TValue>"
                or "System.Collections.Generic.Dictionary<TKey, TValue>")
                return i.TypeArguments[1];
        }
        return null;
    }

    private static ITypeSymbol? Enumerable(INamedTypeSymbol t)
    {
        if (t.SpecialType == SpecialType.System_String) return null;
        foreach (var i in new[] { t }.Concat(t.AllInterfaces))
            if (i.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.IEnumerable<T>") return i.TypeArguments[0];
        return null;
    }

    /// <summary>Nombre de la interfaz TypeScript del tipo (la genera la primera vez).</summary>
    private string Interface(ITypeSymbol t)
    {
        t = t.WithNullableAnnotation(NullableAnnotation.None);
        if (_names.TryGetValue(t, out var known)) return known;
        var baseName = t.Name.Length > 0 ? t.Name : "Anon";
        var name = baseName;
        for (int i = 2; !_used.Add(name); i++) name = baseName + i;
        _names[t] = name;

        var sb = new StringBuilder();
        sb.Append(GlueServiceGenerator.Doc(t, "  "));
        sb.AppendLine($"  interface {name} {{");
        var props = new List<IPropertySymbol>();
        for (var c = t; c is not null && c.SpecialType != SpecialType.System_Object && c.SpecialType != SpecialType.System_ValueType; c = c.BaseType)
            props.AddRange(c.GetMembers().OfType<IPropertySymbol>().Where(p => !props.Any(q => q.Name == p.Name)));
        foreach (var p in props)
        {
            if (p.DeclaredAccessibility != Accessibility.Public || p.IsStatic || p.IsIndexer || p.GetMethod is null) continue;
            if (p.GetAttributes().Any(a => a.AttributeClass?.Name == "JsonIgnoreAttribute")) continue;
            if (p.Name == "EqualityContract") continue;
            var jsonName = p.GetAttributes().FirstOrDefault(a => a.AttributeClass?.Name == "JsonPropertyNameAttribute")
                ?.ConstructorArguments.FirstOrDefault().Value as string ?? GlueServiceGenerator.Camel(p.Name);
            sb.Append(GlueServiceGenerator.Doc(p, "    "));
            sb.AppendLine($"    {(Regex.IsMatch(jsonName, "^[A-Za-z_$][A-Za-z0-9_$]*$") ? jsonName : "\"" + jsonName + "\"")}: {Type(p.Type)};");
        }
        sb.AppendLine("  }");
        Interfaces.Add(sb.ToString());
        return name;
    }
}
