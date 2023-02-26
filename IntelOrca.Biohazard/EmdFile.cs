using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace IntelOrca.Biohazard
{
    public class EmdFile
    {
        private const int CHUNK_MESH = 14;

        private readonly byte[][] _chunks;

        public EmdFile(string path)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                var br = new BinaryReader(fs);

                // Read header
                var directoryOffset = br.ReadInt32();
                var numOffsets = br.ReadInt32();

                // Read directory
                fs.Position = directoryOffset;
                var offsets = new int[numOffsets + 1];
                for (int i = 0; i < numOffsets; i++)
                {
                    offsets[i] = br.ReadInt32();
                }
                offsets[numOffsets] = directoryOffset;

                // Check all offsets are in order
                var lastOffset = 0;
                foreach (var offset in offsets)
                {
                    if (offset < lastOffset)
                        throw new NotSupportedException("Offsets not in order");
                    lastOffset = offset;
                }

                // Read chunks
                _chunks = new byte[numOffsets][];
                for (int i = 0; i < numOffsets; i++)
                {
                    var len = offsets[i + 1] - offsets[i];
                    fs.Position = offsets[i];
                    _chunks[i] = br.ReadBytes(len);
                }
            }
        }

        public void Save(string path)
        {
            var chunkSum = _chunks.Sum(x => x.Length);
            using (var fs = new FileStream(path, FileMode.Create))
            {
                var bw = new BinaryWriter(fs);
                bw.Write(8 + chunkSum);
                bw.Write(_chunks.Length);
                for (int i = 0; i < _chunks.Length; i++)
                {
                    bw.Write(_chunks[i]);
                }

                var offset = 8;
                for (int i = 0; i < _chunks.Length; i++)
                {
                    bw.Write(offset);
                    offset += _chunks[i].Length;
                }
            }
        }

        public void Export(string path)
        {
            var meshChunk = _chunks[CHUNK_MESH];
            var ms = new MemoryStream(meshChunk);
            var br = new BinaryReader(ms);

            var length = br.ReadInt32();
            var numObjects = br.ReadInt32();
            var objects = new ObjectDescriptor[numObjects];
            for (int i = 0; i < numObjects; i++)
            {
                objects[i] = br.ReadStruct<ObjectDescriptor>();
            }

            var objIndex = 0;
            foreach (var obj in objects)
            {
                ms.Position = 8 + obj.vtx_offset;
                var vertices = new Vertex[256];
                for (int i = 0; i < 256; i++)
                {
                    vertices[i] = br.ReadStruct<Vertex>();
                }

                ms.Position = 8 + obj.nor_offset;
                var normals = new Vertex[256];
                for (int i = 0; i < 256; i++)
                {
                    normals[i] = br.ReadStruct<Vertex>();
                }

                ms.Position = 8 + obj.tri_offset;
                var triangles = new Triangle[obj.tri_count];
                for (int i = 0; i < obj.tri_count; i++)
                {
                    triangles[i] = br.ReadStruct<Triangle>();
                }

                ms.Position = 8 + obj.quad_offset;
                var quads = new Quad[obj.quad_count];
                for (int i = 0; i < obj.quad_count; i++)
                {
                    quads[i] = br.ReadStruct<Quad>();
                }

                var sb = new StringBuilder();
                foreach (var v in vertices)
                {
                    sb.AppendLine($"v {v.x / 10000.0f} {v.y / 10000.0f} {v.z / 10000.0f}");
                }
                // foreach (var v in normals)
                // {
                //     sb.AppendLine($"vn {v.x} {v.y} {v.z}");
                // }
                // foreach (var t in triangles)
                // {
                //     sb.AppendLine($"f {t.v0 + 1}//{t.v0 + 1} {t.v1 + 1}//{t.v1 + 1} {t.v2 + 1}//{t.v2 + 1}");
                // }
                foreach (var t in quads)
                {
                    sb.AppendLine($"f {t.v0 + 1} {t.v1 + 1} {t.v3 + 1}");
                    sb.AppendLine($"f {t.v1 + 1} {t.v2 + 1} {t.v3 + 1}");
                }

                File.WriteAllText(path + $"_{objIndex:00}.obj", sb.ToString());
                objIndex++;
                break;
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct ObjectDescriptor
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
        private struct Vertex
        {
            public short x;
            public short y;
            public short z;
            public short zero;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct Triangle
        {
            public byte tu1, tv1; /* u,v texture coordinates of vertex 1 */
            public byte dummy2, v0; /* v0: index for vertex and normal 0 */
            public byte tu2, tv2; /* u,v texture coordinates of vertex 2 */
            public byte v1, v2; /* v1,v2: index for vertex and normal 1,2 */
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct Quad
        {
            public byte tu1, tv1; /* u,v texture coordinates of vertex 1 */
            public byte dummy2, dummy3;
            public byte tu2, tv2; /* u,v texture coordinates of vertex 2 */
            public byte v0, v1; /* v0,v1: index for vertex and normal 0,1 */
            public byte tu3, tv3; /* u,v texture coordinates of vertex 2 */
            public byte v2, v3; /* v2,v3: index for vertex and normal 2,3 */
        }
    }
}
