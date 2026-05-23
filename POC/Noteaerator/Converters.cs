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
