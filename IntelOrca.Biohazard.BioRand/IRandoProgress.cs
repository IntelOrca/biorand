using System;

namespace IntelOrca.Biohazard
{
    public interface IRandoProgress
    {
        IDisposable BeginTask(int? player, string message);
    }
}
