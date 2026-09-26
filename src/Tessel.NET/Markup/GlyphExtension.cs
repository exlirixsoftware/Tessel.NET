using System;
using System.Windows.Markup;

namespace Tessel.NET.Markup;

/// <summary>
/// Returns the glyph string of a <see cref="Symbol"/>:
/// <c>tessel:ControlHelper.Icon="{tessel:Glyph Save}"</c>.
/// </summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class GlyphExtension : MarkupExtension
{
    public GlyphExtension() { }

    public GlyphExtension(Symbol symbol) => Symbol = symbol;

    [ConstructorArgument("symbol")]
    public Symbol Symbol { get; set; }

    public override object ProvideValue(IServiceProvider serviceProvider) => ToGlyph(Symbol);

    public static string ToGlyph(Symbol symbol) => char.ConvertFromUtf32((int)symbol);
}
