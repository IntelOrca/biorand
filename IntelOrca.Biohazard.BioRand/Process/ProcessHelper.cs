using IntelOrca.Biohazard.BioRand.RE2;

namespace IntelOrca.Biohazard.BioRand.Process
{
    public static class ProcessHelper
    {
        public static IProcessHelper GetHelper(BioVersion version, IProcess process)
        {
            return version switch
            {
                BioVersion.Biohazard2 => new Re2ProcessHelper(process)
            };
        }
    }
}
