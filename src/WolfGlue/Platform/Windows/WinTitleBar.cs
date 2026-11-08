using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using WolfGlue.Native;

namespace WolfGlue.Platform.Windows;

/// <summary>
/// Barra de titulo propia en Windows. La ventana conserva su marco (bordes, sombra, esquinas, ajuste de Windows 11),
/// pero el area de cliente sube hasta arriba (<c>WM_NCCALCSIZE</c>) y esta clase pinta la franja superior con el
/// aspecto del sistema: icono y titulo, los elementos de la app y los botones de minimizar, maximizar y cerrar.
/// <c>WM_NCHITTEST</c> marca las zonas libres como titulo (arrastrar, doble clic, menu del sistema) y el boton de
/// maximizar como tal, para el menu de distribuciones de Windows 11. El webview queda debajo. El campo de busqueda es
/// un control EDIT nativo (cursor, seleccion, portapapeles, metodos de entrada y accesibilidad del sistema).
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed unsafe class WinTitleBar : IDisposable, WinMenu.IBarHook
{
    private sealed class Node
    {
        public required GlueTitleBarItem Item;
        public Node? Group;
        public RECT R;
        /// <summary>Zona de la flecha de un boton partido.</summary>
        public RECT Arrow;
        public bool Visible;
        public int NaturalWidth;
    }

    private enum Cap { None, Min, Max, Close }

    private const int ZoneBody = 0, ZoneArrow = 1;
    internal const nint SearchTimerId = 3;
    internal const uint MsgSearchSubmit = 0x8000 + 20, MsgSearchEscape = 0x8000 + 21;

    private readonly WinWindow _w;
    private GlueTitleBar _spec;
    private readonly List<Node> _nodes = [];
    private RECT _minR, _maxR, _closeR, _titleR;
    private Node? _hover, _press;
    private int _hoverZone, _pressZone;
    private Cap _hoverCap, _pressCap;
    private bool _tracking, _trackingNc;
    private uint _layoutDpi;
    private int _layoutWidth = -1;

    private readonly Dictionary<(int size, bool bold, string face), nint> _fonts = [];
    private readonly Dictionary<(string src, int w, int h, uint tint), nint> _images = [];
    private readonly Dictionary<string, nint> _sources = [];
    private readonly Dictionary<string, byte[]?> _files = [];
    private readonly HashSet<string> _loading = [];
    private string _glyphFace = "";

    private string? _iconFontSrc, _iconFamily;
    private nint _iconFontHandle;

    private nint _tip;
    private readonly List<nint> _tipTexts = [];
    private int _tipCount;

    private nint _edit, _editBrush;
    private uint _editBrushColor = uint.MaxValue;
    private string? _editId, _editText;
    private bool _editFocused, _suppressInput;

    public WinTitleBar(WinWindow window, GlueTitleBar spec)
    {
        _w = window;
        _spec = spec;
        Gdip.Start();
        LoadIconFont();
    }

    public void Set(GlueTitleBar spec)
    {
        _spec = spec;
        _hover = _press = null;
        LoadIconFont();
        Invalidate();
    }

    /// <summary>Alto logico de la barra segun el modo.</summary>
    internal static int LogicalHeightOf(GlueTitleBar? bar) => bar?.Mode switch
    {
        null => 0,
        GlueTitleBar.ModeTitle => 32,
        GlueTitleBar.ModeLabels => 56,
        _ => 40,
    };

    // ------------------------------------------------------------- menu de ventana (render propio)
    // Colocacion segun el modo (DOC/MENUS.md): title (y sin barra propia) = fila propia bajo la barra; compact = en la fila de la
    // barra, tras el titulo; labels = fila superior (titulo + menu) y los iconos debajo.
    private enum MenuKind { None, TopRow, Below }
    private const int MenuRowLogical = 28, TopRowLogical = 32;

    private List<GlueMenuItem>? _menuSpec;
    private readonly List<RECT> _menuR = [];
    private readonly List<bool> _menuVisible = [];
    private int _menuHover = -1, _menuPress = -1, _menuOpen = -1;
    private bool _menuKeyMode, _altPending, _showMnemonics;
    private int _menuFocus = -1;
    private int? _pendingSwitch;

    private MenuKind Kind => _menuSpec is not { Count: > 0 } ? MenuKind.None
        : Labels ? MenuKind.TopRow : MenuKind.Below; // title y compact: fila fina bajo la barra; labels: fila superior

    public void SetMenu(IReadOnlyList<GlueMenuItem>? menu)
    {
        _menuSpec = menu is { Count: > 0 } ? [.. menu] : null;
        _menuHover = _menuPress = _menuOpen = -1;
        Invalidate();
        NotifyMenuState();
    }

    /// <summary>Avisa a la pagina del estado del menu de ventana (puesto, foco del teclado y letras de acceso), para que reenvie Alt y las teclas.</summary>
    internal void NotifyMenuState()
    {
        var sb = new System.Text.StringBuilder();
        if (_menuSpec is not null) foreach (var m in _menuSpec) { var c = Mnemonic(m.Text); if (c != 0) sb.Append(c); }
        _w.Host.OnMenuState(_menuSpec is not null, _menuKeyMode, sb.ToString());
    }

    /// <summary>Alto logico total (franja + fila de menus si la hay).</summary>
    internal int LogicalTotalHeight => LogicalHeightOf(_spec) + Kind switch
    {
        MenuKind.Below => MenuRowLogical,
        MenuKind.TopRow => TopRowLogical,
        _ => 0,
    };

    private int BaseHeightPx => S(LogicalHeightOf(_spec));

    /// <summary>Alto de la fila del titulo (en labels con menu es la fila superior; en el resto, la franja de la barra).</summary>
    private int TitleRowH => Kind == MenuKind.TopRow ? S(TopRowLogical) : BaseHeightPx;

    /// <summary>Alto de la franja en pixeles fisicos.</summary>
    public int HeightPx => S(LogicalTotalHeight);

    private bool Labels => _spec.Mode == GlueTitleBar.ModeLabels;
    private bool TitleMode => _spec.Mode == GlueTitleBar.ModeTitle;

    private uint Dpi => _w.Dpi;
    private int S(int logical) => (int)Math.Round(logical * Dpi / 96.0);

    public void Invalidate()
    {
        _layoutWidth = -1;
        RepaintStrip();
    }

    private void RepaintStrip()
    {
        Win32.GetClientRect(_w.Hwnd, out var c);
        var r = new RECT { Right = c.Right, Bottom = HeightPx };
        Win32.InvalidateRect(_w.Hwnd, ref r, false);
    }

    public void OnDpiChanged()
    {
        FreeFonts();
        FreeImages();
        Invalidate();
    }

    // ------------------------------------------------------------- colores del sistema
    private static uint Ref(GlueRgb c) => c.R | (uint)c.G << 8 | (uint)c.B << 16;
    private static uint Argb(GlueRgb c) => 0xFF000000u | (uint)c.R << 16 | (uint)c.G << 8 | c.B;
    private static uint Rgb(GlueRgb c) => (uint)c.R << 16 | (uint)c.G << 8 | c.B;

    private static GlueRgb Mix(GlueRgb a, GlueRgb b, double t) =>
        new((byte)(b.R + (a.R - b.R) * t), (byte)(b.G + (a.G - b.G) * t), (byte)(b.B + (a.B - b.B) * t));

    private bool Dark => _w.DarkMode;
    private GlueRgb Bg => _w.BarBackground;
    private GlueRgb Fg => Dark ? new GlueRgb(0xff, 0xff, 0xff) : new GlueRgb(0x1b, 0x1b, 0x1b);
    private GlueRgb Text => _w.Active ? Fg : Mix(Fg, Bg, 0.55);
    private GlueRgb Muted => Mix(Fg, Bg, 0.62);
    private GlueRgb Hover => Mix(Fg, Bg, 0.08);
    private GlueRgb Pressed => Mix(Fg, Bg, 0.05);
    private GlueRgb Checked => Mix(Fg, Bg, 0.12);
    private GlueRgb ControlBg => Dark ? Mix(Fg, Bg, 0.06) : Mix(new GlueRgb(0xff, 0xff, 0xff), Bg, 0.7);
    private GlueRgb ControlBorder => Mix(Fg, Bg, 0.14);
    private GlueRgb Accent => Dark ? new GlueRgb(0x60, 0xcd, 0xff) : new GlueRgb(0x00, 0x5f, 0xb8);

    // ------------------------------------------------------------- fuentes
    private nint Font(int logicalSize, bool bold = false, string? face = null)
    {
        face ??= UiFace();
        var key = (S(logicalSize), bold, face);
        if (_fonts.TryGetValue(key, out var f)) return f;
        var lf = new Win32.LOGFONTW { lfHeight = -key.Item1, lfWeight = bold ? 600 : 400, lfCharSet = 1, lfQuality = 5 /* ClearType */ };
        for (int i = 0; i < face.Length && i < 31; i++) lf.lfFaceName[i] = face[i];
        f = Win32.CreateFontIndirectW(&lf);
        _fonts[key] = f;
        return f;
    }

    private static string? _uiFace;

    private static string UiFace()
    {
        if (_uiFace is not null) return _uiFace;
        var ncm = new Win32.NONCLIENTMETRICSW { cbSize = (uint)sizeof(Win32.NONCLIENTMETRICSW) };
        string face = "Segoe UI";
        if (Win32.SystemParametersInfoForDpi(Win32.SPI_GETNONCLIENTMETRICS, ncm.cbSize, &ncm, 0, 96))
        {
            var s = new string(ncm.lfCaptionFont.lfFaceName);
            int nul = s.IndexOf('\0');
            if (nul >= 0) s = s[..nul];
            if (s.Length > 0) face = s;
        }
        return _uiFace = face;
    }

    /// <summary>Fuente de iconos del sistema: Segoe Fluent Icons (Windows 11) o Segoe MDL2 Assets (Windows 10).</summary>
    private string GlyphFace()
    {
        if (_glyphFace.Length > 0) return _glyphFace;
        _glyphFace = "Segoe MDL2 Assets";
        const string fluent = "Segoe Fluent Icons";
        var lf = new Win32.LOGFONTW { lfHeight = -16, lfCharSet = 1 };
        for (int i = 0; i < fluent.Length; i++) lf.lfFaceName[i] = fluent[i];
        var f = Win32.CreateFontIndirectW(&lf);
        var dc = Win32.GetDC(0);
        var old = Win32.SelectObject(dc, f);
        char* name = stackalloc char[32];
        int n = Win32.GetTextFaceW(dc, 32, name);
        if (new string(name, 0, Math.Max(0, n)).TrimEnd('\0') == fluent) _glyphFace = fluent;
        Win32.SelectObject(dc, old);
        Win32.ReleaseDC(0, dc);
        Win32.DeleteObject(f);
        return _glyphFace;
    }

    /// <summary>Carga la fuente de iconos propia (TTF/OTF) solo para este proceso, sin instalarla.</summary>
    private void LoadIconFont()
    {
        var src = _spec.IconFont;
        if (src == _iconFontSrc) return;
        _iconFontSrc = src;
        _iconFamily = null;
        if (src is null) return;
        File(src, data =>
        {
            if (data is null || _iconFontSrc != src) return;
            if (GlueFontInfo.FamilyName(data) is not { } family) return;
            if (_iconFontHandle != 0) { Win32.RemoveFontMemResourceEx(_iconFontHandle); _iconFontHandle = 0; }
            fixed (byte* p = data) _iconFontHandle = Win32.AddFontMemResourceEx(p, (uint)data.Length, 0, out _);
            if (_iconFontHandle == 0) return;
            _iconFamily = family;
            FreeFonts();
        });
    }

    /// <summary>Archivo de la app (icono o fuente): lo pide una vez y avisa cuando llega.</summary>
    private void File(string src, Action<byte[]?>? ready = null)
    {
        if (_files.TryGetValue(src, out var known)) { ready?.Invoke(known); return; }
        if (!_loading.Add(src)) return;
        var scheduler = SynchronizationContext.Current is null ? TaskScheduler.Current : TaskScheduler.FromCurrentSynchronizationContext();
        _w.Host.LoadTitleBarFileAsync(src).ContinueWith(t =>
        {
            _loading.Remove(src);
            var data = t.IsCompletedSuccessfully ? t.Result : null;
            _files[src] = data;
            if (_disposed) return;
            ready?.Invoke(data);
            Invalidate();
        }, scheduler);
    }

    /// <summary>Imagen de la app escalada (y tenida si es monocroma); 0 mientras carga o si no se puede leer.</summary>
    private nint Image(string src, int w, int h, uint? tint)
    {
        var key = (src, w, h, tint ?? uint.MaxValue);
        if (_images.TryGetValue(key, out var img)) return img;
        var source = Source(src);
        if (source == 0) return 0;
        img = Gdip.Scaled(source, w, h, tint);
        _images[key] = img;
        return img;
    }

    private nint Source(string src)
    {
        if (_sources.TryGetValue(src, out var s)) return s;
        if (!_files.TryGetValue(src, out var data)) { File(src); return 0; }
        s = data is null ? 0 : Gdip.Load(data);
        _sources[src] = s;
        return s;
    }

    private (int w, int h) SourceSize(string src)
    {
        var s = Source(src);
        return s == 0 ? (0, 0) : Gdip.Size(s);
    }

    private void FreeFonts()
    {
        foreach (var f in _fonts.Values) Win32.DeleteObject(f);
        _fonts.Clear();
    }

    private void FreeImages()
    {
        foreach (var i in _images.Values) Gdip.Dispose(i);
        _images.Clear();
    }

    // ------------------------------------------------------------- medidas
    private static int Measure(nint hdc, string? text, nint font)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        var old = Win32.SelectObject(hdc, font);
        var r = new RECT();
        Win32.DrawTextW(hdc, text, text.Length, ref r, Win32.DT_SINGLELINE | Win32.DT_CALCRECT | Win32.DT_NOPREFIX);
        Win32.SelectObject(hdc, old);
        return r.Right - r.Left;
    }

    private static bool HasMenu(GlueTitleBarItem it) => it.Menu is { Count: > 0 };

    /// <summary>Ancho natural de un elemento (los espacios flexibles valen 0).</summary>
    private int Width(nint hdc, Node n)
    {
        var it = n.Item;
        switch (it.Type)
        {
            case GlueTitleBarItem.Button:
            {
                int arrow = !HasMenu(it) ? 0 : it.Split ? S(16) : S(12);
                if (TitleMode) return S(40) + (it.Split ? S(16) : 0);
                if (Labels && n.Group is null) return Math.Max(S(48), Measure(hdc, it.Text, Font(11)) + S(16)) + arrow;
                return S(32) + arrow;
            }
            case GlueTitleBarItem.Group:
            {
                int sum = 0;
                foreach (var c in _nodes.Where(c => c.Group == n)) sum += c.NaturalWidth + S(2);
                sum = Math.Max(0, sum - S(2));
                if (Labels && !string.IsNullOrEmpty(it.Text)) sum = Math.Max(sum, Measure(hdc, it.Text, Font(11)) + S(12));
                return sum;
            }
            case GlueTitleBarItem.Panel:
            case GlueTitleBarItem.Search:
                return S(it.Width ?? 240);
            case GlueTitleBarItem.Image:
            {
                int h = S(Labels ? 40 : 28);
                var (sw, sh) = SourceSize(it.Icon!);
                return sw > 0 && sh > 0 ? Math.Min(S(240), sw * h / sh) : h;
            }
            default:
                return 0;
        }
    }

    // ------------------------------------------------------------- colocacion
    private void EnsureLayout(nint hdc, int width)
    {
        if (_layoutWidth == width && _layoutDpi == Dpi) return;
        _layoutWidth = width;
        _layoutDpi = Dpi;
        int barH = BaseHeightPx;
        int capH = Labels ? S(32) : barH;
        int capW = S(46);

        int right = width;
        _closeR = new RECT { Left = right - capW, Top = 0, Right = right, Bottom = capH };
        right -= capW;
        _maxR = _w.HasMaximize ? new RECT { Left = right - capW, Top = 0, Right = right, Bottom = capH } : default;
        if (_w.HasMaximize) right -= capW;
        _minR = _w.HasMinimize ? new RECT { Left = right - capW, Top = 0, Right = right, Bottom = capH } : default;
        if (_w.HasMinimize) right -= capW;

        _nodes.Clear();
        foreach (var it in _spec.Items)
        {
            var n = new Node { Item = it };
            _nodes.Add(n);
            if (it.Items is { } sub)
                foreach (var c in sub)
                {
                    var cn = new Node { Item = c, Group = n };
                    _nodes.Add(cn);
                    cn.NaturalWidth = Width(hdc, cn);
                }
            n.NaturalWidth = Width(hdc, n);
        }
        var top = _nodes.Where(n => n.Group is null).ToList();

        if (TitleMode) LayoutTitleMode(hdc, top, right, barH);
        else LayoutToolbar(hdc, top, right - S(8), barH);
        LayoutMenus(hdc, width, right, barH);
        UpdateSearch();
        RebuildTooltips();
    }

    private void LayoutTitle(nint hdc, int x, int room, int barH)
    {
        _titleR = default;
        if (!_spec.Title || room <= 0) return;
        int iconSpace = _w.IconSmall != 0 ? S(16) + S(8) : 0;
        int tw = Math.Clamp(Measure(hdc, _w.TitleText, Font(12)), 0, Math.Max(0, room - iconSpace));
        _titleR = new RECT { Left = x, Top = 0, Right = x + iconSpace + tw, Bottom = barH };
    }

    /// <summary>Modo title: hasta tres botones de icono pegados a los de la ventana, a toda la altura.</summary>
    private void LayoutTitleMode(nint hdc, List<Node> top, int right, int barH)
    {
        int x = right;
        for (int i = top.Count - 1; i >= 0; i--)
        {
            var n = top[i];
            n.R = new RECT { Left = x - n.NaturalWidth, Top = 0, Right = x, Bottom = barH };
            SplitArrow(n);
            x -= n.NaturalWidth;
        }
        LayoutTitle(hdc, S(12), x - S(24), barH);
        foreach (var n in top) n.Visible = n.R.Left >= Math.Max(_titleR.Right, S(12));
    }

    /// <summary>
    /// Modos compact y labels. Sin elemento centrado: en orden tras el titulo, con los espacios repartiendo lo que sobra.
    /// Con uno centrado: los anteriores a la izquierda, los posteriores a la derecha y el centrado en medio de la ventana.
    /// La busqueda va siempre al final.
    /// </summary>
    private void LayoutToolbar(nint hdc, List<Node> top, int contentRight, int barH)
    {
        int gap = S(Labels ? 6 : 4);
        var search = top.FirstOrDefault(n => n.Item.Type == GlueTitleBarItem.Search);
        var seq = top.Where(n => n != search).ToList();
        int fixedW = seq.Where(n => n.Item.Type != GlueTitleBarItem.Space).Sum(n => n.NaturalWidth)
                     + gap * Math.Max(0, seq.Count - 1);

        int limit = contentRight;
        if (search is not null)
        {
            search.R = Box(limit - search.NaturalWidth, search.NaturalWidth, S(30), barH);
            search.Visible = search.R.Left > S(12);
            limit = search.R.Left - gap * 2;
        }

        int x = S(12);
        if (Kind == MenuKind.TopRow) _titleR = default; // en labels con menu, el titulo va en la fila superior
        else LayoutTitle(hdc, x, limit - x - fixedW - S(16), barH);
        if (_titleR.Right > 0) x = _titleR.Right + S(16);
        int start = x;

        var center = seq.FirstOrDefault(n => n.Item.Center);
        if (center is null)
        {
            int spaces = seq.Count(n => n.Item.Type == GlueTitleBarItem.Space);
            int spaceW = spaces > 0 ? Math.Max(0, (limit - x - fixedW) / spaces) : 0;
            foreach (var n in seq)
            {
                int w = n.Item.Type == GlueTitleBarItem.Space ? spaceW : n.NaturalWidth;
                Place(n, x, w, barH, start, limit);
                x += w + gap;
            }
        }
        else
        {
            int ci = seq.IndexOf(center);
            foreach (var n in seq.Take(ci).Where(n => n.Item.Type != GlueTitleBarItem.Space))
            {
                Place(n, x, n.NaturalWidth, barH, start, limit);
                x += n.NaturalWidth + gap;
            }
            int leftEnd = x;
            int r = limit;
            foreach (var n in seq.Skip(ci + 1).Where(n => n.Item.Type != GlueTitleBarItem.Space).Reverse())
            {
                Place(n, r - n.NaturalWidth, n.NaturalWidth, barH, leftEnd, limit);
                r -= n.NaturalWidth + gap;
            }
            // Un panel centrado se encoge (hasta 120) si no cabe entero entre los dos lados.
            int cw = center.NaturalWidth;
            if (center.Item.Type == GlueTitleBarItem.Panel) cw = Math.Max(S(120), Math.Min(cw, r + gap - leftEnd));
            int cx = Math.Clamp((_layoutWidth - cw) / 2, leftEnd, Math.Max(leftEnd, r - cw + gap));
            Place(center, cx, cw, barH, leftEnd, r + gap);
        }
    }

    /// <summary>Menus fuera de la fila de elementos: fila propia bajo la barra (title) o fila superior (labels, desplazando los iconos).</summary>
    private void LayoutMenus(nint hdc, int width, int right, int barH)
    {
        _menuR.Clear();
        _menuVisible.Clear();
        switch (Kind)
        {
            case MenuKind.Below:
                PlaceMenus(hdc, S(8), barH, S(MenuRowLogical), width - S(8));
                break;
            case MenuKind.TopRow:
            {
                int dy = S(TopRowLogical);
                foreach (var n in _nodes) { n.R = Shift(n.R, dy); n.Arrow = Shift(n.Arrow, dy); }
                // Menus a la izquierda; icono + titulo centrados en la ventana (sin pisar los menus ni los botones de ventana).
                LayoutTitle(hdc, 0, S(240), dy);
                int tw = _titleR.Right - _titleR.Left;
                int center = (width - tw) / 2;
                int menusLimit = tw > 0 ? Math.Max(S(12) + S(60), center - S(16)) : right - S(8);
                int end = PlaceMenus(hdc, S(12), 0, dy, menusLimit);
                if (tw > 0)
                {
                    int tx = Math.Clamp(center, end + S(16), Math.Max(end + S(16), right - S(8) - tw));
                    _titleR = new RECT { Left = tx, Top = 0, Right = tx + tw, Bottom = dy };
                }
                break;
            }
        }
    }

    private static RECT Shift(RECT r, int dy) => r.Right > r.Left ? new RECT { Left = r.Left, Top = r.Top + dy, Right = r.Right, Bottom = r.Bottom + dy } : r;

    /// <summary>
    /// Cuantos menus caben en <paramref name="available"/> pixeles: todos si caben; si no, los que quepan dejando sitio para el boton
    /// de desbordamiento (<paramref name="dotsW"/>), que reune los demas.
    /// </summary>
    internal static int FitCount(IReadOnlyList<int> widths, int gap, int dotsW, int available)
    {
        int total = widths.Sum() + gap * Math.Max(0, widths.Count - 1);
        if (total <= available) return widths.Count;
        int fit = 0, used = 0;
        while (fit < widths.Count && used + widths[fit] + gap + dotsW <= available) { used += widths[fit] + gap; fit++; }
        return fit;
    }

    /// <summary>Coloca los menus de izquierda a derecha desde <paramref name="x"/>; devuelve donde acaba el ultimo visible.</summary>
    private int PlaceMenus(nint hdc, int x, int rowTop, int rowH, int limit)
    {
        if (_menuSpec is null) return x;
        _menuR.Clear();
        _menuVisible.Clear();
        int h = Math.Min(S(MenuRowLogical), rowH - S(4));
        int top = rowTop + (rowH - h) / 2;
        int gap = S(2), dotsW = S(32);
        var widths = _menuSpec.Select(m => Measure(hdc, Plain(m.Text), Font(12)) + S(20)).ToList();
        int fit = FitCount(widths, gap, dotsW, limit - x);
        int cx = x;
        for (int i = 0; i < widths.Count; i++)
        {
            bool visible = i < fit;
            _menuR.Add(visible ? new RECT { Left = cx, Top = top, Right = cx + widths[i], Bottom = top + h } : default);
            _menuVisible.Add(visible);
            if (visible) cx += widths[i] + gap;
        }
        if (fit < widths.Count)
        {
            // Boton «···»: abre un menu contextual con los menus que no caben.
            _menuR.Add(new RECT { Left = cx, Top = top, Right = cx + dotsW, Bottom = top + h });
            _menuVisible.Add(true);
            return cx + dotsW;
        }
        return fit == 0 ? x : cx - gap;
    }

    /// <summary>Texto sin marcas de acceso: un ampersand desaparece y dos son uno literal.</summary>
    internal static string Plain(string text)
    {
        var sb = new System.Text.StringBuilder(text.Length);
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == (char)38) { if (i + 1 < text.Length && text[i + 1] == (char)38) { sb.Append((char)38); i++; } continue; }
            sb.Append(text[i]);
        }
        return sb.ToString();
    }

    private static RECT Box(int x, int w, int h, int barH)
    {
        int y = (barH - h) / 2;
        return new RECT { Left = x, Top = y, Right = x + w, Bottom = y + h };
    }

    private void Place(Node n, int x, int w, int barH, int min, int limit)
    {
        var it = n.Item;
        int h = it.Type switch
        {
            GlueTitleBarItem.Button or GlueTitleBarItem.Group => Labels ? S(48) : S(32),
            GlueTitleBarItem.Panel => Labels ? S(44) : S(30),
            GlueTitleBarItem.Image => Labels ? S(40) : S(28),
            _ => S(32),
        };
        n.R = Box(x, w, h, barH);
        n.Visible = x >= min && n.R.Right <= limit;
        SplitArrow(n);
        if (it.Type != GlueTitleBarItem.Group) return;
        // Botones del grupo: solo icono; en labels, arriba, con la etiqueta del grupo debajo.
        int groupW = _nodes.Where(c => c.Group == n).Sum(c => c.NaturalWidth + S(2)) - S(2);
        int cx = x + Math.Max(0, (w - groupW) / 2);
        foreach (var c in _nodes.Where(c => c.Group == n))
        {
            c.R = new RECT { Left = cx, Top = n.R.Top, Right = cx + c.NaturalWidth, Bottom = Labels ? n.R.Top + S(32) : n.R.Bottom };
            c.Visible = n.Visible;
            SplitArrow(c);
            cx += c.NaturalWidth + S(2);
        }
    }

    private void SplitArrow(Node n) =>
        n.Arrow = n.Item.Split ? new RECT { Left = n.R.Right - S(16), Top = n.R.Top, Right = n.R.Right, Bottom = n.R.Bottom } : default;

    // ------------------------------------------------------------- pintado
    public void Paint(nint hdc)
    {
        Win32.GetClientRect(_w.Hwnd, out var c);
        int width = c.Right, barH = HeightPx;
        if (width <= 0 || barH <= 0) return;
        var mem = Win32.CreateCompatibleDC(hdc);
        var bmp = Win32.CreateCompatibleBitmap(hdc, width, barH);
        var oldBmp = Win32.SelectObject(mem, bmp);
        try
        {
            EnsureLayout(mem, width);
            PaintInto(mem, width, barH);
            Win32.BitBlt(hdc, 0, 0, width, barH, mem, 0, 0, Win32.SRCCOPY);
        }
        finally
        {
            Win32.SelectObject(mem, oldBmp);
            Win32.DeleteObject(bmp);
            Win32.DeleteDC(mem);
        }
    }

    private static void Fill(nint hdc, RECT r, GlueRgb color)
    {
        var b = Win32.CreateSolidBrush(Ref(color));
        Win32.FillRect(hdc, ref r, b);
        Win32.DeleteObject(b);
    }

    private void Round(nint hdc, RECT r, GlueRgb? fill, GlueRgb? border, int radius = 4) =>
        Gdip.RoundRect(hdc, r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top, S(radius), fill is { } f ? Argb(f) : 0,
            border is { } b ? Argb(b) : 0, border is null ? 0 : Math.Max(1, S(1)));

    private static void DrawText(nint hdc, string? text, RECT r, nint font, GlueRgb color, uint align)
    {
        if (string.IsNullOrEmpty(text) || r.Right <= r.Left) return;
        var old = Win32.SelectObject(hdc, font);
        Win32.SetTextColor(hdc, Ref(color));
        Win32.DrawTextW(hdc, text, text.Length, ref r, align | Win32.DT_SINGLELINE | Win32.DT_VCENTER | Win32.DT_NOPREFIX | Win32.DT_END_ELLIPSIS);
        Win32.SelectObject(hdc, old);
    }

    private void PaintInto(nint hdc, int width, int barH)
    {
        var text = Text;
        Fill(hdc, new RECT { Right = width, Bottom = barH }, Bg);
        Win32.SetBkMode(hdc, Win32.TRANSPARENT);

        if (_titleR.Right > _titleR.Left)
        {
            int x = _titleR.Left;
            if (_w.IconSmall != 0)
            {
                int sz = S(16);
                Win32.DrawIconEx(hdc, x, (TitleRowH - sz) / 2, _w.IconSmall, sz, sz, 0, 0, Win32.DI_NORMAL);
                x += sz + S(8);
            }
            DrawText(hdc, _w.TitleText, new RECT { Left = x, Top = 0, Right = _titleR.Right, Bottom = TitleRowH }, Font(12), text, Win32.DT_LEFT);
        }

        for (int i = 0; i < _menuR.Count; i++)
        {
            if (!_menuVisible[i]) continue;
            bool open = i == _menuOpen, down = i == _menuPress && i == _menuHover;
            if (open || down) Round(hdc, _menuR[i], Pressed, null);
            else if (i == _menuHover || (_menuKeyMode && _menuOpen < 0 && i == _menuFocus)) Round(hdc, _menuR[i], Hover, null);
            // El texto se dibuja con prefijos (el ampersand marca la tecla de acceso, que se subraya solo con Alt o en modo teclado).
            bool overflow = i >= _menuSpec!.Count;
            string raw = overflow ? "···" : _menuSpec[i].Text;
            var rr = _menuR[i];
            var oldFont = Win32.SelectObject(hdc, Font(12));
            Win32.SetTextColor(hdc, Ref(text));
            uint flags = Win32.DT_CENTER | Win32.DT_SINGLELINE | Win32.DT_VCENTER | (overflow ? Win32.DT_NOPREFIX : (_showMnemonics || _menuKeyMode) ? 0u : 0x00100000u /* DT_HIDEPREFIX */);
            Win32.DrawTextW(hdc, raw, raw.Length, ref rr, flags);
            Win32.SelectObject(hdc, oldFont);
        }

        foreach (var n in _nodes)
        {
            if (!n.Visible) continue;
            switch (n.Item.Type)
            {
                case GlueTitleBarItem.Button: PaintButton(hdc, n, text); break;
                case GlueTitleBarItem.Group:
                    if (Labels && !string.IsNullOrEmpty(n.Item.Text))
                        DrawText(hdc, n.Item.Text, new RECT { Left = n.R.Left - S(4), Top = n.R.Top + S(32), Right = n.R.Right + S(4), Bottom = n.R.Bottom },
                            Font(11), Muted, Win32.DT_CENTER);
                    break;
                case GlueTitleBarItem.Panel: PaintPanel(hdc, n, text); break;
                case GlueTitleBarItem.Search: PaintSearch(hdc, n); break;
                case GlueTitleBarItem.Image: PaintImage(hdc, n); break;
            }
        }

        PaintCaption(hdc, Cap.Min, _minR, text);
        PaintCaption(hdc, Cap.Max, _maxR, text);
        PaintCaption(hdc, Cap.Close, _closeR, text);
    }

    private GlueRgb? StateFill(Node n, int zone)
    {
        var it = n.Item;
        bool hot = it.Enabled && _hover == n && _hoverZone == zone;
        bool down = it.Enabled && _press == n && _pressZone == zone && hot;
        if (down) return Pressed;
        if (zone == ZoneBody && it.Toggle && it.Checked) return hot ? Mix(Fg, Bg, 0.15) : Checked;
        if (hot) return Hover;
        return null;
    }

    private void PaintButton(nint hdc, Node n, GlueRgb text)
    {
        var it = n.Item;
        var r = n.R;
        var color = it.Enabled ? text : Mix(text, Bg, 0.4);
        var body = it.Split ? new RECT { Left = r.Left, Top = r.Top, Right = n.Arrow.Left, Bottom = r.Bottom } : r;

        if (TitleMode)
        {
            if (StateFill(n, ZoneBody) is { } f) Fill(hdc, body, f);
            if (it.Split && StateFill(n, ZoneArrow) is { } fa) Fill(hdc, n.Arrow, fa);
        }
        else
        {
            if (StateFill(n, ZoneBody) is { } f) Round(hdc, body, f, null);
            if (it.Split && StateFill(n, ZoneArrow) is { } fa) Round(hdc, n.Arrow, fa, null);
            if (it.Split && _hover == n)
                Fill(hdc, new RECT { Left = n.Arrow.Left, Top = r.Top + S(6), Right = n.Arrow.Left + Math.Max(1, S(1)), Bottom = r.Bottom - S(6) }, ControlBorder);
        }

        int arrowW = HasMenu(it) && !it.Split ? S(12) : 0;
        var iconArea = new RECT { Left = body.Left, Top = body.Top, Right = body.Right - arrowW, Bottom = body.Bottom };
        if (Labels && n.Group is null)
        {
            DrawIcon(hdc, it, new RECT { Left = iconArea.Left, Top = r.Top + S(4), Right = iconArea.Right, Bottom = r.Top + S(28) }, S(20), color);
            DrawText(hdc, it.Text, new RECT { Left = r.Left + S(2), Top = r.Top + S(28), Right = body.Right - S(2), Bottom = r.Bottom - S(2) },
                Font(11), color, Win32.DT_CENTER);
        }
        else DrawIcon(hdc, it, iconArea, S(16), color);

        if (HasMenu(it))
        {
            var ar = it.Split ? n.Arrow : new RECT { Left = body.Right - S(14), Top = body.Top, Right = body.Right - S(2), Bottom = body.Bottom };
            DrawText(hdc, "", ar, Font(8, face: GlyphFace()), color, Win32.DT_CENTER);
        }
    }

    private void PaintPanel(nint hdc, Node n, GlueRgb text)
    {
        var it = n.Item;
        var r = n.R;
        bool clickable = it.Enabled && (it.Id is not null || HasMenu(it));
        var fill = clickable && _hover == n ? (_press == n ? Pressed : Mix(Fg, Bg, 0.1)) : ControlBg;
        Round(hdc, r, fill, ControlBorder, 6);
        int pad = S(10);
        int left = r.Left + pad, right = r.Right - pad - (HasMenu(it) ? S(14) : 0);
        var color = it.Enabled ? text : Mix(text, Bg, 0.4);
        if (!string.IsNullOrEmpty(it.Icon))
        {
            int isz = S(Labels ? 20 : 16);
            DrawIcon(hdc, it, new RECT { Left = left, Top = r.Top, Right = left + isz, Bottom = r.Bottom }, isz, color);
            left += isz + S(8);
        }
        if (string.IsNullOrEmpty(it.Subtitle))
            DrawText(hdc, it.Text, new RECT { Left = left, Top = r.Top, Right = right, Bottom = r.Bottom }, Font(12, bold: true), color, Win32.DT_CENTER);
        else
        {
            int mid = r.Top + (r.Bottom - r.Top) / 2;
            int lh = S(Labels ? 18 : 14);
            DrawText(hdc, it.Text, new RECT { Left = left, Top = mid - lh, Right = right, Bottom = mid + S(1) }, Font(Labels ? 12 : 11, bold: true), color, Win32.DT_CENTER);
            DrawText(hdc, it.Subtitle, new RECT { Left = left, Top = mid, Right = right, Bottom = mid + lh }, Font(11), Muted, Win32.DT_CENTER);
        }
        if (HasMenu(it))
            DrawText(hdc, "", new RECT { Left = r.Right - pad - S(14), Top = r.Top, Right = r.Right - pad + S(2), Bottom = r.Bottom },
                Font(8, face: GlyphFace()), color, Win32.DT_CENTER);
    }

    private void PaintSearch(nint hdc, Node n)
    {
        var r = n.R;
        Round(hdc, r, ControlBg, ControlBorder, 4);
        if (_editFocused)
            Fill(hdc, new RECT { Left = r.Left + S(4), Top = r.Bottom - S(2), Right = r.Right - S(4), Bottom = r.Bottom }, Accent);
        DrawText(hdc, "", new RECT { Left = r.Left + S(8), Top = r.Top, Right = r.Left + S(26), Bottom = r.Bottom },
            Font(12, face: GlyphFace()), Muted, Win32.DT_CENTER);
    }

    private void PaintImage(nint hdc, Node n)
    {
        var r = n.R;
        var img = Image(n.Item.Icon!, r.Right - r.Left, r.Bottom - r.Top, n.Item.Color ? null : Rgb(Text));
        if (img != 0) Gdip.Draw(hdc, img, r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
    }

    /// <summary>Icono centrado en <paramref name="area"/>: nombre de la lista, simbolo de la fuente propia o imagen.</summary>
    private void DrawIcon(nint hdc, GlueTitleBarItem it, RECT area, int size, GlueRgb color)
    {
        var icon = it.Icon;
        if (string.IsNullOrEmpty(icon)) return;
        int logical = (int)Math.Round(size * 96.0 / Dpi);
        if (GlueTitleBarIcons.Table.TryGetValue(icon, out var named))
        {
            DrawText(hdc, char.ConvertFromUtf32(named.Windows), area, Font(logical, face: GlyphFace()), color, Win32.DT_CENTER);
            return;
        }
        if (icon.StartsWith("font:", StringComparison.OrdinalIgnoreCase))
        {
            if (_iconFamily is not null && GlueTitleBarIcons.FontChar(icon) is { } ch)
                DrawText(hdc, ch, area, Font(logical, face: _iconFamily), color, Win32.DT_CENTER);
            return;
        }
        var img = Image(icon, size, size, it.Color ? null : Rgb(color));
        if (img != 0)
            Gdip.Draw(hdc, img, area.Left + (area.Right - area.Left - size) / 2, area.Top + (area.Bottom - area.Top - size) / 2, size, size);
    }

    private void PaintCaption(nint hdc, Cap cap, RECT r, GlueRgb text)
    {
        if (r.Right <= r.Left) return;
        bool hot = _hoverCap == cap, down = _pressCap == cap && hot;
        var glyphColor = text;
        if (cap == Cap.Close && (hot || down))
        {
            Fill(hdc, r, down ? new GlueRgb(0xc8, 0x3c, 0x31) : new GlueRgb(0xc4, 0x2b, 0x1c));
            glyphColor = new GlueRgb(0xff, 0xff, 0xff);
        }
        else if (down) Fill(hdc, r, Pressed);
        else if (hot) Fill(hdc, r, Hover);
        string glyph = cap switch
        {
            Cap.Min => "",
            Cap.Max => _w.IsMaximized ? "" : "",
            _ => "",
        };
        DrawText(hdc, glyph, r, Font(10, face: GlyphFace()), glyphColor, Win32.DT_CENTER);
    }

    // ------------------------------------------------------------- raton
    /// <summary>Resultado de <c>WM_NCHITTEST</c> para un punto de la franja (coordenadas de cliente).</summary>
    public int HitTest(int x, int y, bool resizable)
    {
        int barH = HeightPx;
        if (y >= barH) return Win32.HTCLIENT;
        EnsureLayoutForInput();
        if (resizable && !_w.IsMaximized)
        {
            int frame = Math.Min(Win32.GetSystemMetricsForDpi(Win32.SM_CYFRAME, Dpi) + Win32.GetSystemMetricsForDpi(Win32.SM_CXPADDEDBORDER, Dpi), barH / 4);
            if (y < frame)
            {
                Win32.GetClientRect(_w.Hwnd, out var c);
                if (x < frame * 2) return Win32.HTTOPLEFT;
                if (x >= c.Right - frame * 2) return Win32.HTTOPRIGHT;
                return Win32.HTTOP;
            }
        }
        if (MenuAt(x, y) >= 0) return Win32.HTCLIENT;
        // Icono de la ventana: como el menu del sistema de toda la vida (clic = menu del sistema, doble clic = cerrar).
        if (OnIcon(x, y)) return Win32.HTSYSMENU;
        if (In(_maxR, x, y)) return Win32.HTMAXBUTTON;
        if (In(_minR, x, y) || In(_closeR, x, y)) return Win32.HTCLIENT;
        return NodeAt(x, y, out _) is not null ? Win32.HTCLIENT : Win32.HTCAPTION;
    }

    private void EnsureLayoutForInput()
    {
        Win32.GetClientRect(_w.Hwnd, out var c);
        if (_layoutWidth == c.Right && _layoutDpi == Dpi) return;
        var dc = Win32.GetDC(_w.Hwnd);
        try { EnsureLayout(dc, c.Right); } finally { Win32.ReleaseDC(_w.Hwnd, dc); }
    }

    private static bool In(RECT r, int x, int y) => r.Right > r.Left && x >= r.Left && x < r.Right && y >= r.Top && y < r.Bottom;

    /// <summary>Elemento interactivo bajo el punto (boton, panel con accion, imagen con id, busqueda) y su zona.</summary>
    private Node? NodeAt(int x, int y, out int zone)
    {
        zone = ZoneBody;
        foreach (var n in _nodes)
        {
            if (!n.Visible || !In(n.R, x, y)) continue;
            var it = n.Item;
            switch (it.Type)
            {
                case GlueTitleBarItem.Button:
                    if (it.Split && In(n.Arrow, x, y)) zone = ZoneArrow;
                    return n;
                case GlueTitleBarItem.Panel when it.Id is not null || HasMenu(it):
                case GlueTitleBarItem.Image when it.Id is not null:
                case GlueTitleBarItem.Search:
                    return n;
            }
        }
        return null;
    }

    private bool OnIcon(int x, int y)
    {
        if (_w.IconSmall == 0 || _titleR.Right <= _titleR.Left) return false;
        var r = new RECT { Left = Math.Max(0, _titleR.Left - S(8)), Top = 0, Right = _titleR.Left + S(16) + S(4), Bottom = TitleRowH };
        return In(r, x, y);
    }

    private int MenuAt(int x, int y)
    {
        for (int i = 0; i < _menuR.Count; i++) if (_menuVisible[i] && In(_menuR[i], x, y)) return i;
        return -1;
    }

    private Cap CapAt(int x, int y) =>
        In(_closeR, x, y) ? Cap.Close : In(_maxR, x, y) ? Cap.Max : In(_minR, x, y) ? Cap.Min : Cap.None;

    public void OnMouseMove(int x, int y)
    {
        EnsureLayoutForInput();
        int zone = ZoneBody;
        var node = y < HeightPx ? NodeAt(x, y, out zone) : null;
        if (node?.Item.Type == GlueTitleBarItem.Search) node = null;
        int mh = y < HeightPx ? MenuAt(x, y) : -1;
        if (mh != _menuHover) { _menuHover = mh; RepaintStrip(); }
        var cap = y < HeightPx ? CapAt(x, y) : Cap.None;
        if (cap == Cap.Max) cap = Cap.None; // el de maximizar llega como no cliente
        SetHover(node, zone, cap);
        if (!_tracking)
        {
            var tme = new Win32.TRACKMOUSEEVENT { cbSize = (uint)sizeof(Win32.TRACKMOUSEEVENT), dwFlags = Win32.TME_LEAVE, hwndTrack = _w.Hwnd };
            _tracking = Win32.TrackMouseEvent(ref tme);
        }
    }

    public void OnMouseLeave()
    {
        _tracking = false;
        if (_menuHover >= 0) { _menuHover = -1; RepaintStrip(); }
        if (_hoverCap != Cap.Max) SetHover(null, ZoneBody, Cap.None);
    }

    public void OnNcMouseMove(int hit)
    {
        SetHover(null, ZoneBody, hit == Win32.HTMAXBUTTON ? Cap.Max : Cap.None);
        if (!_trackingNc)
        {
            var tme = new Win32.TRACKMOUSEEVENT
            {
                cbSize = (uint)sizeof(Win32.TRACKMOUSEEVENT), dwFlags = Win32.TME_LEAVE | Win32.TME_NONCLIENT, hwndTrack = _w.Hwnd,
            };
            _trackingNc = Win32.TrackMouseEvent(ref tme);
        }
    }

    public void OnNcMouseLeave()
    {
        _trackingNc = false;
        if (_hoverCap == Cap.Max) SetHover(null, ZoneBody, Cap.None);
        if (_pressCap == Cap.Max) _pressCap = Cap.None;
    }

    private void SetHover(Node? node, int zone, Cap cap)
    {
        if (node == _hover && zone == _hoverZone && cap == _hoverCap) return;
        _hover = node;
        _hoverZone = zone;
        _hoverCap = cap;
        RepaintStrip();
    }

    /// <summary>Boton izquierdo pulsado en la franja. Devuelve true si lo gestiona la barra.</summary>
    public bool OnLButtonDown(int x, int y)
    {
        if (y >= HeightPx) return false;
        EnsureLayoutForInput();
        int mi = MenuAt(x, y);
        if (mi >= 0)
        {
            ExitKeyMode();
            _menuPress = mi;
            Win32.SetCapture(_w.Hwnd);
            RepaintStrip();
            return true;
        }
        var node = NodeAt(x, y, out var zone);
        var cap = CapAt(x, y);
        if (node is null && cap == Cap.None) return false;
        if (node?.Item.Type == GlueTitleBarItem.Search) { Focus(node.Item.Id!); return true; }
        if (node is not null && !node.Item.Enabled) return true;
        _press = node;
        _pressZone = zone;
        _pressCap = cap;
        Win32.SetCapture(_w.Hwnd);
        RepaintStrip();
        return true;
    }

    public bool OnLButtonUp(int x, int y)
    {
        if (_menuPress >= 0)
        {
            int pressed = _menuPress;
            _menuPress = -1;
            if (Win32.GetCapture() == _w.Hwnd) Win32.ReleaseCapture();
            RepaintStrip();
            if (MenuAt(x, y) == pressed) OpenMenu(pressed);
            return true;
        }
        if (_press is null && _pressCap == Cap.None) return false;
        var press = _press;
        int pressZone = _pressZone;
        var pressCap = _pressCap;
        _press = null;
        _pressCap = Cap.None;
        if (Win32.GetCapture() == _w.Hwnd) Win32.ReleaseCapture();
        RepaintStrip();
        if (press is not null && NodeAt(x, y, out var zone) == press && zone == pressZone) Activate(press, zone);
        else if (pressCap != Cap.None && CapAt(x, y) == pressCap) RunCaption(pressCap);
        return true;
    }

    public void OnNcLButtonDown(int hit)
    {
        if (hit != Win32.HTMAXBUTTON) return;
        _pressCap = Cap.Max;
        RepaintStrip();
    }

    public void OnNcLButtonUp(int hit)
    {
        bool run = _pressCap == Cap.Max && hit == Win32.HTMAXBUTTON;
        _pressCap = Cap.None;
        RepaintStrip();
        if (run) RunCaption(Cap.Max);
    }

    public void OnCaptureLost()
    {
        if (_menuPress >= 0) { _menuPress = -1; RepaintStrip(); }
        if (_press is null && _pressCap == Cap.None) return;
        _press = null;
        _pressCap = Cap.None;
        RepaintStrip();
    }

    /// <summary>
    /// Abre el menu <paramref name="i"/> bajo su entrada (la entrada la pinta la barra; el desplegable es el menu nativo del sistema).
    /// Mientras esta abierto, el raton sobre otra entrada o las flechas izquierda/derecha cambian de menu (<see cref="WinMenu.IBarHook"/>).
    /// </summary>
    private void OpenMenu(int i, bool byKey = false)
    {
        while (true)
        {
            if (_menuSpec is null || i < 0 || i >= _menuR.Count) break;
            // El ultimo boton, si hay desbordamiento, reune los menus que no caben (cada uno como submenu).
            IReadOnlyList<GlueMenuItem>? items = i < _menuSpec.Count
                ? _menuSpec[i].Items
                : [.. _menuSpec.Where((_, k) => k < _menuVisible.Count && !_menuVisible[k])];
            if (items is not { Count: > 0 }) break;
            _menuOpen = i;
            _menuFocus = i; // el foco del teclado sigue al menu abierto (si no, quedaba sombreada la entrada de antes)
            _pendingSwitch = null;
            RepaintStrip();
            var p = new Win32.POINT { X = _menuR[i].Left, Y = _menuR[i].Bottom };
            Win32.ClientToScreen(_w.Hwnd, ref p);
            string? id = WinMenu.PopupAt(_w.Hwnd, p, items, this);
            _menuOpen = -1;
            RepaintStrip();
            if (_pendingSwitch is { } next) { _pendingSwitch = null; i = next; continue; }
            if (id is not null) { ExitKeyMode(); _w.RaiseMenu(id); }
            else if (byKey) { _menuKeyMode = true; _menuFocus = i; SetMnemonics(true); RepaintStrip(); NotifyMenuState(); } // Esc: vuelve a la barra resaltada
            else ExitKeyMode();
            break;
        }
    }

    // ------------------------------------------------------------- teclado del menu de ventana
    /// <summary>Indices de las entradas visibles (los menus que caben y, si lo hay, el boton de desbordamiento).</summary>
    private List<int> VisibleEntries()
    {
        var list = new List<int>();
        for (int i = 0; i < _menuR.Count; i++) if (_menuVisible[i]) list.Add(i);
        return list;
    }

    /// <summary>Tecla de acceso de un texto de menu (la letra tras un solo ampersand), en mayuscula; 0 si no tiene.</summary>
    internal static char Mnemonic(string text)
    {
        for (int i = 0; i < text.Length - 1; i++)
        {
            if (text[i] != (char)38) continue;
            if (text[i + 1] == (char)38) { i++; continue; }
            return char.ToUpperInvariant(text[i + 1]);
        }
        return (char)0;
    }

    private int MnemonicEntry(uint vk)
    {
        if (_menuSpec is null || !(vk is >= 0x30 and <= 0x39 or >= 0x41 and <= 0x5A)) return -1;
        char c = (char)vk;
        for (int i = 0; i < _menuSpec.Count && i < _menuVisible.Count; i++)
            if (_menuVisible[i] && Mnemonic(_menuSpec[i].Text) == c) return i;
        return -1;
    }

    private void SetMnemonics(bool on)
    {
        if (_showMnemonics == on) return;
        _showMnemonics = on;
        RepaintStrip();
    }

    private void EnterKeyMode()
    {
        var v = VisibleEntries();
        if (v.Count == 0) return;
        _menuKeyMode = true;
        _menuFocus = v[0];
        SetMnemonics(true);
        RepaintStrip();
        NotifyMenuState();
    }

    private void ExitKeyMode()
    {
        if (!_menuKeyMode && !_showMnemonics) return;
        _menuKeyMode = false;
        _menuFocus = -1;
        _altPending = false;
        SetMnemonics(false);
        RepaintStrip();
        NotifyMenuState();
    }

    /// <summary>
    /// Teclas que pasan por el webview antes que la pagina (<c>AcceleratorKeyPressed</c>): Alt suelto o F10 activan la barra de
    /// menus (flechas, Intro, Esc, letras de acceso), y Alt+letra abre el menu con esa tecla de acceso. Devuelve true si la tecla
    /// es del menu y no debe llegar a la pagina. <paramref name="kind"/>: 0 tecla, 1 tecla soltada, 2 tecla de sistema (con Alt), 3 soltada.
    /// </summary>
    internal bool OnAccelerator(int kind, uint vk, int lParam, string source = "webview")
    {
        if (Kind == MenuKind.None || _menuSpec is null) return false;
        if (DebugLog.Enabled) DebugLog.Event($"tecla ({source}) kind={kind} vk=0x{vk:X} modoTeclado={_menuKeyMode}");
        const uint VkMenu = 0x12, VkF10 = 0x79, VkEsc = 0x1B, VkLeft = 0x25, VkUp = 0x26, VkRight = 0x27, VkDown = 0x28, VkReturn = 0x0D, VkSpace = 0x20;
        bool down = kind is 0 or 2;
        bool repeat = down && (lParam & 0x40000000) != 0;

        if (vk == VkMenu)
        {
            if (down)
            {
                if (!repeat) { _altPending = true; SetMnemonics(true); }
                return false;
            }
            // Alt soltado sin haber pulsado otra tecla entre medias: activa (o cierra) la barra de menus.
            bool alone = _altPending;
            _altPending = false;
            if (alone)
            {
                if (_menuKeyMode) ExitKeyMode(); else EnterKeyMode();
                return true;
            }
            if (!_menuKeyMode) SetMnemonics(false);
            return false;
        }
        if (!down) return false;
        _altPending = false;

        if (vk == VkF10 && (Win32.GetKeyState(0x10) & 0x8000) == 0) // Mayus+F10 es el menu contextual
        {
            if (_menuKeyMode) ExitKeyMode(); else EnterKeyMode();
            return true;
        }

        if (_menuKeyMode)
        {
            var v = VisibleEntries();
            int at = v.IndexOf(_menuFocus);
            switch (vk)
            {
                case VkLeft: _menuFocus = v[(at - 1 + v.Count) % v.Count]; RepaintStrip(); return true;
                case VkRight: _menuFocus = v[(at + 1) % v.Count]; RepaintStrip(); return true;
                case VkDown or VkUp or VkReturn or VkSpace:
                {
                    int open = _menuFocus;
                    _w.PostUi(() => OpenMenu(open, true));
                    return true;
                }
                case VkEsc: ExitKeyMode(); return true;
            }
            int m = MnemonicEntry(vk);
            if (m >= 0) { _w.PostUi(() => OpenMenu(m, true)); return true; }
            ExitKeyMode(); // cualquier otra tecla sale de la barra y sigue su camino
            return false;
        }

        if (kind == 2) // Alt+letra
        {
            int m = MnemonicEntry(vk);
            if (m >= 0) { _w.PostUi(() => OpenMenu(m, true)); return true; }
        }
        return false;
    }

#if DEBUG
    /// <summary>Estado de la barra para el canal de control de depuracion: modo, alto, rectangulos (pixeles fisicos), menus y elementos.</summary>
    internal void DebugWrite(System.Text.Json.Utf8JsonWriter w)
    {
        EnsureLayoutForInput();
        static void Rect(System.Text.Json.Utf8JsonWriter w, string name, RECT r)
        {
            w.WriteStartObject(name); w.WriteNumber("x", r.Left); w.WriteNumber("y", r.Top); w.WriteNumber("width", r.Right - r.Left); w.WriteNumber("height", r.Bottom - r.Top); w.WriteEndObject();
        }
        w.WriteString("mode", _spec.Mode);
        w.WriteString("menuKind", Kind.ToString());
        w.WriteNumber("heightPx", HeightPx);
        w.WriteNumber("baseHeightPx", BaseHeightPx);
        Rect(w, "title", _titleR);
        Rect(w, "min", _minR); Rect(w, "max", _maxR); Rect(w, "close", _closeR);
        w.WriteBoolean("keyMode", _menuKeyMode);
        w.WriteBoolean("showMnemonics", _showMnemonics);
        w.WriteNumber("menuHover", _menuHover); w.WriteNumber("menuOpen", _menuOpen); w.WriteNumber("menuFocus", _menuFocus);
        w.WriteStartArray("menus");
        for (int i = 0; i < _menuR.Count; i++)
        {
            w.WriteStartObject();
            w.WriteString("text", _menuSpec is not null && i < _menuSpec.Count ? _menuSpec[i].Text : "···");
            w.WriteBoolean("visible", _menuVisible[i]);
            Rect(w, "rect", _menuR[i]);
            w.WriteEndObject();
        }
        w.WriteEndArray();
        w.WriteStartArray("items");
        foreach (var n in _nodes)
        {
            w.WriteStartObject();
            w.WriteString("type", n.Item.Type); w.WriteString("id", n.Item.Id); w.WriteBoolean("visible", n.Visible);
            Rect(w, "rect", n.R);
            w.WriteEndObject();
        }
        w.WriteEndArray();
    }
#endif

    // -- WinMenu.IBarHook: el menu abierto avisa de lo que pasa en la barra
    int WinMenu.IBarHook.Current => _menuOpen;

    int WinMenu.IBarHook.EntryAt(Win32.POINT screen)
    {
        Win32.ScreenToClient(_w.Hwnd, ref screen);
        return MenuAt(screen.X, screen.Y);
    }

    void WinMenu.IBarHook.SwitchTo(int entry, bool relative)
    {
        if (!relative) { _pendingSwitch = entry; return; }
        var v = VisibleEntries();
        int at = v.IndexOf(_menuOpen);
        if (v.Count == 0 || at < 0) return;
        _pendingSwitch = entry == -1 ? v[(at - 1 + v.Count) % v.Count] : v[(at + 1) % v.Count];
    }

    private void RunCaption(Cap cap)
    {
        switch (cap)
        {
            case Cap.Min: Win32.ShowWindow(_w.Hwnd, Win32.SW_SHOWMINIMIZED); break;
            case Cap.Max: Win32.ShowWindow(_w.Hwnd, _w.IsMaximized ? Win32.SW_RESTORE : Win32.SW_SHOWMAXIMIZED); break;
            case Cap.Close: Win32.PostMessageW(_w.Hwnd, Win32.WM_CLOSE, 0, 0); break;
        }
    }

    private void Raise(string type, string id, string? button = null, string? value = null) =>
        _w.Host.OnTitleBarEvent(new GlueTitleBarEvent(type, id, button, value));

    private void Activate(Node n, int zone)
    {
        var it = n.Item;
        bool menu = HasMenu(it) && (!it.Split || zone == ZoneArrow);
        if (menu) { ShowMenu(n, it.Menu!); return; }
        if (it.Id is { } id) Raise("click", id);
    }

    private void ShowMenu(Node n, List<GlueTitleBarMenuItem> menu)
    {
        var h = Win32.CreatePopupMenu();
        try
        {
            for (int i = 0; i < menu.Count; i++)
            {
                var m = menu[i];
                if (m.Type == "separator") Win32.AppendMenuW(h, Win32.MF_SEPARATOR, 0, null);
                else Win32.AppendMenuW(h, Win32.MF_STRING | (m.Enabled ? 0 : Win32.MF_GRAYED) | (m.Checked ? Win32.MF_CHECKED : 0), i + 1, m.Text);
            }
            var p = new Win32.POINT { X = n.R.Left, Y = n.R.Bottom };
            Win32.ClientToScreen(_w.Hwnd, ref p);
            _press = n;
            _pressZone = n.Item.Split ? ZoneArrow : ZoneBody;
            _hover = n;
            _hoverZone = _pressZone;
            RepaintStrip();
            int cmd = Win32.TrackPopupMenu(h, Win32.TPM_LEFTALIGN | Win32.TPM_TOPALIGN | Win32.TPM_RETURNCMD | Win32.TPM_NONOTIFY,
                p.X, p.Y, 0, _w.Hwnd, 0);
            _press = null;
            _hover = null;
            RepaintStrip();
            if (cmd > 0 && cmd <= menu.Count && menu[cmd - 1].Id is { } id) Raise("click", id, n.Item.Id);
        }
        finally { Win32.DestroyMenu(h); }
    }

    // ------------------------------------------------------------- campo de busqueda (EDIT nativo)
    private void UpdateSearch()
    {
        var node = _nodes.FirstOrDefault(n => n.Item.Type == GlueTitleBarItem.Search);
        if (node is null || node.Item.Id != _editId) DestroyEdit();
        if (node is null) return;
        var it = node.Item;
        if (_edit == 0)
        {
            _edit = Win32.CreateWindowExW(0, "EDIT", "", Win32.WS_CHILD | Win32.WS_TABSTOP | Win32.ES_AUTOHSCROLL,
                0, 0, 0, 0, _w.Hwnd, 0x7A01, 0, 0);
            if (_edit == 0) return;
            _editId = it.Id;
            _editText = null;
            Win32.SetWindowSubclass(_edit, &EditProc, 1, _w.Hwnd);
        }
        Win32.SendMessageW(_edit, Win32.WM_SETFONT, Font(12), 0);
        Win32.SendMessageText(_edit, Win32.EM_SETCUEBANNER, 1, it.Placeholder ?? "");
        if (it.Text != _editText)
        {
            _editText = it.Text;
            _suppressInput = true;
            Win32.SetWindowTextW(_edit, it.Text ?? "");
            _suppressInput = false;
        }
        int h = S(20);
        var r = node.R;
        Win32.MoveWindow(_edit, r.Left + S(30), r.Top + (r.Bottom - r.Top - h) / 2, Math.Max(0, r.Right - r.Left - S(38)), h, true);
        Win32.ShowWindow(_edit, node.Visible ? Win32.SW_SHOWNA : Win32.SW_HIDE);
    }

    private void DestroyEdit()
    {
        if (_edit == 0) return;
        Win32.RemoveWindowSubclass(_edit, &EditProc, 1);
        Win32.DestroyWindow(_edit);
        _edit = 0;
        _editId = null;
        _editFocused = false;
    }

    /// <summary>Da el foco al campo de busqueda (y selecciona su texto).</summary>
    public void Focus(string id)
    {
        EnsureLayoutForInput();
        if (_edit == 0 || _editId != id) return;
        Win32.SetFocus(_edit);
        Win32.SendMessageW(_edit, Win32.EM_SETSEL, 0, -1);
    }

    /// <summary><c>WM_COMMAND</c> del campo: cambios (se agrupan con un temporizador) y foco.</summary>
    public bool OnCommand(nint wParam, nint lParam)
    {
        if (_edit == 0 || lParam != _edit) return false;
        switch ((int)((wParam >> 16) & 0xFFFF))
        {
            case Win32.EN_CHANGE:
                if (!_suppressInput) Win32.SetTimer(_w.Hwnd, SearchTimerId, 250, 0);
                break;
            case Win32.EN_SETFOCUS: _editFocused = true; RepaintStrip(); break;
            case Win32.EN_KILLFOCUS: _editFocused = false; RepaintStrip(); break;
        }
        return true;
    }

    public void OnSearchTimer()
    {
        Win32.KillTimer(_w.Hwnd, SearchTimerId);
        if (_edit != 0 && _editId is { } id) Raise("input", id, null, Win32.GetText(_edit));
    }

    public void OnSearchSubmit()
    {
        if (_edit == 0 || _editId is not { } id) return;
        Win32.KillTimer(_w.Hwnd, SearchTimerId);
        Raise("submit", id, null, Win32.GetText(_edit));
    }

    /// <summary>Escape: borra el texto; si ya estaba vacio, devuelve el foco a la pagina.</summary>
    public void OnSearchEscape()
    {
        if (_edit == 0 || _editId is not { } id) return;
        if (Win32.GetText(_edit).Length > 0)
        {
            _suppressInput = true;
            Win32.SetWindowTextW(_edit, "");
            _suppressInput = false;
            Win32.KillTimer(_w.Hwnd, SearchTimerId);
            Raise("input", id, null, "");
        }
        else _w.FocusContent();
    }

    /// <summary><c>WM_CTLCOLOREDIT</c>: colores del campo segun el tema.</summary>
    public nint EditColors(nint hdc, nint control)
    {
        if (_edit == 0 || control != _edit) return 0;
        var bg = ControlBg;
        Win32.SetTextColor(hdc, Ref(Fg));
        Win32.SetBkColor(hdc, Ref(bg));
        if (_editBrush == 0 || _editBrushColor != Ref(bg))
        {
            if (_editBrush != 0) Win32.DeleteObject(_editBrush);
            _editBrush = Win32.CreateSolidBrush(Ref(bg));
            _editBrushColor = Ref(bg);
        }
        return _editBrush;
    }

    [UnmanagedCallersOnly]
    private static nint EditProc(nint hwnd, uint msg, nint wParam, nint lParam, nint id, nint parent)
    {
        if (msg == Win32.WM_KEYDOWN && wParam == Win32.VK_RETURN) { Win32.PostMessageW(parent, MsgSearchSubmit, 0, 0); return 0; }
        if (msg == Win32.WM_KEYDOWN && wParam == Win32.VK_ESCAPE) { Win32.PostMessageW(parent, MsgSearchEscape, 0, 0); return 0; }
        if (msg == Win32.WM_CHAR && (wParam == '\r' || wParam == 27)) return 0; // sin pitido
        return Win32.DefSubclassProc(hwnd, msg, wParam, lParam);
    }

    // ------------------------------------------------------------- ayudas emergentes
    private static bool _commonControls;

    private string? TipFor(Node n)
    {
        var it = n.Item;
        if (it.Tooltip is { Length: > 0 } t) return t;
        if (it.Type == GlueTitleBarItem.Button && (!Labels || n.Group is not null)) return it.Text;
        return null;
    }

    private void RebuildTooltips()
    {
        var withTips = _nodes.Where(n => n.Visible && n.Item.Type != GlueTitleBarItem.Search && TipFor(n) is not null).ToList();
        if (_tip == 0 && withTips.Count == 0) return;
        if (_tip == 0)
        {
            if (!_commonControls)
            {
                var icc = new Win32.INITCOMMONCONTROLSEX { dwSize = (uint)sizeof(Win32.INITCOMMONCONTROLSEX), dwICC = Win32.ICC_WIN95_CLASSES };
                _commonControls = Win32.InitCommonControlsEx(ref icc);
            }
            _tip = Win32.CreateWindowExW(Win32.WS_EX_TOPMOST, "tooltips_class32", null, 0x80000000 | Win32.TTS_ALWAYSTIP | Win32.TTS_NOPREFIX,
                Win32.CW_USEDEFAULT, Win32.CW_USEDEFAULT, Win32.CW_USEDEFAULT, Win32.CW_USEDEFAULT, _w.Hwnd, 0, 0, 0);
            if (_tip == 0) return;
            Win32.SendMessageW(_tip, Win32.TTM_SETMAXTIPWIDTH, 0, S(320));
        }
        for (int i = 0; i < _tipCount; i++)
        {
            var ti = new Win32.TOOLINFOW { cbSize = (uint)sizeof(Win32.TOOLINFOW), hwnd = _w.Hwnd, uId = i + 1 };
            Win32.SendMessageW(_tip, Win32.TTM_DELTOOLW, 0, (nint)(&ti));
        }
        FreeTipTexts();
        _tipCount = 0;
        foreach (var n in withTips)
        {
            var text = Marshal.StringToHGlobalUni(TipFor(n));
            _tipTexts.Add(text);
            var ti = new Win32.TOOLINFOW
            {
                cbSize = (uint)sizeof(Win32.TOOLINFOW), uFlags = Win32.TTF_SUBCLASS, hwnd = _w.Hwnd, uId = ++_tipCount,
                rect = n.R, lpszText = text,
            };
            Win32.SendMessageW(_tip, Win32.TTM_ADDTOOLW, 0, (nint)(&ti));
        }
    }

    private void FreeTipTexts()
    {
        foreach (var t in _tipTexts) Marshal.FreeHGlobal(t);
        _tipTexts.Clear();
    }

    private bool _disposed;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Win32.KillTimer(_w.Hwnd, SearchTimerId);
        DestroyEdit();
        if (_editBrush != 0) { Win32.DeleteObject(_editBrush); _editBrush = 0; }
        if (_tip != 0) { Win32.DestroyWindow(_tip); _tip = 0; }
        FreeTipTexts();
        FreeFonts();
        FreeImages();
        foreach (var s in _sources.Values) if (s != 0) Gdip.Dispose(s);
        _sources.Clear();
        if (_iconFontHandle != 0) { Win32.RemoveFontMemResourceEx(_iconFontHandle); _iconFontHandle = 0; }
    }
}
