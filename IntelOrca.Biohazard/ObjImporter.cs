using System;
using System.Collections.Generic;
using System.IO;
using static IntelOrca.Biohazard.Md2;

namespace IntelOrca.Biohazard
{
    public class ObjImporter
    {
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        private WavefrontObjFile _objFile;
        private Stream _dataStream;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        private int _textureWidth;
        private int _textureHeight;

        private List<ObjectDescriptor> _objects = new List<ObjectDescriptor>();
        private Dictionary<int, byte> _positionMap = new Dictionary<int, byte>();
        private Vector[] _positions = new Vector[256];
        private Vector[] _normals = new Vector[256];
        private List<Triangle> _triangles = new List<Triangle>();
        private List<Quad> _quads = new List<Quad>();
        private ObjectDescriptor _currentObject;
        private int _minVertexIndex;
        private int _maxVertexIndex;

        public Md1 ImportMd1(string objPath, int numPages)
        {
            return new Md1(new byte[0]);
        }

        public Md2 ImportMd2(string objPath, int numPages)
        {
            _objFile = new WavefrontObjFile(objPath);
            _dataStream = new MemoryStream();
            _textureWidth = numPages * 128;
            _textureHeight = 256;
            Import();
            return new Md2(GetData());
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
            var newPosition = new Vector()
            {
                x = (short)(position.x * 1000),
                y = (short)(position.y * 1000),
                z = (short)(position.z * 1000)
            };
            // We should normalize the normal first
            var newNormal = new Vector()
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
}
