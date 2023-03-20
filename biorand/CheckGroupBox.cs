using System;
using System.Windows;
using System.Windows.Controls;

namespace IntelOrca.Biohazard.BioRand
{
    public class CheckGroupBox : HeaderedContentControl
    {
        public EventHandler<RoutedEventArgs> Unchecked;
        public EventHandler<RoutedEventArgs> Checked;
        public event EventHandler OnCheckedChanged;

        public static readonly DependencyProperty ActualHeaderProperty =
            DependencyProperty.Register(nameof(ActualHeader), typeof(string), typeof(CheckGroupBox), new PropertyMetadata());

        public static readonly DependencyProperty IsCheckedProperty =
            DependencyProperty.Register(nameof(IsChecked), typeof(bool?), typeof(CheckGroupBox), new PropertyMetadata(false, IsCheckedChanged));

        public static readonly DependencyProperty IsChildrenEnabledProperty =
            DependencyProperty.Register(nameof(IsChildrenEnabled), typeof(bool), typeof(CheckGroupBox), new PropertyMetadata(false));

        public string ActualHeader
        {
            get => (string)GetValue(ActualHeaderProperty);
            set => SetValue(ActualHeaderProperty, value);
        }

        public bool IsChildrenEnabled
        {
            get => (bool)GetValue(IsChildrenEnabledProperty);
            set => SetValue(IsChildrenEnabledProperty, value);
        }

        public bool? IsChecked
        {
            get => (bool?)GetValue(IsCheckedProperty);
            set => SetValue(IsCheckedProperty, value);
        }

        private static void IsCheckedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is CheckGroupBox instance)
            {
                var newValue = (bool?)e.NewValue;
                instance.IsChildrenEnabled = newValue == true;
                if (newValue == true)
                    instance.Checked?.Invoke(instance, new RoutedEventArgs());
                if (newValue == false)
                    instance.Unchecked?.Invoke(instance, new RoutedEventArgs());
                if(instance.OnCheckedChanged != null)
                    instance.OnCheckedChanged(instance, new RoutedEventArgs());
            }
        }

        protected override void OnHeaderChanged(object oldHeader, object newHeader)
        {
            base.OnHeaderChanged(oldHeader, newHeader);
            ActualHeader = "      " + newHeader;
        }
    }
}
