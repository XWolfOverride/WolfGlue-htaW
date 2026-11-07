namespace WolfGlue.Bridge;

/// <summary>
/// Expone los metodos publicos de la clase a la web como <c>glue.services.&lt;nombre&gt;.&lt;metodo&gt;(...)</c>. El
/// generador de WolfGlue (WolfGlue.Generators, enlazado por WolfGlue.targets) crea en tiempo de compilacion
/// <c>bridge.Register(servicio)</c> y los tipos TypeScript (<c>GlueServicesTypeScript</c>). Sin reflexion: compatible con
/// Native AOT. Los tipos de parametros y resultados tienen que estar en el JsonSerializerContext de la app (UseJson).
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class GlueServiceAttribute(string? name = null) : Attribute
{
    /// <summary>Nombre en la web (por defecto, el de la clase sin "Service" y en camelCase).</summary>
    public string? Name { get; } = name;
}

/// <summary>Metodo publico de un <see cref="GlueServiceAttribute"/> que no se expone a la web.</summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class GlueIgnoreAttribute : Attribute;