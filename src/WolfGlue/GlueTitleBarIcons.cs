namespace WolfGlue;

/// <summary>
/// Iconos con nombre de la barra de titulo, comunes a todos los sistemas: cada uno se traduce al icono nativo (Segoe
/// Fluent Icons en Windows, SF Symbols en macOS, iconos simbolicos de freedesktop en Linux). La lista crece con el uso;
/// los nombres nunca cambian de significado.
/// </summary>
public static class GlueTitleBarIcons
{
    /// <summary>Nombre → (codigo en Segoe Fluent/MDL2, SF Symbol, icono simbolico de Linux).</summary>
    internal static readonly IReadOnlyDictionary<string, (int Windows, string MacOS, string Linux)> Table =
        new Dictionary<string, (int, string, string)>(StringComparer.Ordinal)
        {
            ["add"] = (0xE710, "plus", "list-add-symbolic"),
            ["remove"] = (0xE738, "minus", "list-remove-symbolic"),
            ["document"] = (0xE8A5, "doc", "text-x-generic-symbolic"),
            ["open"] = (0xE8E5, "folder", "document-open-symbolic"),
            ["folder"] = (0xE8B7, "folder", "folder-symbolic"),
            ["save"] = (0xE74E, "square.and.arrow.down", "document-save-symbolic"),
            ["print"] = (0xE749, "printer", "printer-symbolic"),
            ["undo"] = (0xE7A7, "arrow.uturn.backward", "edit-undo-symbolic"),
            ["redo"] = (0xE7A6, "arrow.uturn.forward", "edit-redo-symbolic"),
            ["cut"] = (0xE8C6, "scissors", "edit-cut-symbolic"),
            ["copy"] = (0xE8C8, "doc.on.doc", "edit-copy-symbolic"),
            ["paste"] = (0xE77F, "doc.on.clipboard", "edit-paste-symbolic"),
            ["delete"] = (0xE74D, "trash", "user-trash-symbolic"),
            ["edit"] = (0xE70F, "pencil", "document-edit-symbolic"),
            ["search"] = (0xE721, "magnifyingglass", "system-search-symbolic"),
            ["settings"] = (0xE713, "gearshape", "emblem-system-symbolic"),
            ["refresh"] = (0xE72C, "arrow.clockwise", "view-refresh-symbolic"),
            ["sync"] = (0xE895, "arrow.triangle.2.circlepath", "emblem-synchronizing-symbolic"),
            ["back"] = (0xE72B, "chevron.backward", "go-previous-symbolic"),
            ["forward"] = (0xE72A, "chevron.forward", "go-next-symbolic"),
            ["up"] = (0xE74A, "chevron.up", "go-up-symbolic"),
            ["down"] = (0xE74B, "chevron.down", "go-down-symbolic"),
            ["home"] = (0xE80F, "house", "go-home-symbolic"),
            ["share"] = (0xE72D, "square.and.arrow.up", "emblem-shared-symbolic"),
            ["download"] = (0xE896, "arrow.down.circle", "folder-download-symbolic"),
            ["upload"] = (0xE898, "arrow.up.circle", "document-send-symbolic"),
            ["play"] = (0xE768, "play.fill", "media-playback-start-symbolic"),
            ["pause"] = (0xE769, "pause.fill", "media-playback-pause-symbolic"),
            ["stop"] = (0xE71A, "stop.fill", "media-playback-stop-symbolic"),
            ["filter"] = (0xE71C, "line.3.horizontal.decrease", "view-more-symbolic"),
            ["sort"] = (0xE8CB, "arrow.up.arrow.down", "view-sort-ascending-symbolic"),
            ["list"] = (0xE8FD, "list.bullet", "view-list-symbolic"),
            ["more"] = (0xE712, "ellipsis", "view-more-horizontal-symbolic"),
            ["menu"] = (0xE700, "line.3.horizontal", "open-menu-symbolic"),
            ["sidebar"] = (0xE90C, "sidebar.left", "sidebar-show-symbolic"),
            ["info"] = (0xE946, "info.circle", "dialog-information-symbolic"),
            ["help"] = (0xE897, "questionmark.circle", "help-browser-symbolic"),
            ["warning"] = (0xE7BA, "exclamationmark.triangle", "dialog-warning-symbolic"),
            ["star"] = (0xE734, "star", "starred-symbolic"),
            ["heart"] = (0xEB51, "heart", "emblem-favorite-symbolic"),
            ["emoji"] = (0xE76E, "face.smiling", "face-smile-symbolic"),
            ["user"] = (0xE77B, "person", "avatar-default-symbolic"),
            ["mail"] = (0xE715, "envelope", "mail-unread-symbolic"),
            ["link"] = (0xE71B, "link", "insert-link-symbolic"),
            ["lock"] = (0xE72E, "lock", "changes-prevent-symbolic"),
            ["unlock"] = (0xE785, "lock.open", "changes-allow-symbolic"),
            ["bell"] = (0xEA8F, "bell", "preferences-system-notifications-symbolic"),
            ["calendar"] = (0xE787, "calendar", "x-office-calendar-symbolic"),
            ["clock"] = (0xE917, "clock", "alarm-symbolic"),
            ["terminal"] = (0xE756, "terminal", "utilities-terminal-symbolic"),
            ["code"] = (0xE943, "chevron.left.forwardslash.chevron.right", "text-x-script-symbolic"),
            ["bold"] = (0xE8DD, "bold", "format-text-bold-symbolic"),
            ["italic"] = (0xE8DB, "italic", "format-text-italic-symbolic"),
            ["underline"] = (0xE8DC, "underline", "format-text-underline-symbolic"),
            ["zoomIn"] = (0xE8A3, "plus.magnifyingglass", "zoom-in-symbolic"),
            ["zoomOut"] = (0xE71F, "minus.magnifyingglass", "zoom-out-symbolic"),
            ["fullscreen"] = (0xE740, "arrow.up.left.and.arrow.down.right", "view-fullscreen-symbolic"),
            ["pin"] = (0xE718, "pin", "view-pin-symbolic"),
            ["tag"] = (0xE8EC, "tag", "bookmark-new-symbolic"),
            ["flag"] = (0xE7C1, "flag", "emblem-important-symbolic"),
            ["cloud"] = (0xE753, "cloud", "weather-overcast-symbolic"),
            ["globe"] = (0xE774, "globe", "web-browser-symbolic"),
            ["image"] = (0xEB9F, "photo", "image-x-generic-symbolic"),
            ["chat"] = (0xE8BD, "bubble.left", "mail-message-new-symbolic"),
            ["check"] = (0xE73E, "checkmark", "object-select-symbolic"),
            ["close"] = (0xE711, "xmark", "window-close-symbolic"),
            ["light"] = (0xE706, "sun.max", "weather-clear-symbolic"),
            ["dark"] = (0xE708, "moon", "weather-clear-night-symbolic"),
        };

    /// <summary>Nombres disponibles, en orden alfabetico.</summary>
    public static IEnumerable<string> Names => Table.Keys.Order(StringComparer.Ordinal);

    /// <summary>Icono valido: nombre de la lista, <c>font:XXXX</c> o ruta (o URL data:) de un PNG o ICO.</summary>
    public static bool IsValid(string icon) =>
        Table.ContainsKey(icon) || (icon.StartsWith("font:", StringComparison.OrdinalIgnoreCase) ? FontChar(icon) is not null : IsImagePath(icon));

    internal static bool IsImagePath(string icon)
    {
        if (icon.StartsWith("data:image/png;base64,", StringComparison.OrdinalIgnoreCase)
            || icon.StartsWith("data:image/x-icon;base64,", StringComparison.OrdinalIgnoreCase)
            || icon.StartsWith("data:image/vnd.microsoft.icon;base64,", StringComparison.OrdinalIgnoreCase)) return true;
        var ext = Path.GetExtension(icon.Split('?')[0]);
        return (ext.Equals(".png", StringComparison.OrdinalIgnoreCase) || ext.Equals(".ico", StringComparison.OrdinalIgnoreCase))
               && GlueTitleBarItem.IsSafePath(icon);
    }

    /// <summary>Caracter de un icono <c>font:XXXX</c> (codigo hexadecimal), o null.</summary>
    internal static string? FontChar(string icon)
    {
        var hex = icon[5..];
        if (hex.Length is < 2 or > 6 || !int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out var cp)) return null;
        try { return char.ConvertFromUtf32(cp); } catch (ArgumentOutOfRangeException) { return null; }
    }
}
