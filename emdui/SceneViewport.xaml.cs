using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace emdui
{
    /// <summary>
    /// Interaction logic for SceneViewport.xaml
    /// </summary>
    public partial class SceneViewport : UserControl
    {
        private ModelScene _scene;

        private Point3D _cameraLookAt = new Point3D(0, -1000, 0);
        private double _cameraZoom = 10000;
        private double _cameraAngleH;
        private double _cameraAngleV;

        private Point _cameraPointerStartPosition;
        private Point3D _cameraStartPosition;
        private double _cameraStartWidth;
        private double _startCameraAngleH;
        private double _startCameraAngleV;

        public ModelScene Scene
        {
            get => _scene;
            set
            {
                if (_scene != value)
                {
                    _scene = value;
                    RefreshScene();
                }
            }
        }

        public SceneViewport()
        {
            InitializeComponent();
            SetCameraPerspective();
        }

        public void SetCameraPerspective()
        {
            viewport.Camera = new PerspectiveCamera(
                new Point3D(-5000, 0, 0),
                new Vector3D(1, 0, 0),
                new Vector3D(0, -1, 0),
                70);
        }

        public void SetCameraOrthographic(Vector3D lookDirection)
        {
            var distance = 10000;
            var origin = new Vector3D(0, -1500, 0);
            var position = origin - (lookDirection * distance);

            var camera = new OrthographicCamera(
                position.ToPoint3D(),
                lookDirection,
                new Vector3D(0, -1, 0),
                5000);
            camera.FarPlaneDistance = distance * 2;
            camera.NearPlaneDistance = 1;
            viewport.Camera = camera;
        }

        private void RefreshScene()
        {
            viewport.Children.Clear();
            viewport.Children.Add(_scene.CreateVisual3d());
            viewport.Children.Add(new ModelVisual3D() {
                Content = new AmbientLight(Colors.White)
            });

            UpdateCamera();
        }

        private void UpdateCamera()
        {
            if (viewport.Camera is PerspectiveCamera camera)
            {
                // var rx = _cameraZoom * (Math.Cos(_cameraAngleH) - Math.Sin(_cameraAngleV));
                // var ry = _cameraZoom * Math.Sin(_cameraAngleV);
                // var rz = _cameraZoom * Math.Sin(_cameraAngleH);

                var hoizontalVector = new Vector3D(
                    Math.Cos(_cameraAngleH),
                    0,
                    Math.Sin(_cameraAngleH));
                var verticalVector = new Vector3D(
                    Math.Cos(_cameraAngleV),
                    -Math.Sin(_cameraAngleV),
                    0);

                var merged = hoizontalVector + verticalVector;
                merged.Normalize();
                var position = merged * _cameraZoom;
                camera.Position = new Point3D(position.X, position.Y, position.Z);

                var look = _cameraLookAt - camera.Position;
                look.Normalize();
                camera.LookDirection = look;
            }
        }

        private void container_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (viewport.Camera is PerspectiveCamera pcamera)
            {
                _cameraPointerStartPosition = e.GetPosition(viewport);
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    _startCameraAngleH = _cameraAngleH;
                    _startCameraAngleV = _cameraAngleV;
                }
                if (e.MiddleButton == MouseButtonState.Pressed)
                {
                    _cameraStartPosition = _cameraLookAt;
                }
            }
            else if (viewport.Camera is OrthographicCamera camera)
            {
                _cameraPointerStartPosition = e.GetPosition(viewport);
                _cameraStartPosition = camera.Position;
                _cameraStartWidth = camera.Width;
            }
        }

        private void container_MouseMove(object sender, MouseEventArgs e)
        {
            var position = e.GetPosition(viewport);
            var diff = position - _cameraPointerStartPosition;

            if (viewport.Camera is OrthographicCamera camera)
            {
                if (e.MouseDevice.MiddleButton == MouseButtonState.Pressed)
                {
                    var newCameraPosition = _cameraStartPosition;
                    newCameraPosition.Z += diff.X * 6;
                    newCameraPosition.Y -= diff.Y * 6;
                    camera.Position = newCameraPosition;
                }
            }
            else if (viewport.Camera is PerspectiveCamera pcamera)
            {
                if (e.MouseDevice.LeftButton == MouseButtonState.Pressed)
                {
                    _cameraAngleH = _startCameraAngleH + (diff.X / 100);
                    _cameraAngleV = _startCameraAngleV + (diff.Y / 100);
                    UpdateCamera();
                }
                if (e.MouseDevice.MiddleButton == MouseButtonState.Pressed)
                {
                    var newCameraPosition = _cameraStartPosition;
                    newCameraPosition.Z += diff.X * 6;
                    newCameraPosition.Y -= diff.Y * 6;
                    _cameraLookAt = newCameraPosition;
                    UpdateCamera();
                }
            }
        }

        private void container_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (viewport.Camera is OrthographicCamera camera)
            {
                camera.Width = Math.Max(2000, camera.Width - e.Delta * 4);
            }
            else if (viewport.Camera is PerspectiveCamera pcamera)
            {
                _cameraZoom = Math.Max(1000, _cameraZoom - (e.Delta * 10));
                UpdateCamera();
            }
        }
    }
}
