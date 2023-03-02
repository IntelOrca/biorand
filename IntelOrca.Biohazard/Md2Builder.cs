using System;
using System.Collections.Generic;
using System.IO;
using static IntelOrca.Biohazard.Md2;

namespace IntelOrca.Biohazard
{
    public class Md2Builder
    {
        public List<Part> Parts { get; } = new List<Part>();

        public unsafe Md2 ToMd2()
        {
            var objects = new List<ObjectDescriptor>();
            var positions = new List<Vector>();
            var normals = new List<Vector>();
            var triangles = new List<Triangle>();
            var quads = new List<Quad>();
            foreach (var part in Parts)
            {
                // Take a note of current index of each array
                var firstPositionIndex = positions.Count;
                var firstNormalIndex = normals.Count;
                var firstTriangleIndex = triangles.Count;
                var firstQuadIndex = quads.Count;

                // Add positions and normal placeholders
                positions.AddRange(part.Positions);
                normals.AddRange(part.Normals);
                triangles.AddRange(part.Triangles);
                quads.AddRange(part.Quads);

                // Add object (offsets are just an index at the moment)
                objects.Add(new ObjectDescriptor()
                {
                    vtx_offset = (ushort)firstPositionIndex,
                    nor_offset = (ushort)firstNormalIndex,
                    vtx_count = (ushort)(positions.Count - firstPositionIndex),
                    tri_offset = (ushort)firstTriangleIndex,
                    quad_offset = (ushort)firstQuadIndex,
                    tri_count = (ushort)(triangles.Count - firstTriangleIndex),
                    quad_count = (ushort)(quads.Count - firstQuadIndex)
                });
            }

            // Serialise the data
            if (positions.Count != normals.Count)
                throw new Exception("Expected same number of normals as positions.");

            var vertexOffset = Parts.Count * sizeof(ObjectDescriptor);
            var normalOffset = vertexOffset + (positions.Count * sizeof(Vector));
            var triangleOffset = normalOffset + (normals.Count * sizeof(Vector));
            var quadOffset = triangleOffset + (triangles.Count * sizeof(Triangle));

            var ms = new MemoryStream();
            var bw = new BinaryWriter(ms);
            bw.Write(0);
            bw.Write(Parts.Count);
            for (int i = 0; i < Parts.Count; i++)
            {
                var md2Object = objects[i];
                md2Object.vtx_offset = (ushort)(vertexOffset + (md2Object.vtx_offset * sizeof(Vector)));
                md2Object.nor_offset = (ushort)(normalOffset + (md2Object.nor_offset * sizeof(Vector)));
                md2Object.tri_offset = (ushort)(triangleOffset + (md2Object.tri_offset * sizeof(Triangle)));
                md2Object.quad_offset = (ushort)(quadOffset + (md2Object.quad_offset * sizeof(Quad)));
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

        public class Part
        {
            public List<Vector> Positions { get; } = new List<Vector>();
            public List<Vector> Normals { get; } = new List<Vector>();
            public List<Triangle> Triangles { get; } = new List<Triangle>();
            public List<Quad> Quads { get; } = new List<Quad>();
        }
    }
}
