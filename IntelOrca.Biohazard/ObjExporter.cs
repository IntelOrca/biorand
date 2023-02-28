using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using static IntelOrca.Biohazard.Md2;
using static IntelOrca.Biohazard.WavefrontObjFile;

namespace IntelOrca.Biohazard
{
    public class ObjExporter
    {
        public void Export(Md2 md2, string objPath, int numPages)
        {
            var textureWidth = numPages * 128.0;
            var textureHeight = 256.0;

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
            foreach (var obj in md2.Objects)
            {
                sb.AppendLine($"o part_{objIndex:00}");
                foreach (var v in md2.GetPositionData(obj))
                {
                    AppendDataLine(sb, "v", v.x / 1000.0, v.y / 1000.0, v.z / 1000.0);
                }
                foreach (var v in md2.GetNormalData(obj))
                {
                    // var total = (double)Math.Abs(v.x) + Math.Abs(v.y) + Math.Abs(v.z);
                    // var x = v.x / total;
                    // var y = v.y / total;
                    // var z = v.z / total;
                    // AppendDataLine(sb, "vn", x, y, z);
                    AppendDataLine(sb, "vn", v.x / 5000.0, v.y / 5000.0, v.z / 5000.0);
                }
                foreach (var t in md2.GetTriangles(obj))
                {
                    var page = t.page & 0x0F;
                    var offsetU = page * 128;
                    AppendDataLine(sb, "vt", (offsetU + t.tu2) / textureWidth, 1 - (t.tv2 / textureHeight));
                    AppendDataLine(sb, "vt", (offsetU + t.tu1) / textureWidth, 1 - (t.tv1 / textureHeight));
                    AppendDataLine(sb, "vt", (offsetU + t.tu0) / textureWidth, 1 - (t.tv0 / textureHeight));
                }
                foreach (var t in md2.GetQuads(obj))
                {
                    var page = t.page & 0x0F;
                    var offsetU = page * 128;
                    AppendDataLine(sb, "vt", (offsetU + t.tu2) / textureWidth, 1 - (t.tv2 / textureHeight));
                    AppendDataLine(sb, "vt", (offsetU + t.tu3) / textureWidth, 1 - (t.tv3 / textureHeight));
                    AppendDataLine(sb, "vt", (offsetU + t.tu1) / textureWidth, 1 - (t.tv1 / textureHeight));
                    AppendDataLine(sb, "vt", (offsetU + t.tu0) / textureWidth, 1 - (t.tv0 / textureHeight));
                }
                sb.AppendLine($"s 1");
                foreach (var t in md2.GetTriangles(obj))
                {
                    sb.AppendLine($"f {t.v2 + vIndex}/{tvIndex + 0}/{t.v2 + vIndex} {t.v1 + vIndex}/{tvIndex + 1}/{t.v1 + vIndex} {t.v0 + vIndex}/{tvIndex + 2}/{t.v0 + vIndex}");
                    // sb.AppendLine($"f {t.v2 + vIndex}//{t.v2 + vIndex} {t.v1 + vIndex}//{t.v1 + vIndex} {t.v0 + vIndex}//{t.v0 + vIndex}");
                    tvIndex += 3;
                }
                sb.AppendLine($"s 1");
                foreach (var t in md2.GetQuads(obj))
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
    }
}
