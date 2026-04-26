// AeroDial — ThemeModel.cs
// Theme data model. Every visual property of the overlay is here.
// Themes are stored as JSON files in the /themes folder.

using SkiaSharp;

namespace AeroDial.Themes;

public sealed class AeroTheme
{
    // ── Identity ──────────────────────────────────────────────────────────
    public string Name        { get; set; } = "Custom";
    public string Description { get; set; } = "";
    public bool   IsBuiltIn   { get; set; } = false;

    // ── Background dim ────────────────────────────────────────────────────
    /// <summary>ARGB hex color of the full-screen dim overlay behind the ring.</summary>
    public string DimColor    { get; set; } = "#66000000";

    // ── Ring (flat fill — used as fallback when gradient properties are empty) ────
    public string SliceFill        { get; set; } = "#CC1A1A2E";
    public string SliceFillHover   { get; set; } = "#CC2D2D50";
    public string SliceStroke      { get; set; } = "#44FFFFFF";
    public string SliceStrokeHover { get; set; } = "#AA7C6EF7";
    public float  SliceStrokeWidth { get; set; } = 0.8f;
    public float  SliceCornerBlend { get; set; } = 0f; // 0=sharp, 1=pill (future)

    // ── Gradient (radial fill — leave empty to fall back to flat SliceFill) ──
    /// <summary>Slice color at the inner radius. Darker = more depth.</summary>
    public string SliceGradientInner      { get; set; } = "";
    /// <summary>Slice color at the outer radius. Slightly lighter than inner.</summary>
    public string SliceGradientOuter      { get; set; } = "";
    /// <summary>Hovered inner color.</summary>
    public string SliceGradientInnerHover { get; set; } = "";
    /// <summary>Hovered outer color.</summary>
    public string SliceGradientOuterHover { get; set; } = "";

    // ── Glow ─────────────────────────────────────────────────────────────────────
    /// <summary>Color of the blurred outer glow on the hovered slice. Falls back to AccentColor@40% if empty.</summary>
    public string GlowColor               { get; set; } = "";

    // ── Center circle ─────────────────────────────────────────────────────
    public string CenterFill       { get; set; } = "#CC111122";
    public string CenterStroke     { get; set; } = "#33FFFFFF";

    // ── Icon & label ──────────────────────────────────────────────────────
    public string IconTint         { get; set; } = "#CCFFFFFF";
    public string IconTintHover    { get; set; } = "#FFFFFFFF";
    /// <summary>
    /// Multiplier applied to built-in icon stroke widths. 1.0 = original, 1.5 = 50% thicker.
    /// Has no effect on raster icons (.exe, .png, etc.) — those are always drawn at full size.
    /// </summary>
    public float  IconStrokeScale  { get; set; } = 1.0f;
    public string LabelColor       { get; set; } = "#AAFFFFFF";
    public string LabelColorHover  { get; set; } = "#FFFFFFFF";
    public float  LabelFontSize    { get; set; } = 11f;
    public string LabelFontFamily  { get; set; } = "Segoe UI Variable";

    // ── Breadcrumb ────────────────────────────────────────────────────────
    public string BreadcrumbFill   { get; set; } = "#BB111122";
    public string BreadcrumbText   { get; set; } = "#AAFFFFFF";

    // ── Volume ring ───────────────────────────────────────────────────────
    /// <summary>Stroke width of the volume level arc drawn just outside the ring. Default 3.0f.</summary>
    public float VolumeRingThickness { get; set; } = 3.0f;

    // ── Ring border ───────────────────────────────────────────────────────
    /// <summary>
    /// Color for the explicit circular border lines drawn around child (L2/L3) rings.
    /// Leave empty to fall back to SliceStroke.
    /// </summary>
    public string RingBorderColor { get; set; } = "";

    // ── Accent (submenu indicator arrow, active dot, etc.) ───────────────
    public string AccentColor      { get; set; } = "#FF7C6EF7";

    // ── Helpers ───────────────────────────────────────────────────────────

    public SKColor ToSKColor(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length == 6) hex = "FF" + hex;
        if (!uint.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out var argb))
            return SKColors.White;
        return new SKColor(
            red:   (byte)((argb >> 16) & 0xFF),
            green: (byte)((argb >>  8) & 0xFF),
            blue:  (byte)( argb        & 0xFF),
            alpha: (byte)((argb >> 24) & 0xFF));
    }

    public SKPaint MakePaint(string hexColor, SKPaintStyle style = SKPaintStyle.Fill)
        => new() { Color = ToSKColor(hexColor), Style = style, IsAntialias = true };
}
