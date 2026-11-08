using System.Text.Json;
using System.Text.Json.Serialization;
using WolfGlue.Native;

namespace HtaW.Api;

public sealed class FsEntry
{
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public bool IsDirectory { get; set; }
    public long Size { get; set; }
    public DateTime Modified { get; set; }
}

public sealed class FsStat
{
    public bool Exists { get; set; }
    public bool IsDirectory { get; set; }
    public long Size { get; set; }
    public DateTime Modified { get; set; }
    public DateTime Created { get; set; }
    public bool ReadOnly { get; set; }
}

public sealed class FsWriteText
{
    public string Path { get; set; } = "";
    public string Text { get; set; } = "";
    public bool Append { get; set; }
}

public sealed class FsTwoPaths
{
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public bool Overwrite { get; set; }
}

public sealed class FsRemove
{
    public string Path { get; set; } = "";
    public bool Recursive { get; set; }
}

public sealed class FsWatchArgs
{
    public string Path { get; set; } = "";
    public bool Recursive { get; set; }
}

public sealed class FsWatchEvent
{
    public int Id { get; set; }
    public string Type { get; set; } = "";
    public string Path { get; set; } = "";
    public string? OldPath { get; set; }
}

public sealed class ExecArgs
{
    public string Name { get; set; } = "";
    public string[] Args { get; set; } = [];
    public string? Cwd { get; set; }
    public Dictionary<string, string>? Env { get; set; }
    /// <summary>Milisegundos (solo exec).</summary>
    public int? Timeout { get; set; }
    /// <summary>utf-8 (por defecto), oem, o un nombre de codificacion.</summary>
    public string? Encoding { get; set; }
    /// <summary>Texto para la entrada estandar (solo exec).</summary>
    public string? Input { get; set; }
}

public sealed class ExecResult
{
    public int Code { get; set; }
    public string Stdout { get; set; } = "";
    public string Stderr { get; set; } = "";
}

public sealed class ProcWrite
{
    public int Id { get; set; }
    public string Data { get; set; } = "";
}

public sealed class ProcEvent
{
    public int Id { get; set; }
    /// <summary>stdout | stderr | exit</summary>
    public string Type { get; set; } = "";
    public string? Data { get; set; }
    public int? Code { get; set; }
}

public sealed class HttpArgs
{
    public string Url { get; set; } = "";
    public string Method { get; set; } = "GET";
    public Dictionary<string, string>? Headers { get; set; }
    public string? Body { get; set; }
    public string? BodyBase64 { get; set; }
    /// <summary>text (por defecto) | base64</summary>
    public string? ResponseType { get; set; }
    public int? Timeout { get; set; }
}

public sealed class HttpResult
{
    public int Status { get; set; }
    public string StatusText { get; set; } = "";
    public Dictionary<string, string> Headers { get; set; } = [];
    public string Body { get; set; } = "";
}

public sealed class SettingsSet
{
    public string Key { get; set; } = "";
    public JsonElement Value { get; set; }
}

public sealed class WindowOpen
{
    public string Url { get; set; } = "";
    public string? Title { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public bool Modal { get; set; }
    public bool Resizable { get; set; } = true;
}

/// <summary>Datos que el shim recibe al cargar (<c>window.__htawInit</c>).</summary>
public sealed class HtawInit
{
    public InitApp App { get; set; } = new();
    public string[] Permissions { get; set; } = [];
    public InitEnv? Env { get; set; }
    public ThemeInfo? Theme { get; set; }
    public string Os { get; set; } = "";
    public string[] Warnings { get; set; } = [];
    public bool Dev { get; set; }
}

public sealed class InitApp
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Version { get; set; }
    public string? Author { get; set; }
    public string[] Args { get; set; } = [];
    public string? Dir { get; set; }
    public string File { get; set; } = "";
}

public sealed class InitEnv
{
    public string Home { get; set; } = "";
    public string Temp { get; set; } = "";
    public string Documents { get; set; } = "";
    public string Os { get; set; } = "";
    public string Arch { get; set; } = "";
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(FsEntry[]))]
[JsonSerializable(typeof(SecondInstanceInfo))]
[JsonSerializable(typeof(FsStat))]
[JsonSerializable(typeof(FsWriteText))]
[JsonSerializable(typeof(FsTwoPaths))]
[JsonSerializable(typeof(FsRemove))]
[JsonSerializable(typeof(FsWatchArgs))]
[JsonSerializable(typeof(FsWatchEvent))]
[JsonSerializable(typeof(ExecArgs))]
[JsonSerializable(typeof(ExecResult))]
[JsonSerializable(typeof(ProcWrite))]
[JsonSerializable(typeof(ProcEvent))]
[JsonSerializable(typeof(HttpArgs))]
[JsonSerializable(typeof(HttpResult))]
[JsonSerializable(typeof(SettingsSet))]
[JsonSerializable(typeof(Dictionary<string, JsonElement>))]
[JsonSerializable(typeof(JsonElement?))]
[JsonSerializable(typeof(WindowOpen))]
[JsonSerializable(typeof(HtawInit))]
[JsonSerializable(typeof(GlueNotification))]
[JsonSerializable(typeof(GlueTrayRequest))]
[JsonSerializable(typeof(GlueMenuPopupArgs))]
[JsonSerializable(typeof(GlueTrayUpdateArgs))]
internal partial class ApiJson : JsonSerializerContext;
