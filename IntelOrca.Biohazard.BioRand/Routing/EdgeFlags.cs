using System;

namespace IntelOrca.Biohazard.BioRand.Routing
{
    [Flags]
    public enum EdgeFlags : byte
    {
        Removable = 1 << 0,
        Consume = 1 << 1,
    }
}
