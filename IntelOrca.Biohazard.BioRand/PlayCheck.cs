using System;

namespace IntelOrca.Biohazard.BioRand
{
    internal class PlayCheck
    {
        public int GlobalId { get; set; }
        public PlayRequirement[] Requires { get; set; } = Array.Empty<PlayRequirement>();
    }
}
