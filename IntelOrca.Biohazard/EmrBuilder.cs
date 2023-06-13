using System;
using System.Collections.Generic;
using System.IO;

namespace IntelOrca.Biohazard
{
    public class EmrBuilder
    {
        public List<Emr.Vector> RelativePositions { get; } = new List<Emr.Vector>();
        public List<byte[]> Armatures { get; } = new List<byte[]>();
        public byte[] KeyFrameData { get; set; } = new byte[0];
        public ushort KeyFrameSize { get; set; }

        public Emr ToEmr()
        {
            if (RelativePositions.Count != Armatures.Count)
                throw new InvalidOperationException("Number of relative positions does not match number of armatures.");

            var ms = new MemoryStream();
            var bw = new BinaryWriter(ms);

            // Header
            bw.Write((ushort)0);
            bw.Write((ushort)0);
            bw.Write((ushort)RelativePositions.Count);
            bw.Write((ushort)KeyFrameSize);

            // Positions
            foreach (var pos in RelativePositions)
            {
                bw.Write(pos.x);
                bw.Write(pos.y);
                bw.Write(pos.z);
            }

            // Padding?
            if (ms.Position < 100)
                ms.Position = 100;

            // Armatures
            var armatureStartOffset = ms.Position;
            var offset = Armatures.Count * 4;
            foreach (var armature in Armatures)
            {
                bw.Write((ushort)armature.Length);
                bw.Write((ushort)offset);
                offset += armature.Length;
            }
            foreach (var armature in Armatures)
            {
                bw.Write(armature);
            }

            if (ms.Position < 176)
                ms.Position = 176;

            // Key frames
            var keyFrameStartOffset = ms.Position;
            bw.Write(KeyFrameData);

            ms.Position = 0;
            bw.Write((ushort)armatureStartOffset);
            ms.Position = 2;
            bw.Write((ushort)keyFrameStartOffset);

            return new Emr(ms.ToArray());
        }
    }
}
