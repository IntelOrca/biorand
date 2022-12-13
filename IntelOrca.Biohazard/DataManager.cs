using System;
using System.Collections.Generic;
using System.IO;

namespace IntelOrca.Biohazard
{
    internal class DataManager
    {
        public string BasePath { get; }

        public DataManager(string basePath)
        {
            BasePath = basePath;
        }

        public string GetPath(string baseName, string path)
        {
            return Path.Combine(BasePath, baseName, path);
        }

        public string GetPath(BioVersion version, string path)
        {
            switch (version)
            {
                case BioVersion.Biohazard1:
                    return GetPath("re1", path);
                case BioVersion.Biohazard2:
                    return GetPath("re2", path);
                default:
                    throw new NotImplementedException();
            }
        }

        public byte[] GetData(BioVersion version, string path)
        {
            var fullPath = GetPath(version, path);
            return File.ReadAllBytes(fullPath);
        }

        public string[] GetFiles(BioVersion version, string baseName)
        {
            var basePath = GetPath(version, baseName);
            return Directory.GetFiles(basePath);
        }

        public string[] GetBgmFiles(string tag)
        {
            var files = new List<string>();
            var top = Path.Combine(BasePath, "bgm");
            if (Directory.Exists(top))
            {
                var directories = Directory.GetDirectories(top);
                foreach (var directory in directories)
                {
                    var dir = Path.Combine(directory, tag);
                    if (Directory.Exists(dir))
                    {
                        var subFiles = Directory.GetFiles(dir);
                        files.AddRange(subFiles);
                    }
                }
            }
            return files.ToArray();
        }
    }
}
