using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace GestionCarp.App.Converters;

/// <summary>true → Visible, false → Collapsed. Con Invert="True" al revés.</summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => (value is true) ^ Invert ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => (value is Visibility.Visible) ^ Invert;
}
