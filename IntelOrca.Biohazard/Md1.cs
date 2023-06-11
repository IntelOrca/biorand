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

        public Md1Builder ToBuilder()
        {
            var builder = new Md1Builder();
            for (var i = 0; i < NumObjects; i += 2)
            {
                var objTriangles = Objects[i];
                var objQuads = Objects[i + 1];

                var part = new Md1Builder.Part();
                part.Triangles.Positions.AddRange(GetPositionData(objTriangles).ToArray());
                part.Triangles.Normals.AddRange(GetNormalData(objTriangles).ToArray());
                part.Triangles.Triangles.AddRange(GetTriangles(objTriangles).ToArray());
                part.Triangles.TriangleTextures.AddRange(GetTriangleTextures(objTriangles).ToArray());

                part.Quads.Positions.AddRange(GetPositionData(objQuads).ToArray());
                part.Quads.Normals.AddRange(GetNormalData(objQuads).ToArray());
                part.Quads.Quads.AddRange(GetQuads(objQuads).ToArray());
                part.Quads.QuadTextures.AddRange(GetQuadTextures(objQuads).ToArray());

                builder.Parts.Add(part);
            }
            return builder;
        }

        private static readonly int[] g_partRemap = new[]
        {
            0, 8, 9, 10, 11, 12, 13, 14, 1, 2, 3, 4, 5, 6, 7
        };

        public unsafe Md2 ToMd2()
        {
            var builder = new Md2Builder();
            for (int i = 0; i < 15; i++)
                builder.Parts.Add(new Md2Builder.Part());

            for (int i = 0; i < 30; i += 2)
            {
                var part = builder.Parts[g_partRemap[i / 2]];

                // Get the triangle and quad objects
                var objTriangle = Objects[i];
                var objQuad = Objects[i + 1];

                // Add positions and normal placeholders
                foreach (var pos in GetPositionData(objTriangle))
                {
                    part.Positions.Add(pos.ToMd2());
                    part.Normals.Add(new Md2.Vector());
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
                    var triangleTexture = triangleTextureData[j];

                    part.Normals[triangle.v0] = normalData[triangle.n0].ToMd2();
                    part.Normals[triangle.v1] = normalData[triangle.n1].ToMd2();
                    part.Normals[triangle.v2] = normalData[triangle.n2].ToMd2();

                    part.Triangles.Add(new Md2.Triangle()
                    {
                        v0 = (byte)triangle.v0,
                        v1 = (byte)triangle.v1,
                        v2 = (byte)triangle.v2,
                        tu0 = triangleTexture.u0,
                        tu1 = triangleTexture.u1,
                        tu2 = triangleTexture.u2,
                        tv0 = triangleTexture.v0,
                        tv1 = triangleTexture.v1,
                        tv2 = triangleTexture.v2,
                        dummy0 = (byte)(triangleTexture.page * 64),
                        page = (byte)(0x80 | (triangleTexture.page & 0x0F)),
                        visible = 120
                    });
                }

                // Add quads
                for (int j = 0; j < quadData.Length; j++)
                {
                    var quad = quadData[j];
                    var quadTexture = quadTextureData[j];

                    part.Normals[quad.v0] = normalData[quad.n0].ToMd2();
                    part.Normals[quad.v1] = normalData[quad.n1].ToMd2();
                    part.Normals[quad.v2] = normalData[quad.n2].ToMd2();
                    part.Normals[quad.v3] = normalData[quad.n3].ToMd2();

                    part.Quads.Add(new Md2.Quad()
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
            }

            // Add extra part, if missing (hand with gun)
            if (builder.Parts.Count == 15)
            {
                builder.Parts.Add(builder.Parts[4]);
            }

            return builder.ToMd2();
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
