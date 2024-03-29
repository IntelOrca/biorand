﻿using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace IntelOrca.Biohazard.BioRand
{
    /// <summary>
    /// Interaction logic for RandoSlider.xaml
    /// </summary>
    public partial class RandoSlider : UserControl
    {
        public static readonly DependencyProperty HeadingProperty =
           DependencyProperty.Register(nameof(Heading), typeof(string), typeof(RandoSlider), new PropertyMetadata("Heading", OnHeadingChanged));

        public static readonly DependencyProperty LowTextProperty =
            DependencyProperty.Register(nameof(LowText), typeof(string), typeof(RandoSlider), new PropertyMetadata("Low"));

        public static readonly DependencyProperty HighTextProperty =
            DependencyProperty.Register(nameof(HighText), typeof(string), typeof(RandoSlider), new PropertyMetadata("High"));

        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(nameof(Value), typeof(double), typeof(RandoSlider));

        public static readonly DependencyProperty MaximumProperty =
            DependencyProperty.Register(nameof(Maximum), typeof(double), typeof(RandoSlider));

        public event RoutedPropertyChangedEventHandler<double> ValueChanged;

        public string Heading
        {
            get => (string)GetValue(HeadingProperty);
            set => SetValue(HeadingProperty, value);
        }

        public string LowText
        {
            get => (string)GetValue(LowTextProperty);
            set => SetValue(LowTextProperty, value);
        }

        public string HighText
        {
            get => (string)GetValue(HighTextProperty);
            set => SetValue(HighTextProperty, value);
        }

        public double Value
        {
            get => (double)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public double Maximum
        {
            get => (double)GetValue(MaximumProperty);
            set => SetValue(MaximumProperty, value);
        }

        public RandoSlider()
        {
            InitializeComponent();
        }

        private static void OnHeadingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is RandoSlider slider)
            {
                slider.headingLabel.Visibility = string.IsNullOrEmpty((string)e.NewValue) ?
                    Visibility.Collapsed :
                    Visibility.Visible;
            }
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            ValueChanged?.Invoke(this, e);
        }
    }
}
