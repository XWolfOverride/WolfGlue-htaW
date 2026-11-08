using System.Text.Json.Serialization;

namespace WolfGlue.Native;

/// <summary>Boton de una notificacion.</summary>
public sealed class GlueNotificationButton
{
    public string Id { get; set; } = "";
    public string Text { get; set; } = "";
}

/// <summary>
/// Notificacion del sistema: titulo, texto, hasta <see cref="NativeNotifications.MaxButtons"/> botones y un
/// <see cref="Id"/> opcional (una notificacion con el mismo id sustituye a la anterior).
/// </summary>
public sealed class GlueNotification
{
    /// <summary>Si se omite, se genera uno. Mostrar otra con el mismo id sustituye a la anterior.</summary>
    public string? Id { get; set; }
    public string Title { get; set; } = "";
    public string? Body { get; set; }
    /// <summary>Sin sonido.</summary>
    public bool Silent { get; set; }
    public GlueNotificationButton[]? Buttons { get; set; }
}

/// <summary>Permiso del sistema para mostrar notificaciones de la app.</summary>
public enum NotificationPermission { Granted, Denied }

/// <summary>Lo que se envia a las paginas cuando el usuario actua sobre una notificacion (<c>glue.notification</c>).</summary>
public sealed class NotificationEvent
{
    /// <summary><c>click</c> (la notificacion) o <c>button</c> (un boton).</summary>
    public string Type { get; set; } = "";
    public string Id { get; set; } = "";
    public string? Button { get; set; }
}

/// <summary>
/// Notificaciones del sistema. Solo existen mientras la app esta abierta: al terminar <see cref="GlueApp.Run"/> se
/// retiran todas, asi que ninguna deja un clic que no lleve a ninguna parte.
/// </summary>
public sealed class NativeNotifications
{
    public const int MaxButtons = 2;

    private readonly GlueApp _app;
    private int _next;
    internal NativeNotifications(GlueApp app) => _app = app;

    /// <summary>Se produce en el hilo de UI al pulsar la notificacion (no un boton).</summary>
    public event Action<string>? Clicked;

    /// <summary>Se produce en el hilo de UI al pulsar un boton: id de la notificacion e id del boton.</summary>
    public event Action<string, string>? ButtonClicked;

    /// <summary>Muestra una notificacion y devuelve su id. Lanza <see cref="ArgumentException"/> si el modelo no es valido.</summary>
    public Task<string> ShowAsync(GlueNotification n)
    {
        Validate(n);
        var id = string.IsNullOrEmpty(n.Id) ? "n" + Interlocked.Increment(ref _next) : n.Id;
        return GlueNative.OnUi(_app, () =>
        {
            _app.Platform.Services.ShowNotification(id, n);
            return id;
        });
    }

    /// <summary>Retira una notificacion (no hace nada si ya no esta).</summary>
    public Task CloseAsync(string id) => GlueNative.OnUi(_app, () =>
    {
        _app.Platform.Services.CloseNotification(id);
        return true;
    });

    /// <summary>Si el sistema permite mostrar notificaciones de esta app (el usuario las puede desactivar).</summary>
    public Task<NotificationPermission> GetPermissionAsync() =>
        GlueNative.OnUi(_app, () => _app.Platform.Services.GetNotificationPermission());

    internal static void Validate(GlueNotification n)
    {
        if (string.IsNullOrWhiteSpace(n.Title)) throw new ArgumentException("La notificacion necesita un titulo.", nameof(n));
        if (n.Buttons is { Length: > MaxButtons })
            throw new ArgumentException($"Como mucho {MaxButtons} botones por notificacion.", nameof(n));
        if (n.Buttons is not null && n.Buttons.Any(b => string.IsNullOrEmpty(b.Id) || string.IsNullOrWhiteSpace(b.Text)))
            throw new ArgumentException("Cada boton necesita id y texto.", nameof(n));
    }

    /// <summary>Lo llama el backend (en cualquier hilo) cuando el usuario actua sobre una notificacion.</summary>
    internal void Raise(string id, string? button)
    {
        _app.Invoke(() =>
        {
            if (button is null) Clicked?.Invoke(id);
            else ButtonClicked?.Invoke(id, button);
            _app.Emit("glue.notification", new NotificationEvent { Type = button is null ? "click" : "button", Id = id, Button = button });
        });
    }

    /// <summary>Retira todas (al terminar la app).</summary>
    internal void CloseAll()
    {
        try { _app.Platform.Services.CloseAllNotifications(); }
        catch (Exception) { /* al salir no debe fallar nada */ }
    }
}
