using System;
using System.Linq;
using System.Reflection;

namespace IntelOrca.Biohazard
{
    public class Program
    {
        public static Assembly CurrentAssembly => Assembly.GetEntryAssembly();
        public static Version CurrentVersion = CurrentAssembly?.GetName().Version ?? new Version();
        public static string CurrentVersionNumber => $"{CurrentVersion.Major}.{CurrentVersion.Minor}.{CurrentVersion.Build}";
        public static string CurrentVersionInfo => $"BioRand {CurrentVersion.Major}.{CurrentVersion.Minor}.{CurrentVersion.Build} ({GitHash})";
        public static string GitHash
        {
            get
            {
                var assembly = CurrentAssembly;
                if (assembly == null)
                    return "";

                var attribute = assembly
                    .GetCustomAttributes<AssemblyInformationalVersionAttribute>()
                    .FirstOrDefault();
                var rev = attribute.InformationalVersion;
                var plusIndex = rev.IndexOf('+');
                if (plusIndex != -1)
                {
                    return rev.Substring(plusIndex + 1);
                }
                return rev;
            }
        }
    }
}
