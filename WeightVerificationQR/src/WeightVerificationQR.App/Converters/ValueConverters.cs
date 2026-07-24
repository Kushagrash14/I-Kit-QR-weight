using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using WeightVerificationQR.Core.Models;

namespace WeightVerificationQR.App.Converters;

public class ConnectionStatusToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture) => value switch
    {
        ConnectionStatus.Connected => new SolidColorBrush(Color.FromRgb(0x1E, 0x8E, 0x3E)),
        ConnectionStatus.Connecting => new SolidColorBrush(Color.FromRgb(0xF2, 0xA9, 0x00)),
        _ => new SolidColorBrush(Color.FromRgb(0xD6, 0x34, 0x2A))
    };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public class WeighResultToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture) => value switch
    {
        WeighResult.Pass => new SolidColorBrush(Color.FromRgb(0x1E, 0x8E, 0x3E)),
        WeighResult.Fail => new SolidColorBrush(Color.FromRgb(0xD6, 0x34, 0x2A)),
        _ => new SolidColorBrush(Color.FromRgb(0x5B, 0x6B, 0x76))
    };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        var flip = parameter?.ToString() == "Invert";
        var b = value is bool bb && bb;
        if (flip) b = !b;
        return b ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture) =>
        value is null ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
