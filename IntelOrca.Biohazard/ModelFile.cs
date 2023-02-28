using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace IntelOrca.Biohazard
{
    public abstract class ModelFile
    {
        protected abstract int NumPages { get; }

        public abstract byte[] GetMd2();
        public abstract void SetMd2(byte[] value);

        public void ImportObj(string objPath)
        {
            var objFile = new WavefrontObjFile(objPath);
            var meshBuilder = new MeshBuilder(objFile, NumPages);
            meshBuilder.Import();
            var data = meshBuilder.GetData();
            SetMd2(data);
        }

        public void ExportObj(string objPath)
        {
            var textureWidth = NumPages * 128.0;
            var textureHeight = 256.0;

            var ed2 = GetMd2();
            var ms = new MemoryStream(ed2);
            var br = new BinaryReader(ms);
            var length = br.ReadInt32();
            var numObjects = br.ReadInt32();
            var objects = new ObjectDescriptor[numObjects];
            for (int i = 0; i < numObjects; i++)
            {
                objects[i] = br.ReadStruct<ObjectDescriptor>();
            }

            var mtlPath = Path.ChangeExtension(objPath, ".mtl");
            var imgPath = Path.ChangeExtension(objPath, ".png");
            var sb = new StringBuilder();
            sb.AppendLine("newmtl main");
            sb.AppendLine("Ka 1.000 1.000 1.000");
            sb.AppendLine("Kd 1.000 1.000 1.000");
            sb.AppendLine($"map_Kd {Path.GetFileName(imgPath)}");
            File.WriteAllText(mtlPath, sb.ToString());

            sb.Clear();
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
                    AppendDataLine(sb, "v", v.x / 1000.0, v.y / 1000.0, v.z / 1000.0);
                }
                foreach (var v in normals)
                {
                    // var total = (double)Math.Abs(v.x) + Math.Abs(v.y) + Math.Abs(v.z);
                    // var x = v.x / total;
                    // var y = v.y / total;
                    // var z = v.z / total;
                    // AppendDataLine(sb, "vn", x, y, z);
                    AppendDataLine(sb, "vn", v.x / 5000.0, v.y / 5000.0, v.z / 5000.0);
                }
                foreach (var t in triangles)
                {
                    var page = t.page & 0x0F;
                    var offsetU = page * 128;
                    AppendDataLine(sb, "vt", (offsetU + t.tu2) / textureWidth, 1 - (t.tv2 / textureHeight));
                    AppendDataLine(sb, "vt", (offsetU + t.tu1) / textureWidth, 1 - (t.tv1 / textureHeight));
                    AppendDataLine(sb, "vt", (offsetU + t.tu0) / textureWidth, 1 - (t.tv0 / textureHeight));
                }
                foreach (var t in quads)
                {
                    var page = t.page & 0x0F;
                    var offsetU = page * 128;
                    AppendDataLine(sb, "vt", (offsetU + t.tu2) / textureWidth, 1 - (t.tv2 / textureHeight));
                    AppendDataLine(sb, "vt", (offsetU + t.tu3) / textureWidth, 1 - (t.tv3 / textureHeight));
                    AppendDataLine(sb, "vt", (offsetU + t.tu1) / textureWidth, 1 - (t.tv1 / textureHeight));
                    AppendDataLine(sb, "vt", (offsetU + t.tu0) / textureWidth, 1 - (t.tv0 / textureHeight));
                }
                sb.AppendLine($"s 1");
                foreach (var t in triangles)
                {
                    sb.AppendLine($"f {t.v2 + vIndex}/{tvIndex + 0}/{t.v2 + vIndex} {t.v1 + vIndex}/{tvIndex + 1}/{t.v1 + vIndex} {t.v0 + vIndex}/{tvIndex + 2}/{t.v0 + vIndex}");
                    // sb.AppendLine($"f {t.v2 + vIndex}//{t.v2 + vIndex} {t.v1 + vIndex}//{t.v1 + vIndex} {t.v0 + vIndex}//{t.v0 + vIndex}");
                    tvIndex += 3;
                }
                sb.AppendLine($"s 1");
                foreach (var t in quads)
                {
                    sb.AppendLine($"f {t.v2 + vIndex}/{tvIndex + 0}/{t.v2 + vIndex} {t.v3 + vIndex}/{tvIndex + 1}/{t.v3 + vIndex} {t.v1 + vIndex}/{tvIndex + 2}/{t.v1 + vIndex} {t.v0 + vIndex}/{tvIndex + 3}/{t.v0 + vIndex}");
                    // sb.AppendLine($"f {t.v2 + vIndex}//{t.v2 + vIndex} {t.v3 + vIndex}//{t.v3 + vIndex} {t.v1 + vIndex}//{t.v1 + vIndex} {t.v0 + vIndex}//{t.v0 + vIndex}");
                    tvIndex += 4;
                }

                objIndex++;
                vIndex += obj.vtx_count;
            }
            File.WriteAllText(objPath, sb.ToString());
        }

        private void AppendDataLine(StringBuilder sb, string kind, params double[] parameters)
        {
            sb.Append(kind);
            sb.Append(' ');
            foreach (var p in parameters)
            {
                sb.AppendFormat("{0:0.000000}", p);
                sb.Append(' ');
            }
            sb.Remove(sb.Length - 1, 1);
            sb.Append('\n');
        }

        private class MeshBuilder
        {
            private WavefrontObjFile _objFile;
            private int _textureWidth;
            private int _textureHeight;
            private Stream _dataStream;

            private List<ObjectDescriptor> _objects = new List<ObjectDescriptor>();
            private Dictionary<int, byte> _positionMap = new Dictionary<int, byte>();
            private Vertex[] _positions = new Vertex[256];
            private Vertex[] _normals = new Vertex[256];
            private List<Triangle> _triangles = new List<Triangle>();
            private List<Quad> _quads = new List<Quad>();
            private ObjectDescriptor _currentObject;
            private int _minVertexIndex;
            private int _maxVertexIndex;

            public MeshBuilder(WavefrontObjFile objFile, int numPages)
            {
                _objFile = objFile;
                _dataStream = new MemoryStream();
                _textureWidth = numPages * 128;
                _textureHeight = 256;
            }

            public void Import()
            {
                foreach (var objGroup in _objFile.Objects)
                {
                    BeginObject();

                    // Find lowest vertex index
                    foreach (var face in objGroup.Triangles)
                    {
                        UpdateBaseVertex(face.a.Vertex);
                        UpdateBaseVertex(face.b.Vertex);
                        UpdateBaseVertex(face.c.Vertex);
                    }
                    foreach (var face in objGroup.Quads)
                    {
                        UpdateBaseVertex(face.a.Vertex);
                        UpdateBaseVertex(face.b.Vertex);
                        UpdateBaseVertex(face.c.Vertex);
                        UpdateBaseVertex(face.d.Vertex);
                    }

                    foreach (var face in objGroup.Triangles)
                    {
                        var triangle = new Triangle();
                        triangle.v2 = AddVertex(face.a.Vertex, face.a.Normal);
                        triangle.v1 = AddVertex(face.b.Vertex, face.b.Normal);
                        triangle.v0 = AddVertex(face.c.Vertex, face.c.Normal);
                        (triangle.page, triangle.tu2, triangle.tv2) = GetTextureOffset(face.a.Texture);
                        (triangle.page, triangle.tu1, triangle.tv1) = GetTextureOffset(face.b.Texture);
                        (triangle.page, triangle.tu0, triangle.tv0) = GetTextureOffset(face.c.Texture);
                        triangle.page |= 0x80;
                        triangle.dummy0 = (byte)(triangle.page * 64);
                        triangle.visible = 120;
                        _triangles.Add(triangle);
                    }
                    foreach (var face in objGroup.Quads)
                    {
                        var quad = new Quad();
                        quad.v2 = AddVertex(face.a.Vertex, face.a.Normal);
                        quad.v3 = AddVertex(face.b.Vertex, face.b.Normal);
                        quad.v1 = AddVertex(face.c.Vertex, face.c.Normal);
                        quad.v0 = AddVertex(face.d.Vertex, face.d.Normal);
                        (quad.page, quad.tu2, quad.tv2) = GetTextureOffset(face.a.Texture);
                        (quad.page, quad.tu3, quad.tv3) = GetTextureOffset(face.b.Texture);
                        (quad.page, quad.tu1, quad.tv1) = GetTextureOffset(face.c.Texture);
                        (quad.page, quad.tu0, quad.tv0) = GetTextureOffset(face.d.Texture);
                        quad.page |= 0x80;
                        quad.dummy2 = (byte)(quad.page * 64);
                        quad.visible = 120;
                        _quads.Add(quad);
                    }
                    EndObject();
                }
            }

            private void BeginObject()
            {
                _currentObject = new ObjectDescriptor();
                _positionMap.Clear();
                Array.Clear(_positions, 0, _positions.Length);
                Array.Clear(_normals, 0, _normals.Length);
                _triangles.Clear();
                _quads.Clear();
                _minVertexIndex = int.MaxValue;
                _maxVertexIndex = 0;
            }

            private void EndObject()
            {
                var dataBw = new BinaryWriter(_dataStream);
                _currentObject.vtx_offset = (ushort)_dataStream.Position;
                _currentObject.vtx_count = (ushort)(_maxVertexIndex - _minVertexIndex + 1);
                for (int i = 0; i < _currentObject.vtx_count; i++)
                {
                    dataBw.Write(_positions[i]);
                }
                _currentObject.nor_offset = (ushort)_dataStream.Position;
                for (int i = 0; i < _currentObject.vtx_count; i++)
                {
                    dataBw.Write(_normals[i]);
                }
                _currentObject.tri_offset = (ushort)_dataStream.Position;
                _currentObject.tri_count = (ushort)_triangles.Count;
                foreach (var t in _triangles)
                {
                    dataBw.Write(t);
                }
                _currentObject.quad_offset = (ushort)_dataStream.Position;
                _currentObject.quad_count = (ushort)_quads.Count;
                foreach (var q in _quads)
                {
                    dataBw.Write(q);
                }
                _objects.Add(_currentObject);
            }

            public unsafe byte[] GetData()
            {
                var objectDescriptorLength = (ushort)(_objects.Count * sizeof(ObjectDescriptor));

                var ms = new MemoryStream();
                var bw = new BinaryWriter(ms);

                bw.Write((int)(8 + objectDescriptorLength + _dataStream.Length));
                bw.Write((int)_objects.Count);

                for (int i = 0; i < _objects.Count; i++)
                {
                    var obj = _objects[i];
                    obj.vtx_offset += objectDescriptorLength;
                    obj.nor_offset += objectDescriptorLength;
                    obj.tri_offset += objectDescriptorLength;
                    obj.quad_offset += objectDescriptorLength;
                    bw.Write(obj);
                }

                _dataStream.Position = 0;
                _dataStream.CopyTo(ms);
                return ms.ToArray();
            }

            private void UpdateBaseVertex(int index)
            {
                _minVertexIndex = Math.Min(_minVertexIndex, index);
                _maxVertexIndex = Math.Max(_maxVertexIndex, index);
            }

            private byte AddVertex(int vertexIndex, int normalIndex)
            {
                if (_positionMap.TryGetValue(vertexIndex, out var newIndex))
                {
                    return newIndex;
                }

                newIndex = (byte)(vertexIndex - _minVertexIndex);
                _positionMap[vertexIndex] = newIndex;

                var position = _objFile.Vertices[vertexIndex];
                var normal = _objFile.Normals[normalIndex];
                var newPosition = new Vertex()
                {
                    x = (short)(position.x * 1000),
                    y = (short)(position.y * 1000),
                    z = (short)(position.z * 1000)
                };
                // We should normalize the normal first
                var newNormal = new Vertex()
                {
                    x = (short)(normal.x * 5000),
                    y = (short)(normal.y * 5000),
                    z = (short)(normal.z * 5000)
                };
                _positions[newIndex] = newPosition;
                _normals[newIndex] = newNormal;
                return newIndex;
            }

            private (byte, byte, byte) GetTextureOffset(int index)
            {
                var coord = _objFile.TextureCoordinates[index];
                var u = (int)(coord.u * _textureWidth);
                var v = (int)(1 - (coord.v * _textureHeight));
                var page = (byte)(u / 128);
                u &= 127;
                return (page, (byte)u, (byte)v);
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        protected struct ObjectDescriptor
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
        protected struct Vertex
        {
            public short x;
            public short y;
            public short z;
            public short zero;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        protected struct Triangle
        {
            public byte tu0, tv0; /* u,v texture coordinates of vertex 0 */
            public byte dummy0, visible;
            public byte tu1, tv1; /* u,v texture coordinates of vertex 1 */
            public byte page, v0; /* v0: index for vertex and normal 0 */
            public byte tu2, tv2; /* u,v texture coordinates of vertex 2 */
            public byte v1, v2; /* v1,v2: index for vertex and normal 1,2 */
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        protected struct Quad
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
