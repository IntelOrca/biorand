using System;
using System.Reflection;

namespace IntelOrca.Biohazard
{
    public class Program
    {
        public static Version CurrentVersion = Assembly.GetEntryAssembly().GetName().Version;
        public static string CurrentVersionInfo => $"BioRand {CurrentVersion.Major}.{CurrentVersion.Minor}.{CurrentVersion.Build}";
    }
}
