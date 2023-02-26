using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace IntelOrca.Biohazard
{
    public class PldFile
    {
        private const int CHUNK_MESH = 2;
        private const int CHUNK_TIM = 4;

        private readonly byte[][] _chunks;

        public PldFile(string path)
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

            var objPath = path + $".obj";
            var mtlPath = path + $".mtl";

            var sb = new StringBuilder();
            sb.AppendLine($"mtllib {Path.GetFileName(mtlPath)}");
            sb.AppendLine($"usemtl main");

            var objIndex = 0;
            var vIndex = 1;
            var tvIndex = 1;
            foreach (var obj in objects)
            {
                sb.AppendLine($"o part_{objIndex:00}");
                ms.Position = 8 + obj.vtx_offset;
                var vertices = new Vertex[obj.vtx_count];
                for (int i = 0; i < obj.vtx_count; i++)
                {
                    vertices[i] = br.ReadStruct<Vertex>();
                }

                ms.Position = 8 + obj.nor_offset;
                var normals = new Vertex[obj.vtx_count];
                for (int i = 0; i < obj.vtx_count; i++)
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

                foreach (var v in vertices)
                {
                    // sb.AppendLine($"v {v.z * -0.0039068627450980394} {v.y * -0.0039068627450980394} {v.x * -0.0039068627450980394}");
                    sb.AppendLine($"v {v.x / 1000.0} {v.y / 1000.0} {v.z / 1000.0}");
                }
                foreach (var v in normals)
                {
                    sb.AppendLine($"vn {v.x / 1000.0} {v.y / 1000.0} {v.z / 1000.0}");
                }
                foreach (var t in triangles)
                {
                    var page = t.page & 0x0F;
                    var offsetU = page * 128;
                    sb.AppendLine($"vt {(offsetU + t.tu2) / 384.0} {1 - (t.tv2 / 256.0)}");
                    sb.AppendLine($"vt {(offsetU + t.tu1) / 384.0} {1 - (t.tv1 / 256.0)}");
                    sb.AppendLine($"vt {(offsetU + t.tu0) / 384.0} {1 - (t.tv0 / 256.0)}");
                }
                foreach (var t in quads)
                {
                    var page = t.page & 0x0F;
                    var offsetU = page * 128;
                    sb.AppendLine($"vt {(offsetU + t.tu2) / 384.0} {1 - (t.tv2 / 256.0)}");
                    sb.AppendLine($"vt {(offsetU + t.tu3) / 384.0} {1 - (t.tv3 / 256.0)}");
                    sb.AppendLine($"vt {(offsetU + t.tu1) / 384.0} {1 - (t.tv1 / 256.0)}");
                    sb.AppendLine($"vt {(offsetU + t.tu0) / 384.0} {1 - (t.tv0 / 256.0)}");
                }
                foreach (var t in triangles)
                {
                    sb.AppendLine($"f {t.v2 + vIndex}/{tvIndex + 0}/{t.v2 + vIndex} {t.v1 + vIndex}/{tvIndex + 1}/{t.v1 + vIndex} {t.v0 + vIndex}/{tvIndex + 2}/{t.v0 + vIndex}");
                    // sb.AppendLine($"f {t.v2 + vIndex}//{t.v2 + vIndex} {t.v1 + vIndex}//{t.v1 + vIndex} {t.v0 + vIndex}//{t.v0 + vIndex}");
                    tvIndex += 3;
                }
                foreach (var t in quads)
                {
                    sb.AppendLine($"f {t.v2 + vIndex}/{tvIndex + 0}/{t.v2 + vIndex} {t.v3 + vIndex}/{tvIndex + 1}/{t.v3 + vIndex} {t.v1 + vIndex}/{tvIndex + 2}/{t.v1 + vIndex} {t.v0 + vIndex}/{tvIndex + 3}/{t.v0 + vIndex}");
                    // sb.AppendLine($"f {t.v2 + vIndex}//{t.v2 + vIndex} {t.v3 + vIndex}//{t.v3 + vIndex} {t.v1 + vIndex}//{t.v1 + vIndex} {t.v0 + vIndex}//{t.v0 + vIndex}");
                    tvIndex += 4;
                }

                objIndex++;
                vIndex += obj.vtx_count;
            }
            File.WriteAllText(path + $".obj", sb.ToString());
        }

        public TimFile GetTim()
        {
            var meshChunk = _chunks[CHUNK_TIM];
            var ms = new MemoryStream(meshChunk);
            return new TimFile(ms);
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
            public byte tu0, tv0; /* u,v texture coordinates of vertex 0 */
            public byte dummy0, dummy1;
            public byte tu1, tv1; /* u,v texture coordinates of vertex 1 */
            public byte page, v0; /* v0: index for vertex and normal 0 */
            public byte tu2, tv2; /* u,v texture coordinates of vertex 2 */
            public byte v1, v2; /* v1,v2: index for vertex and normal 1,2 */
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct Quad
        {
            public byte tu0, tv0; /* u,v texture coordinates of vertex 1 */
            public byte dummy2, dummy3;
            public byte tu1, tv1;
            public byte page, dummy7;
            public byte tu2, tv2; /* u,v texture coordinates of vertex 2 */
            public byte v0, v1; /* v0,v1: index for vertex and normal 0,1 */
            public byte tu3, tv3; /* u,v texture coordinates of vertex 2 */
            public byte v2, v3; /* v2,v3: index for vertex and normal 2,3 */
        }
    }
}
