using System;
using System.Collections.Generic;
using System.IO;
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
            var triangles = new List<Triangle>();
            var triangleTextures = new List<TriangleTexture>();
            var quads = new List<Quad>();
            var quadTextures = new List<QuadTexture>();
            foreach (var part in Parts)
            {
                // Take a note of current index of each array
                var firstPositionIndex = positions.Count;
                var firstNormalIndex = normals.Count;
                var firstPrimitiveIndex = triangles.Count;
                var firstPrimitiveTextureIndex = triangleTextures.Count;

                // Add positions and normal placeholders
                positions.AddRange(part.Triangles.Positions);
                normals.AddRange(part.Triangles.Normals);
                triangles.AddRange(part.Triangles.Triangles);
                triangleTextures.AddRange(part.Triangles.TriangleTextures);

                // Add object (offsets are just an index at the moment)
                objects.Add(new ObjectDescriptor()
                {
                    vtx_offset = (ushort)firstPositionIndex,
                    vtx_count = (ushort)(positions.Count - firstPositionIndex),
                    nor_offset = (ushort)firstNormalIndex,
                    nor_count = (ushort)(normals.Count - firstPositionIndex),
                    pri_offset = (ushort)firstPrimitiveIndex,
                    pri_count = (ushort)(triangles.Count - firstPrimitiveIndex),
                    tex_offset = (ushort)firstPrimitiveTextureIndex
                });

                // Take a note of current index of each array
                firstPositionIndex = positions.Count;
                firstNormalIndex = normals.Count;
                firstPrimitiveIndex = quads.Count;
                firstPrimitiveTextureIndex = quadTextures.Count;

                // Add positions and normal placeholders
                positions.AddRange(part.Quads.Positions);
                normals.AddRange(part.Quads.Normals);
                quads.AddRange(part.Quads.Quads);
                quadTextures.AddRange(part.Quads.QuadTextures);

                // Add object (offsets are just an index at the moment)
                objects.Add(new ObjectDescriptor()
                {
                    vtx_offset = (ushort)firstPositionIndex,
                    vtx_count = (ushort)(positions.Count - firstPositionIndex),
                    nor_offset = (ushort)firstNormalIndex,
                    nor_count = (ushort)(normals.Count - firstPositionIndex),
                    pri_offset = (ushort)firstPrimitiveIndex,
                    pri_count = (ushort)(quads.Count - firstPrimitiveIndex),
                    tex_offset = (ushort)firstPrimitiveTextureIndex
                });
            }

            // Serialise the data
            if (positions.Count != normals.Count)
                throw new Exception("Expected same number of normals as positions.");

            var vertexOffset = objects.Count * sizeof(ObjectDescriptor);
            var normalOffset = vertexOffset + (positions.Count * sizeof(Vector));
            var triangleOffset = normalOffset + (normals.Count * sizeof(Vector));
            var triangleTextureOffset = triangleOffset + (triangles.Count * sizeof(Triangle));
            var quadOffset = triangleTextureOffset + (triangleTextures.Count * sizeof(TriangleTexture));
            var quadTextureOffset = quadOffset + (quads.Count * sizeof(Quad));

            var ms = new MemoryStream();
            var bw = new BinaryWriter(ms);
            bw.Write(0);
            bw.Write(0);
            bw.Write(objects.Count);
            for (int i = 0; i < objects.Count; i++)
            {
                var md1Object = objects[i];
                md1Object.vtx_offset = (ushort)(vertexOffset + (md1Object.vtx_offset * sizeof(Vector)));
                md1Object.nor_offset = (ushort)(normalOffset + (md1Object.nor_offset * sizeof(Vector)));
                if ((i & 1) == 0)
                {
                    md1Object.pri_offset = (ushort)(triangleOffset + (md1Object.pri_offset * sizeof(Triangle)));
                    md1Object.tex_offset = (ushort)(triangleTextureOffset + (md1Object.tex_offset * sizeof(TriangleTexture)));
                }
                else
                {
                    md1Object.pri_offset = (ushort)(quadOffset + (md1Object.pri_offset * sizeof(Quad)));
                    md1Object.tex_offset = (ushort)(quadTextureOffset + (md1Object.tex_offset * sizeof(QuadTexture)));
                }
                bw.Write(md1Object);
            }
            foreach (var p in positions)
                bw.Write(p);
            foreach (var n in normals)
                bw.Write(n);
            foreach (var t in triangles)
                bw.Write(t);
            foreach (var tt in triangleTextures)
                bw.Write(tt);
            foreach (var q in quads)
                bw.Write(q);
            foreach (var qt in quadTextures)
                bw.Write(qt);

            ms.Position = 0;
            bw.Write((uint)ms.Length);

            return new Md1(ms.ToArray());
        }

        public class Part
        {
            public PartTriangles Triangles { get; set; } = new PartTriangles();
            public PartQuads Quads { get; set; } = new PartQuads();
        }

        public class PartTriangles
        {
            public List<Vector> Positions { get; } = new List<Vector>();
            public List<Vector> Normals { get; } = new List<Vector>();
            public List<Triangle> Triangles { get; } = new List<Triangle>();
            public List<TriangleTexture> TriangleTextures { get; } = new List<TriangleTexture>();
        }

        public class PartQuads
        {
            public List<Vector> Positions { get; } = new List<Vector>();
            public List<Vector> Normals { get; } = new List<Vector>();
            public List<Quad> Quads { get; } = new List<Quad>();
            public List<QuadTexture> QuadTextures { get; } = new List<QuadTexture>();
        }
    }
}
