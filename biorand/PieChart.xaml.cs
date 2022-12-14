using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace IntelOrca.Biohazard.BioRand
{
    /// <summary>
    /// Interaction logic for PieChart.xaml
    /// </summary>
    public partial class PieChart : UserControl
    {
        public List<Record> Records { get; } = new List<Record>();
        public ChartKind Kind { get; set; }

        public PieChart()
        {
            InitializeComponent();
        }

        public void Update()
        {
            if (Kind == ChartKind.Pie)
                UpdatePie();
            else
                UpdateBar();
        }

        public void UpdatePie()
        {
            viewBox.Visibility = Visibility.Visible;
            horizontalGrid.Visibility = Visibility.Collapsed;

            var gridItems = new List<UIElement>();
            var records = Records.Where(x => x.Value != 0).ToArray();
            var radius = 50;
            var total = records.Sum(x => x.Value);
            if (records.Length == 1)
            {
                var record = records[0];

                var circle = new Ellipse();
                circle.Fill = new SolidColorBrush(record.Color);
                circle.Width = radius * 2;
                circle.Height = radius * 2;
                gridItems.Add(circle);
                gridItems.Add(CreatePieLabel(record, new Point()));
            }
            else
            {
                var angle = 0.0;
                var centre = new Point(radius, radius);
                var firstPiePoint = new Point(radius, 0);
                var piePoint = firstPiePoint;
                for (int i = 0; i < records.Length; i++)
                {
                    var record = records[i];
                    var angleLength = Math.PI * 2 * (record.Value / total);
                    var angleEnd = angle + angleLength;
                    var nextPiePoint = i == records.Length - 1 ?
                        firstPiePoint :
                        new Point(
                            radius + (Math.Sin(angleEnd) * radius),
                            radius - (Math.Cos(angleEnd) * radius));

                    var textAngle = angle + (angleLength / 2);
                    var textRadius = radius * 1.4;
                    var textPosition = new Point(
                        radius + (Math.Sin(textAngle) * textRadius),
                        radius - (Math.Cos(textAngle) * textRadius));

                    var pathFigure = new PathFigure();
                    pathFigure.StartPoint = centre;
                    pathFigure.IsClosed = true;
                    pathFigure.Segments.Add(new LineSegment(piePoint, false));
                    if (angleLength >= Math.PI)
                        pathFigure.Segments.Add(new ArcSegment(nextPiePoint, new Size(radius, radius), 0, true, SweepDirection.Clockwise, false));
                    else
                        pathFigure.Segments.Add(new ArcSegment(nextPiePoint, new Size(radius, radius), 0, false, SweepDirection.Clockwise, false));
                    var pathGeometry = new PathGeometry();
                    pathGeometry.Figures.Add(pathFigure);
                    var path = new Path();
                    path.Fill = new SolidColorBrush(record.Color);
                    path.Data = pathGeometry;
                    gridItems.Add(path);
                    gridItems.Add(CreatePieLabel(record, new Point(textPosition.X - radius, textPosition.Y - radius)));

                    angle = angleEnd;
                    piePoint = nextPiePoint;
                }
            }
            grid.Children.Clear();
            foreach (var item in gridItems.OrderBy(x => x is Path ? 0 : 1))
            {
                grid.Children.Add(item);
            }
        }

        public void UpdateBar()
        {
            viewBox.Visibility = Visibility.Collapsed;
            horizontalGrid.Visibility = Visibility.Visible;

            horizontalGrid.ColumnDefinitions.Clear();
            horizontalGrid.Children.Clear();

            var index = 0;
            foreach (var record in Records)
            {
                if (record.Value == 0)
                    continue;

                horizontalGrid.ColumnDefinitions.Add(new ColumnDefinition()
                {
                    Width = new GridLength(record.Value, GridUnitType.Star)
                });

                var border = new Border();
                border.Background = new SolidColorBrush(record.Color);

                var textBlock = new TextBlock();
                textBlock.Background = new SolidColorBrush(record.Color);
                textBlock.Foreground = GetLuma(record.Color) >= 0.5 ?
                    new SolidColorBrush(Colors.Black) :
                    new SolidColorBrush(Colors.White);
                textBlock.Text = record.Name;
                textBlock.TextAlignment = TextAlignment.Center;
                textBlock.ToolTip = record.Name;
                textBlock.Padding = new Thickness(2, 0, 2, 0);
                textBlock.TextTrimming = TextTrimming.CharacterEllipsis;

                border.Child = textBlock;
                Grid.SetColumn(border, index);
                horizontalGrid.Children.Add(border);
                index++;
            }
        }

        private static TextBlock CreatePieLabel(Record record, Point position)
        {
            var textBlock = new TextBlock();
            textBlock.Text = record.Name;
            textBlock.Foreground = GetLuma(record.Color) >= 0.5 ?
                new SolidColorBrush(Colors.Black) :
                new SolidColorBrush(Colors.White);
            textBlock.Margin = new Thickness(position.X, position.Y, 0, 0);
            textBlock.TextAlignment = TextAlignment.Center;
            textBlock.Height = 12;
            textBlock.FontSize = 8;
            return textBlock;
        }

        private static double GetLuma(Color color)
        {
            return ((color.R / 255.0) * 0.2126) + ((color.G / 255.0) * 0.7152) + ((color.B / 255.0) * 0.0722);
        }

        public class Record
        {
            public string Name { get; set; }
            public double Value { get; set; }
            public Color Color { get; set; }
        }

        public enum ChartKind
        {
            Pie,
            Bar
        }
    }
}
