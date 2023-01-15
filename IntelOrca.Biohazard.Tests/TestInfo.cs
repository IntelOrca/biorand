using System;
using System.IO;

namespace IntelOrca.Biohazard.Tests
{
    internal static class TestInfo
    {
        public static string GetInstallPath(int game)
        {
            var a = $@"F:\games\re{game + 1}";
            var b = $@"M:\games\re{game + 1}";
            if (Directory.Exists(a))
            {
                return a;
            }
            if (Directory.Exists(b))
            {
                return b;
            }
            throw new Exception("Unable to find RE.");
        }
    }
}
