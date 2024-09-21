using System;

namespace IntelOrca.Biohazard.BioRand.Routing
{
    [Flags]
    public enum RouteSolverResult
    {
        NodesRemaining = 1 << 0,
        PotentialSoftlock = 1 << 1,
    }
}
