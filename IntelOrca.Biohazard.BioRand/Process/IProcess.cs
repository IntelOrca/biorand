using System;

namespace IntelOrca.Biohazard.BioRand.Process
{
    public interface IProcess
    {
        int Id { get; }
        string Name { get; }
        bool IsRunning { get; }

        void ReadMemory(int offset, Span<byte> buffer);
        void WriteMemory(int offset, ReadOnlySpan<byte> buffer);
    }
}
