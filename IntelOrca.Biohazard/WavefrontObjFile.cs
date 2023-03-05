using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace IntelOrca.Biohazard
{
    public class WavefrontObjFile
    {
        public List<Vertex> Vertices { get; } = new List<Vertex>();
        public List<Vertex> Normals { get; } = new List<Vertex>();
        public List<TextureCoordinate> TextureCoordinates { get; } = new List<TextureCoordinate>();
        public List<ObjectGroup> Objects { get; } = new List<ObjectGroup>();

        public WavefrontObjFile(string path)
        {
            ObjectGroup? currentObject = null;
            var text = File.ReadAllText(path);
            var tr = new StringReader(text);
            string line;
            while ((line = tr.ReadLine()) != null)
            {
                line = line.Trim();
                var commentIndex = line.IndexOf('#');
                if (commentIndex != -1)
                {
                    line = line.Substring(0, commentIndex);
                }

                var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0)
                    continue;

                switch (parts[0])
                {
                    case "o":
                        if (currentObject != null)
                        {
                            Objects.Add(currentObject);
                        }
                        currentObject = new ObjectGroup();
                        break;
                    case "v":
                        Vertices.Add(new Vertex(double.Parse(parts[1]), double.Parse(parts[2]), double.Parse(parts[3])));
                        break;
                    case "vn":
                        Normals.Add(new Vertex(double.Parse(parts[1]), double.Parse(parts[2]), double.Parse(parts[3])));
                        break;
                    case "vt":
                        TextureCoordinates.Add(new TextureCoordinate(double.Parse(parts[1]), double.Parse(parts[2])));
                        break;
                    case "f":
                        if (parts.Length == 4)
                        {
                            currentObject!.Triangles.Add(new Triangle()
                            {
                                a = ParseFaceVertex(parts[1]),
                                b = ParseFaceVertex(parts[2]),
                                c = ParseFaceVertex(parts[3])
                            });
                        }
                        else if (parts.Length >= 5)
                        {
                            currentObject!.Quads.Add(new Quad()
                            {
                                a = ParseFaceVertex(parts[1]),
                                b = ParseFaceVertex(parts[2]),
                                c = ParseFaceVertex(parts[3]),
                                d = ParseFaceVertex(parts[4])
                            });
                        }
                        break;
                }
            }
            if (currentObject != null)
            {
                Objects.Add(currentObject);
            }
        }

        private FaceVertex ParseFaceVertex(string component)
        {
            var parts = component.Split('/');
            return new FaceVertex()
            {
                Vertex = int.Parse(parts[0]) - 1,
                Texture = int.Parse(parts[1]) - 1,
                Normal = int.Parse(parts[2]) - 1
            };
        }

        [DebuggerDisplay("v {x} {y} {z}")]
        public struct Vertex
        {
            public double x, y, z;

            public Vertex(double x, double y, double z)
            {
                this.x = x;
                this.y = y;
                this.z = z;
            }
        }

        [DebuggerDisplay("vt {u} {v}")]
        public struct TextureCoordinate
        {
            public double u, v;

            public TextureCoordinate(double u, double v)
            {
                this.u = u;
                this.v = v;
            }
        }

        [DebuggerDisplay("{Vertex}/{Texture}/{Normal}")]
        public struct FaceVertex
        {
            public int Vertex;
            public int Texture;
            public int Normal;

            public FaceVertex(int vertex, int texture, int normal)
            {
                Vertex = vertex;
                Texture = texture;
                Normal = normal;
            }
        }

        [DebuggerDisplay("Triangles = {Triangles.Count} Quads = {Quads.Count}")]
        public class ObjectGroup
        {
            public List<Triangle> Triangles { get; } = new List<Triangle>();
            public List<Quad> Quads { get; } = new List<Quad>();
        }

        [DebuggerDisplay("f {a} {b} {c}")]
        public struct Triangle
        {
            public FaceVertex a;
            public FaceVertex b;
            public FaceVertex c;
        }

        [DebuggerDisplay("f {a} {b} {c} {d}")]
        public struct Quad
        {
            public FaceVertex a;
            public FaceVertex b;
            public FaceVertex c;
            public FaceVertex d;
        }
    }
}
