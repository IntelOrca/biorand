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

        public string GetPath(string baseName)
        {
            return Path.Combine(BasePath, baseName);
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

        public string GetText(BioVersion version, string path)
        {
            var fullPath = GetPath(version, path);
            return File.ReadAllText(fullPath);
        }

        public string[] GetDirectories(BioVersion version, string baseName)
        {
            var fullPath = GetPath(version, baseName);
            return GetDirectoriesSafe(fullPath);
        }

        public string[] GetFiles(string baseName)
        {
            return GetFilesSafe(GetPath(baseName));
        }

        public string[] GetFiles(BioVersion version, string baseName)
        {
            return GetFilesSafe(GetPath(version, baseName));
        }

        public string[] GetFiles(string a, string b)
        {
            return GetFilesSafe(Path.Combine(BasePath, a, b));
        }

        public string[] GetDirectoriesIn(string baseName)
        {
            var basePath = GetPath(baseName);
            return GetDirectoriesSafe(basePath);
        }

        public string[] GetBgmFiles(string tag) => GetTaggedFiles("bgm", tag);
        public string[] GetHurtFiles(string actor) => GetFiles("hurt", actor);
        public string[] GetVoiceFiles(string actor) => GetFiles("voice", actor);

        public string[] GetTaggedFiles(string baseName, string tag)
        {
            var files = new List<string>();
            var top = Path.Combine(BasePath, baseName);
            var directories = GetDirectoriesSafe(top);
            foreach (var directory in directories)
            {
                var dir = Path.Combine(directory, tag);
                var subFiles = GetFilesSafe(dir);
                files.AddRange(subFiles);
            }
            return files.ToArray();
        }

        private static string[] GetFilesSafe(string path)
        {
            if (Directory.Exists(path))
            {
                return Directory.GetFiles(path);
            }
            return new string[0];
        }

        private static string[] GetDirectoriesSafe(string path)
        {
            if (Directory.Exists(path))
            {
                return Directory.GetDirectories(path);
            }
            return new string[0];
        }
    }
}
