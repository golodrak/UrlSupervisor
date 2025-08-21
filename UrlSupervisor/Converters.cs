using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace UrlSupervisor
{
    public class BoolToBrushConverter : IValueConverter
    {
        private static readonly SolidColorBrush Green;
        private static readonly SolidColorBrush Red;

        static BoolToBrushConverter()
        {
            Green = new SolidColorBrush(Color.FromRgb(0x30, 0xD4, 0xC1));
            Green.Freeze();
            Red = new SolidColorBrush(Color.FromRgb(0xFF, 0x5A, 0x5A));
            Red.Freeze();
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool b = value is bool v && v;
            bool invert = false;
            if (parameter is bool pb)
            {
                invert = pb;
            }
            else if (parameter is string ps && bool.TryParse(ps, out var parsed))
            {
                invert = parsed;
            }
            if (invert)
            {
                b = !b;
            }
            return b ? Green : Red;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool b = value is bool v && v;
            return b ? Visibility.Visible : Visibility.Collapsed;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class RunningToGlyphConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool running = value is bool b && b;
            return running ? "" : "";
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public static class ThemeManager
    {
        public static void UseLight() => SwapTheme("pack://application:,,,/Themes/Colors.Light.xaml");
        public static void UseDark() => SwapTheme("pack://application:,,,/Themes/Colors.Dark.xaml");

        private static void SwapTheme(string uri)
        {
            var app = System.Windows.Application.Current;
            if (app == null) return;
            for (int i = 0; i < app.Resources.MergedDictionaries.Count; i++)
            {
                var md = app.Resources.MergedDictionaries[i];
                if (md.Source != null && md.Source.OriginalString.Contains("/Themes/Colors."))
                {
                    app.Resources.MergedDictionaries.RemoveAt(i);
                    break;
                }
            }
            app.Resources.MergedDictionaries.Insert(0, new ResourceDictionary { Source = new Uri(uri, UriKind.RelativeOrAbsolute) });
        }
    }
}
