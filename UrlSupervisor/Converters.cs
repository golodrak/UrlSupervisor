using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace UrlSupervisor
{
    public class BoolToBrushConverter : IValueConverter
    {
        private static readonly SolidColorBrush GreenBrush;
        private static readonly SolidColorBrush RedBrush;

        static BoolToBrushConverter()
        {
            GreenBrush = new SolidColorBrush(Color.FromRgb(0x30, 0xD4, 0xC1));
            GreenBrush.Freeze();
            RedBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x5A, 0x5A));
            RedBrush.Freeze();
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool result = value is bool b && b;

            bool invert = false;
            if (parameter is bool boolParam)
            {
                invert = boolParam;
            }
            else if (parameter is string strParam && bool.TryParse(strParam, out var parsed))
            {
                invert = parsed;
            }

            if (invert)
            {
                result = !result;
            }

            return result ? GreenBrush : RedBrush;
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

    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value == null ? Visibility.Collapsed : Visibility.Visible;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
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
