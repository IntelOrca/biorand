using System.Windows.Media.Media3D;

namespace emdui.Extensions
{
    internal static class VectorExtensions
    {
        public static Point3D ToPoint3D(this Vector3D v) => new Point3D(v.X, v.Y, v.Z);
    }
}
