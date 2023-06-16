using System;
using System.Windows;

namespace emdui.Extensions
{
    internal static class ExceptionExtensions
    {
        public static void ShowMessageBox(this Exception exception, FrameworkElement parent)
        {
            while (parent != null && !(parent is Window))
            {
                parent = parent.Parent as FrameworkElement;
            }
            var window = parent as Window;

            var title = "emdui";
            var message = exception.Message;
            MessageBox.Show(window, message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
