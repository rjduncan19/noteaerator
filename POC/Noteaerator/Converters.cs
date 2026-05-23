using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Noteaerator;

/// <summary>
/// Converts a tree depth (int) to a left-margin Thickness for nested
/// file-list rows. Used by the grouped file list to indent children.
/// </summary>
internal sealed class DepthToIndentConverter : IValueConverter
{
    public const double IndentPerLevel = 14.0;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var depth = value is int d ? d : 0;
        return new Thickness(depth * IndentPerLevel, 0, 0, 0);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Converts a tree depth (int) to the full width of the chevron hit-target
/// column: depth-indent + chevron glyph + comfortable gutter. This lets a
/// click anywhere in the indented "left gutter" of a row toggle expand /
/// collapse, instead of forcing pixel-perfect aim on the chevron glyph.
/// </summary>
internal sealed class DepthToChevronHitWidthConverter : IValueConverter
{
    public const double ChevronGlyphWidth = 16.0;
    public const double GutterRight = 6.0;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var depth = value is int d ? d : 0;
        return depth * DepthToIndentConverter.IndentPerLevel + ChevronGlyphWidth + GutterRight;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
