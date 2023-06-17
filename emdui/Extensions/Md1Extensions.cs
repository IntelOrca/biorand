using System.Windows.Media.Media3D;
using IntelOrca.Biohazard;

namespace emdui.Extensions
{
    internal static class Md1Extensions
    {
        public static Point3D ToPoint3D(this Md1.Vector v) => new Point3D(v.x, v.y, v.z);
        public static Vector3D ToVector3D(this Md1.Vector v) => new Vector3D(v.x, v.y, v.z);
        public static Point3D ToPoint3D(this Md2.Vector v) => new Point3D(v.x, v.y, v.z);
        public static Vector3D ToVector3D(this Md2.Vector v) => new Vector3D(v.x, v.y, v.z);
    }
}
