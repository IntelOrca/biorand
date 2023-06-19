using System;
using System.IO;

namespace IntelOrca.Biohazard.Tests
{
    internal static class TestInfo
    {
        public static string GetInstallPath(int game)
        {
            var places = new[]
            {
                $@"D:\games\re{game + 1}",
                $@"F:\games\re{game + 1}",
                $@"M:\games\re{game + 1}"
            };

            foreach (var place in places)
            {
                if (Directory.Exists(place))
                {
                    return place;
                }
            }
            throw new Exception("Unable to find RE.");
        }
    }
}
