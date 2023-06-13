using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace IntelOrca.Biohazard
{
    public sealed class Md2
    {
        private byte[] _data;

        public Md2(byte[] data)
        {
            _data = data;
        }

        public byte[] GetBytes() => _data;

        public int Length => BitConverter.ToInt32(_data, 0);
        public int NumObjects => BitConverter.ToInt32(_data, 4);
        public Span<ObjectDescriptor> Objects => GetSpan<ObjectDescriptor>(8, NumObjects);

        public Span<Vector> GetPositionData(in ObjectDescriptor obj) => GetSpan<Vector>(8 + obj.vtx_offset, obj.vtx_count);
        public Span<Vector> GetNormalData(in ObjectDescriptor obj) => GetSpan<Vector>(8 + obj.nor_offset, obj.vtx_count);
        public Span<Triangle> GetTriangles(in ObjectDescriptor obj) => GetSpan<Triangle>(8 + obj.tri_offset, obj.tri_count);
        public Span<Quad> GetQuads(in ObjectDescriptor obj) => GetSpan<Quad>(8 + obj.quad_offset, obj.quad_count);

        private Span<T> GetSpan<T>(int offset, int count) where T : struct
        {
            var data = new Span<byte>(_data, offset, _data.Length - offset);
            return MemoryMarshal.Cast<byte, T>(data).Slice(0, count);
        }

        public Md2Builder ToBuilder()
        {
            var builder = new Md2Builder();
            foreach (var obj in Objects)
            {
                var part = new Md2Builder.Part();
                part.Positions.AddRange(GetPositionData(obj).ToArray());
                part.Normals.AddRange(GetNormalData(obj).ToArray());
                part.Triangles.AddRange(GetTriangles(obj).ToArray());
                part.Quads.AddRange(GetQuads(obj).ToArray());
                builder.Parts.Add(part);
            }
            return builder;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct ObjectDescriptor
        {
            public ushort vtx_offset; /* Offset to vertex data */
            public ushort unknown0;
            public ushort nor_offset; /* Offset to normal vertex data */
            public ushort unknown1;
            public ushort vtx_count; /* Number of vertices */
            public ushort unknown2;
            public ushort tri_offset; /* Offset to triangle data */
            public ushort unknown3;
            public ushort quad_offset; /* Offset to quad data */
            public ushort unknown4;
            public ushort tri_count; /* Number of triangles */
            public ushort quad_count; /* Number of quads */
        }

        [DebuggerDisplay("{x}, {y}, {z}")]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct Vector
        {
            public short x;
            public short y;
            public short z;
            public short zero;

            public Vector(short x, short y, short z)
                : this()
            {
                this.x = x;
                this.y = y;
                this.z = z;
            }

            public Md1.Vector ToMd1() => new Md1.Vector(x, y, z);
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct Triangle
        {
            public byte tu0, tv0; /* u,v texture coordinates of vertex 0 */
            public byte dummy0, visible;
            public byte tu1, tv1; /* u,v texture coordinates of vertex 1 */
            public byte page, v0; /* v0: index for vertex and normal 0 */
            public byte tu2, tv2; /* u,v texture coordinates of vertex 2 */
            public byte v1, v2; /* v1,v2: index for vertex and normal 1,2 */
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct Quad
        {
            public byte tu0, tv0; /* u,v texture coordinates of vertex 1 */
            public byte dummy2, visible;
            public byte tu1, tv1;
            public byte page, dummy7;
            public byte tu2, tv2; /* u,v texture coordinates of vertex 2 */
            public byte v0, v1; /* v0,v1: index for vertex and normal 0,1 */
            public byte tu3, tv3; /* u,v texture coordinates of vertex 2 */
            public byte v2, v3; /* v2,v3: index for vertex and normal 2,3 */
        }
    }
}
