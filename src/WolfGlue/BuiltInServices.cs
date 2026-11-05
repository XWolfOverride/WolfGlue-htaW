using System.Text.Json;
using WolfGlue.Bridge;
using WolfGlue.Native;

namespace WolfGlue;

/// <summary>Metodos <c>glue.*</c> que el shim JS usa para controlar ventanas y la app.</summary>
internal static class BuiltInServices
{
    internal static void Register(GlueApp app)
    {
        var b = app.Bridge;

        b.Register<JsonElement>("glue.window.close", (result, ctx) =>
        {
            var w = Win(ctx);
            if (result.ValueKind is not JsonValueKind.Undefined and not JsonValueKind.Null) w.Result = result.GetRawText();
            app.Platform.Post(w.Close); // encolado: se responde antes de cerrar
        });
        b.Register<string>("glue.window.setTitle", (title, ctx) => Win(ctx).Title = title);
        b.Register<SizeArgs>("glue.window.setSize", (s, ctx) => Win(ctx).SetSize(s.Width, s.Height));
        b.Register<JsonElement>("glue.window.center", (_, ctx) => Win(ctx).Center());
        b.Register<JsonElement>("glue.window.minimize", (_, ctx) => Win(ctx).Minimize());
        b.Register<JsonElement>("glue.window.maximize", (_, ctx) => Win(ctx).Maximize());
        b.Register<JsonElement>("glue.window.restore", (_, ctx) => Win(ctx).Restore());
        b.Register<JsonElement>("glue.window.devtools", (_, ctx) =>
        {
            if (app.Options.DevTools) Win(ctx).OpenDevTools();
        });

        // Abre una ventana; si es modal, la promesa se resuelve al cerrarse con su resultado (JSON o null).
        b.Register<WindowOpenArgs, WindowInfo>("glue.window.open", async (a, ctx) =>
        {
            var owner = Win(ctx);
            var url = owner.ResolveUrl(a.Url);
            if (!owner.IsTrustedUrl(url)) throw new GlueException("InvalidArgument", "URL fuera del origen propio: " + a.Url);
            var w = app.CreateWindow(new GlueWindowOptions
            {
                StartPage = url,
                Title = a.Title,
                Width = a.Width ?? owner.Options.Width,
                Height = a.Height ?? owner.Options.Height,
                Resizable = a.Resizable,
                Owner = a.Modal ? owner : null,
                Modal = a.Modal,
                Content = owner.Options.Content,
            });
            if (!a.Modal)
            {
                w.Show();
                return new WindowInfo { Id = w.Id };
            }
            var result = await w.ShowDialogAsync();
            return new WindowInfo { Id = w.Id, Result = result };
        });

        b.Register<JsonElement>("glue.app.quit", (code, _) =>
            app.Quit(code.ValueKind == JsonValueKind.Number ? code.GetInt32() : 0));
    }

    /// <summary>Servicios nativos para la web (si <see cref="GlueAppOptions.NativeServicesInWeb"/>).</summary>
    internal static void RegisterNative(GlueApp app)
    {
        var b = app.Bridge;
        var n = app.Native;

        b.Register<FileDialogOptions, string[]?>("glue.dialog.open", (o, ctx) => n.Dialogs.OpenFileAsync(o, ctx.Window));
        b.Register<FileDialogOptions, string?>("glue.dialog.save", (o, ctx) => n.Dialogs.SaveFileAsync(o, ctx.Window));
        b.Register<FolderDialogOptions, string?>("glue.dialog.folder", (o, ctx) => n.Dialogs.ChooseFolderAsync(o, ctx.Window));
        b.Register<MessageOptions, MessageResult>("glue.dialog.message", (o, ctx) => n.Dialogs.MessageAsync(o, ctx.Window));

        b.Register<JsonElement, string?>("glue.clipboard.readText", (_, _) => n.Clipboard.GetTextAsync());
        b.Register<string>("glue.clipboard.writeText", (t, _) => n.Clipboard.SetTextAsync(t ?? ""));

        b.Register<JsonElement, ScreenInfo[]>("glue.screens", _ => [.. n.Screens.GetAll()]);
        b.Register<JsonElement, ThemeInfo>("glue.theme", _ => n.Theme.Current);

        b.Register<string>("glue.shell.openUrl", (url, _) =>
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var u) || (u.Scheme != "https" && u.Scheme != "http" && u.Scheme != "mailto"))
                throw new GlueException("InvalidArgument", "Solo URLs http, https o mailto.");
            n.Shell.OpenUrl(u.AbsoluteUri);
        });
        b.Register<string>("glue.shell.open", (path, _) => n.Shell.Open(path));
        b.Register<string>("glue.shell.reveal", (path, _) => n.Shell.Reveal(path));
    }

    private static GlueWindow Win(GlueCallContext ctx) =>
        ctx.Window ?? throw new GlueException("InvalidArgument", "Llamada sin ventana.");
}
