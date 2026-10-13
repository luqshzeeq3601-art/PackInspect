using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PackInspect.Core.Devices;

namespace PackInspect.App.Converters;

/// <summary>
/// Turns the camera's raw grey pixels into something WPF can draw. It lives in
/// the view layer so that view models never touch WPF imaging types.
/// </summary>
public sealed class CapturedImageConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not CapturedImage image)
        {
            return null;
        }

        var bitmap = BitmapSource.Create(
            image.Width, image.Height, 96, 96, PixelFormats.Gray8, palette: null, image.Pixels.ToArray(), stride: image.Width);
        bitmap.Freeze();
        return bitmap;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
