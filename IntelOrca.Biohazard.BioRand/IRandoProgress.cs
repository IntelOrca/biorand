using System;

namespace IntelOrca.Biohazard.BioRand
{
    public interface IRandoProgress
    {
        IDisposable BeginTask(int? player, string message);
    }
}
