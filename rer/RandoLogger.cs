using System;
using System.IO;

namespace rer
{
    internal class RandoLogger : IDisposable
    {
        private readonly StreamWriter _sw;

        public RandoLogger(string path)
        {
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
    }
}
