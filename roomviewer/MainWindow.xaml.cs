using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace IntelOrca.Biohazard.RoomViewer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string _cutsceneJsonPath = @"M:\git\rer\IntelOrca.Biohazard\data\re2\cutscene.json";
        private string _enemyJsonPath = @"M:\git\rer\IntelOrca.Biohazard\data\re2\enemy.json";

        private readonly Dictionary<RdtId, CutsceneRoomInfo> _cutsceneRoomInfoMap = new Dictionary<RdtId, CutsceneRoomInfo>();

        public MainWindow()
        {
            InitializeComponent();
            Load();
        }

        private void Load()
        {
            LoadCutsceneRoomInfo();

            roomDropdown.ItemsSource = _cutsceneRoomInfoMap.Keys.ToArray();
            roomDropdown.SelectedIndex = 0;
        }

        private void LoadMap(RdtId id)
        {
            canvas.Children.Clear();

            var info = _cutsceneRoomInfoMap[id];
            if (info.Poi != null)
            {
                var origin = GetOrigin(info);
                foreach (var poi in info.Poi)
                {
                    if (poi.X == 0 && poi.Z == 0)
                        continue;

                    var node = new Ellipse();
                    node.Fill = GetNodeColor(poi.Kind);
                    node.Width = 8;
                    node.Height = 8;

                    var pos = GetDrawPos(origin, poi);
                    Canvas.SetLeft(node, pos.X - (node.Width / 2));
                    Canvas.SetTop(node, pos.Y - (node.Height / 2));

                    canvas.Children.Add(node);

                    if (poi.Edges != null)
                    {
                        foreach (var edge in poi.Edges)
                        {
                            var connection = info.Poi.FirstOrDefault(x => x.Id == edge);
                            if (connection != null && !(connection.X == 0 && connection.Y == 0))
                            {
                                var line = new Line();
                                var otherPos = GetDrawPos(origin, connection);
                                line.Stroke = Brushes.Black;
                                line.StrokeThickness = 1;
                                line.X1 = pos.X;
                                line.Y1 = pos.Y;
                                line.X2 = otherPos.X;
                                line.Y2 = otherPos.Y;
                                canvas.Children.Add(line);
                            }
                        }
                    }
                }
            }
        }

        private Brush GetNodeColor(string kind)
        {
            switch (kind)
            {
                case PoiKind.Npc:
                    return Brushes.Red;
                case PoiKind.Door:
                    return Brushes.Brown;
                case PoiKind.Stairs:
                    return Brushes.Brown;
                case PoiKind.Meet:
                    return Brushes.Red;
                case PoiKind.Waypoint:
                    return Brushes.Aqua;
                default:
                    return Brushes.Black;
            }
        }

        private Point GetDrawPos(Point origin, PointOfInterest poi)
        {
            var offsetX = 250;
            var offsetY = 250;
            var ratio = 1 / 100.0;
            var x = (poi.X - origin.X) * ratio;
            var y = (poi.Z - origin.Y) * ratio;
            return new Point(offsetX + x, offsetY - y);
        }

        private Point GetOrigin(CutsceneRoomInfo info)
        {
            if (info.Poi == null || info.Poi.Length == 0)
                return new Point();

            var x = 0;
            var y = 0;
            foreach (var poi in info.Poi)
            {
                x += poi.X;
                y += poi.Z;
            }
            x /= info.Poi.Length;
            y /= info.Poi.Length;
            return new Point(x, y);
        }

        private void LoadCutsceneRoomInfo()
        {
            _cutsceneRoomInfoMap.Clear();

            var json = File.ReadAllText(_cutsceneJsonPath);
            var map = JsonSerializer.Deserialize<Dictionary<string, CutsceneRoomInfo>>(json, new JsonSerializerOptions()
            {
                ReadCommentHandling = JsonCommentHandling.Skip,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            foreach (var kvp in map)
            {
                var key = RdtId.Parse(kvp.Key);
                _cutsceneRoomInfoMap[key] = kvp.Value;
            }
        }

        private void roomDropdown_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (roomDropdown.SelectedItem is RdtId roomId)
            {
                LoadMap(roomId);
            }
        }
    }
}
