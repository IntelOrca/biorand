using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static IntelOrca.Biohazard.Md1;

namespace IntelOrca.Biohazard
{
    public class Md1Builder
    {
        public List<Part> Parts { get; } = new List<Part>();

        public unsafe Md1 ToMd1()
        {
            var objects = new List<ObjectDescriptor>();
            var positions = new List<Vector>();
            var normals = new List<Vector>();
            var primitives = new List<object>();
            var textures = new List<object>();

            var vertexSize = 0;
            var normalSize = 0;
            var primitiveSize = 0;
            var textureSize = 0;
            foreach (var part in Parts)
            {
                // Add positions and normal placeholders
                positions.AddRange(part.Positions);
                normals.AddRange(part.Normals);
                primitives.AddRange(part.Triangles.Cast<object>());
                textures.AddRange(part.TriangleTextures.Cast<object>());

                // Add objects (offsets are just an index at the moment)
                objects.Add(new ObjectDescriptor()
                {
                    vtx_offset = (ushort)vertexSize,
                    vtx_count = (ushort)part.Positions.Count,
                    nor_offset = (ushort)normalSize,
                    nor_count = (ushort)part.Normals.Count,
                    pri_offset = (ushort)primitiveSize,
                    pri_count = (ushort)part.Triangles.Count,
                    tex_offset = (ushort)textureSize
                });

                // The original files did not increase primitive size until after the quad object
                // if there are no quads
                if (part.Quads.Count != 0)
                {
                    primitiveSize += part.Triangles.Count * sizeof(Triangle);
                }
                textureSize += part.Triangles.Count * sizeof(TriangleTexture);

                primitives.AddRange(part.Quads.Cast<object>());
                textures.AddRange(part.QuadTextures.Cast<object>());

                objects.Add(new ObjectDescriptor()
                {
                    vtx_offset = vertexSize,
                    vtx_count = part.Positions.Count,
                    nor_offset = normalSize,
                    nor_count = part.Normals.Count,
                    pri_offset = primitiveSize,
                    pri_count = part.Quads.Count,
                    tex_offset = textureSize
                });

                if (part.Quads.Count == 0)
                {
                    primitiveSize += part.Triangles.Count * sizeof(Triangle);
                }
                primitiveSize += part.Quads.Count * sizeof(Quad);
                textureSize += part.Quads.Count * sizeof(QuadTexture);

                vertexSize += part.Positions.Count * sizeof(Vector);
                normalSize += part.Normals.Count * sizeof(Vector);
            }

            // Serialise the data
            var vertexOffset = objects.Count * sizeof(ObjectDescriptor);
            var normalOffset = vertexOffset + vertexSize;
            var primitiveOffset = normalOffset + normalSize;
            var textureOffset = primitiveOffset + primitiveSize;

            var ms = new MemoryStream();
            var bw = new BinaryWriter(ms);
            bw.Write(0);
            bw.Write(0);
            bw.Write(objects.Count);
            for (int i = 0; i < objects.Count; i++)
            {
                var md1Object = objects[i];
                md1Object.vtx_offset += vertexOffset;
                md1Object.nor_offset += normalOffset;
                md1Object.pri_offset += primitiveOffset;
                md1Object.tex_offset += textureOffset;
                bw.Write(md1Object);
            }
            foreach (var p in positions)
                bw.Write(p);
            foreach (var n in normals)
                bw.Write(n);
            foreach (var p in primitives)
            {
                if (p is Triangle t)
                    bw.Write(t);
                else if (p is Quad q)
                    bw.Write(q);
            }

            ms.Position = 0;
            bw.Write((uint)ms.Length);
            ms.Position = ms.Length;

            foreach (var t in textures)
            {
                if (t is TriangleTexture tt)
                    bw.Write(tt);
                else if (t is QuadTexture qt)
                    bw.Write(qt);
            }

            return new Md1(ms.ToArray());
        }

        public class Part
        {
            public List<Vector> Positions { get; } = new List<Vector>();
            public List<Vector> Normals { get; } = new List<Vector>();
            public List<Triangle> Triangles { get; } = new List<Triangle>();
            public List<TriangleTexture> TriangleTextures { get; } = new List<TriangleTexture>();
            public List<Quad> Quads { get; } = new List<Quad>();
            public List<QuadTexture> QuadTextures { get; } = new List<QuadTexture>();
        }
    }
}
