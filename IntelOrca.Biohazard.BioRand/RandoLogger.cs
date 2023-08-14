using System;
using System.IO;

namespace IntelOrca.Biohazard.BioRand
{
    public class RandoLogger : IDisposable
    {
        private readonly StreamWriter _sw;

        public IRandoProgress Progress { get; }

        public RandoLogger(IRandoProgress progress, string path)
        {
            Progress = progress;
            _sw = new StreamWriter(path);
        }

        public void Dispose()
        {
            _sw.Dispose();
        }

        public void WriteHeading(string s)
        {
            _sw.WriteLine(s);
            _sw.Flush();
        }

        public void WriteLine(string s)
        {
            _sw.WriteLine($"    {s}");
            _sw.Flush();
        }

        public void WriteException(Exception ex)
        {
            WriteLine($"Exception: {ex.Message}");
            WriteLine(ex.StackTrace);
        }
    }
}
