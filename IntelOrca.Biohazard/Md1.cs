using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using static IntelOrca.Biohazard.Md1;
using static IntelOrca.Biohazard.Md2;

namespace IntelOrca.Biohazard
{
    public class Md1
    {
        private byte[] _data;

        public Md1(byte[] data)
        {
            _data = data;
        }

        public byte[] GetBytes() => _data;

        public int Length => BitConverter.ToInt32(_data, 0);
        public int NumObjects => BitConverter.ToInt32(_data, 8);
        public Span<ObjectDescriptor> Objects => GetSpan<ObjectDescriptor>(12, NumObjects);

        public Span<Vector> GetPositionData(in ObjectDescriptor obj) => GetSpan<Vector>(12 + obj.vtx_offset, obj.vtx_count);
        public Span<Vector> GetNormalData(in ObjectDescriptor obj) => GetSpan<Vector>(12 + obj.nor_offset, obj.nor_count);
        public Span<Triangle> GetTriangles(in ObjectDescriptor obj) => GetSpan<Triangle>(12 + obj.pri_offset, obj.pri_count);
        public Span<Quad> GetQuads(in ObjectDescriptor obj) => GetSpan<Quad>(12 + obj.pri_offset, obj.pri_count);
        public Span<TriangleTexture> GetTriangleTextures(in ObjectDescriptor obj) => GetSpan<TriangleTexture>(12 + obj.tex_offset, obj.pri_count);
        public Span<QuadTexture> GetQuadTextures(in ObjectDescriptor obj) => GetSpan<QuadTexture>(12 + obj.tex_offset, obj.pri_count);

        private Span<T> GetSpan<T>(int offset, int count) where T : struct
        {
            var data = new Span<byte>(_data, offset, _data.Length - offset);
            return MemoryMarshal.Cast<byte, T>(data).Slice(0, count);
        }

        private static readonly int[] g_partRemap = new[]
        {
            0, 8, 9, 10, 11, 12, 13, 14, 1, 2, 3, 4, 5, 6, 7
        };

        public unsafe Md2 ToMd2()
        {
            var numObjects = NumObjects / 2;
            var objects = new Md2.ObjectDescriptor[numObjects];
            var positions = new List<Md2.Vector>();
            var normals = new List<Md2.Vector>();
            var triangles = new List<Md2.Triangle>();
            var quads = new List<Md2.Quad>();

            for (int i = 0; i < NumObjects; i += 2)
            {
                // Take a note of current index of each array
                var firstPositionIndex = positions.Count;
                var firstNormalIndex = normals.Count;
                var firstTriangleIndex = triangles.Count;
                var firstQuadIndex = quads.Count;

                // Get the triangle and quad objects
                var objTriangle = Objects[i];
                var objQuad = Objects[i + 1];

                // Add positions and normal placeholders
                foreach (var pos in GetPositionData(objTriangle))
                {
                    positions.Add(pos.ToMd2());
                    normals.Add(new Md2.Vector());
                }

                var normalData = GetNormalData(objTriangle);
                var triangleData = GetTriangles(objTriangle);
                var triangleTextureData = GetTriangleTextures(objTriangle);
                var quadData = GetQuads(objQuad);
                var quadTextureData = GetQuadTextures(objQuad);

                // Add triangles
                for (int j = 0; j < triangleData.Length; j++)
                {
                    var triangle = triangleData[j];
                    var trinagleTexture = triangleTextureData[j];

                    normals[firstNormalIndex + triangle.v0] = normalData[triangle.n0].ToMd2();
                    normals[firstNormalIndex + triangle.v1] = normalData[triangle.n1].ToMd2();
                    normals[firstNormalIndex + triangle.v2] = normalData[triangle.n2].ToMd2();

                    triangles.Add(new Md2.Triangle()
                    {
                        v0 = (byte)triangle.v0,
                        v1 = (byte)triangle.v1,
                        v2 = (byte)triangle.v2,
                        tu0 = trinagleTexture.u0,
                        tu1 = trinagleTexture.u1,
                        tu2 = trinagleTexture.u2,
                        tv0 = trinagleTexture.v0,
                        tv1 = trinagleTexture.v1,
                        tv2 = trinagleTexture.v2,
                        dummy0 = (byte)(trinagleTexture.page * 64),
                        page = (byte)(0x80 | (trinagleTexture.page & 0x0F)),
                        visible = 120
                    });
                }

                // Add quads
                for (int j = 0; j < quadData.Length; j++)
                {
                    var quad = quadData[j];
                    var quadTexture = quadTextureData[j];

                    normals[quad.v0] = normalData[quad.n0].ToMd2();
                    normals[quad.v1] = normalData[quad.n1].ToMd2();
                    normals[quad.v2] = normalData[quad.n2].ToMd2();
                    normals[quad.v3] = normalData[quad.n3].ToMd2();

                    quads.Add(new Md2.Quad()
                    {
                        v0 = (byte)quad.v0,
                        v1 = (byte)quad.v1,
                        v2 = (byte)quad.v2,
                        v3 = (byte)quad.v3,
                        tu0 = quadTexture.u0,
                        tu1 = quadTexture.u1,
                        tu2 = quadTexture.u2,
                        tu3 = quadTexture.u3,
                        tv0 = quadTexture.v0,
                        tv1 = quadTexture.v1,
                        tv2 = quadTexture.v2,
                        tv3 = quadTexture.v3,
                        dummy2 = (byte)(quadTexture.page * 64),
                        page = (byte)(0x80 | (quadTexture.page & 0x0F)),
                        visible = 120
                    });
                }

                // Add object (offsets are just an index at the moment)
                objects[g_partRemap[i / 2]] = new Md2.ObjectDescriptor()
                {
                    vtx_offset = (ushort)firstPositionIndex,
                    nor_offset = (ushort)firstNormalIndex,
                    vtx_count = (ushort)(positions.Count - firstPositionIndex),
                    tri_offset = (ushort)firstTriangleIndex,
                    quad_offset = (ushort)firstQuadIndex,
                    tri_count = (ushort)(triangles.Count - firstTriangleIndex),
                    quad_count = (ushort)(quads.Count - firstQuadIndex)
                };
            }

            // Serialise the data
            if (positions.Count != normals.Count)
                throw new Exception("Expected same number of normals as positions.");

            // Add extra part, if missing (hand with gun)
            if (numObjects == 15)
            {
                numObjects++;
                Array.Resize(ref objects, numObjects);
                var srcObject = objects[4];
                objects[numObjects - 1] = objects[4];
                ref var obj = ref objects[numObjects - 1];
                obj.vtx_offset = (ushort)positions.Count;
                obj.nor_offset = (ushort)normals.Count;
                obj.tri_offset = (ushort)triangles.Count;
                obj.quad_offset = (ushort)quads.Count;
                positions.AddRange(positions.Skip(srcObject.vtx_offset).Take(srcObject.vtx_count).ToArray());
                normals.AddRange(normals.Skip(srcObject.nor_offset).Take(srcObject.vtx_count).ToArray());
                triangles.AddRange(triangles.Skip(srcObject.tri_offset).Take(srcObject.tri_count).ToArray());
                quads.AddRange(quads.Skip(srcObject.quad_offset).Take(srcObject.quad_count).ToArray());
            }

            var vertexOffset = numObjects * sizeof(Md2.ObjectDescriptor);
            var normalOffset = vertexOffset + (positions.Count * sizeof(Md2.Vector));
            var triangleOffset = normalOffset + (normals.Count * sizeof(Md2.Vector));
            var quadOffset = triangleOffset + (triangles.Count * sizeof(Md2.Triangle));

            var ms = new MemoryStream();
            var bw = new BinaryWriter(ms);
            bw.Write(0);
            bw.Write(numObjects);
            for (int i = 0; i < numObjects; i++)
            {
                var md2Object = objects[i];
                md2Object.vtx_offset = (ushort)(vertexOffset + (md2Object.vtx_offset * sizeof(Md2.Vector)));
                md2Object.nor_offset = (ushort)(normalOffset + (md2Object.nor_offset * sizeof(Md2.Vector)));
                md2Object.tri_offset = (ushort)(triangleOffset + (md2Object.tri_offset * sizeof(Md2.Triangle)));
                md2Object.quad_offset = (ushort)(quadOffset + (md2Object.quad_offset * sizeof(Md2.Quad)));
                bw.Write(md2Object);
            }
            foreach (var p in positions)
                bw.Write(p);
            foreach (var n in normals)
                bw.Write(n);
            foreach (var t in triangles)
                bw.Write(t);
            foreach (var q in quads)
                bw.Write(q);

            ms.Position = 0;
            bw.Write((uint)ms.Length);

            return new Md2(ms.ToArray());
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct ObjectDescriptor
        {
            public int vtx_offset;
            public int vtx_count;
            public int nor_offset;
            public int nor_count;
            public int pri_offset;
            public int pri_count;
            public int tex_offset;
        }

        [DebuggerDisplay("{x}, {y}, {z}")]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct Vector
        {
            public short x;
            public short y;
            public short z;
            public short zero;

            public Md2.Vector ToMd2() => new Md2.Vector(x, y, z);
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct Triangle
        {
            public short n0;
            public short v0;
            public short n1;
            public short v1;
            public short n2;
            public short v2;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct TriangleTexture
        {
            public byte u0;
            public byte v0;
            public short clutId;
            public byte u1;
            public byte v1;
            public short page;
            public byte u2;
            public byte v2;
            public short zero;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct Quad
        {
            public short n0;
            public short v0;
            public short n1;
            public short v1;
            public short n2;
            public short v2;
            public short n3;
            public short v3;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct QuadTexture
        {
            public byte u0;
            public byte v0;
            public short clutId;
            public byte u1;
            public byte v1;
            public short page;
            public byte u2;
            public byte v2;
            public short zero1;
            public byte u3;
            public byte v3;
            public short zero2;
        }
    }
}
