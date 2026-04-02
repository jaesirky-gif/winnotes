using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WinNotes.Client.Converters;

public sealed class NullToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var hasValue = value is not null;
        if (value is string text)
        {
            hasValue = !string.IsNullOrWhiteSpace(text);
        }

        if (Invert)
        {
            hasValue = !hasValue;
        }

        return hasValue ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}
