using System;
using System.Collections.Generic;
using System.Diagnostics;
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
            topGrid.Visibility = Visibility.Collapsed;

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
                circle.ToolTip = GetRecordToolTip(record, total);
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
                    path.ToolTip = GetRecordToolTip(record, total);
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
            topGrid.Visibility = Visibility.Visible;
            topGrid.RowDefinitions.Clear();
            topGrid.Children.Clear();

            var records = Records.OrderByDescending(x => x.Value).ToArray();
            var split = DivideRecords(records);

            var row = 0;
            foreach (var s in split)
            {
                topGrid.RowDefinitions.Add(new RowDefinition()
                {
                    Height = new GridLength(18, GridUnitType.Pixel)
                });

                var g = CreateHorizontalBar(s);
                Grid.SetRow(g, row);
                topGrid.Children.Add(g);
                row++;
            }
        }

        private Grid CreateHorizontalBar(Record[] records)
        {
            var grid = new Grid();
            var index = 0;
            var total = Records.Sum(x => x.Value);
            foreach (var record in records)
            {
                if (record.Value == 0)
                    continue;

                grid.ColumnDefinitions.Add(new ColumnDefinition()
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
                textBlock.ToolTip = GetRecordToolTip(record, total);
                textBlock.Padding = new Thickness(2, 0, 2, 0);
                textBlock.TextTrimming = TextTrimming.CharacterEllipsis;

                border.Child = textBlock;
                Grid.SetColumn(border, index);
                grid.Children.Add(border);
                index++;
            }
            return grid;
        }

        private Record[][] DivideRecords(Record[] records)
        {
            if (records.Length <= 3)
                return new[] { records };

            records = records.OrderByDescending(x => x.Value).ToArray();
            var totalSize = records.Sum(x => x.Value);
            var halfSize = totalSize / 2;
            var left = FindOptimal((int)halfSize, records, out var splitLength);
            if (left == null)
            {
                return new[] { records };
            }
            else
            {
                if (splitLength < halfSize)
                {
                    left = left.Append(new Record()
                    {
                        Name = "",
                        Value = halfSize - splitLength,
                        Color = Colors.Transparent
                    }).ToArray();
                }
                var right = records.Except(left).ToArray();
                var rightLength = right.Sum(x => (int)x.Value);
                if (rightLength < halfSize)
                {
                    right = right.Append(new Record()
                    {
                        Name = "",
                        Value = halfSize - rightLength,
                        Color = Colors.Transparent
                    }).ToArray();
                }
                return new[] { right, left };
            }
        }

        private Record[] FindOptimal(int length, Record[] records, out int optimalLength)
        {
            Record[] optimal = null;
            optimalLength = 0;
            for (var i = 0; i < records.Length && optimalLength != length; i++)
            {
                var currentLength = (int)records[i].Value;
                var remaining = length - currentLength;
                if (remaining == 0)
                {
                    optimal = new[] { records[i] };
                    optimalLength = length;
                }
                else if (remaining > 0)
                {
                    var rest = FindOptimal(remaining, records.Take(i).Concat(records.Skip(i + 1)).ToArray(), out var l);
                    if (rest != null)
                    {
                        var total = currentLength + l;
                        if (total > optimalLength)
                        {
                            optimal = new[] { records[i] }.Concat(rest).ToArray();
                            optimalLength = total;
                        }
                    }
                    else
                    {
                        optimal = new[] { records[i] };
                        optimalLength = currentLength;
                    }
                }
            }
            return optimal;
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
            textBlock.IsHitTestVisible = false;
            return textBlock;
        }

        private static double GetLuma(Color color)
        {
            return ((color.R / 255.0) * 0.2126) + ((color.G / 255.0) * 0.7152) + ((color.B / 255.0) * 0.0722);
        }

        private static object GetRecordToolTip(Record record, double total)
        {
            if (record.Name == String.Empty)
                return null; // Ignore padding records.
            double p = record.Value / total * 100;
            return $"{record.Name}: {Math.Round(p, 2)}%";
        }

        [DebuggerDisplay("({Name}, {Value})")]
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
