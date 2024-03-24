using System;
using System.Collections.Generic;

namespace IntelOrca.Biohazard.BioRand
{
    internal class PlayNode
    {
        public RdtId RdtId { get; set; }
        public RdtId? LinkedRdtId { get; set; }

        public List<PlayEdge> Edges { get; } = new List<PlayEdge>();
        public PlayItem[] Items { get; set; } = Array.Empty<PlayItem>();
        public PlayCheck[] Checks { get; set; } = Array.Empty<PlayCheck>();

        public Dictionary<byte, RdtItemId> LinkedItems { get; set; } = new Dictionary<byte, RdtItemId>();
        public byte[] DoorRandoAllRequiredItems { get; set; } = Array.Empty<byte>();
        public DoorRandoCategory Category { get; set; }
        public int[] DoorRandoNop { get; set; } = Array.Empty<int>();
        public List<PlayItem> PlacedKeyItems { get; } = new List<PlayItem>();
        public int Depth { get; set; }
        public bool Visited { get; set; }
        public bool HasCutscene { get; set; }

        public PlayNode(RdtId rdtId)
        {
            RdtId = rdtId;
        }

        public override string ToString() => RdtId.ToString();
    }
}
