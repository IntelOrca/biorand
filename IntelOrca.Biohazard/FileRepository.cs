using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace IntelOrca.Biohazard
{
    public class FileRepository : IDisposable
    {
        private readonly List<RE3Archive> _re3Archives = new List<RE3Archive>();
        private readonly Dictionary<string, Func<Stream>> _re3Paths = new Dictionary<string, Func<Stream>>(StringComparer.OrdinalIgnoreCase);
        private readonly string _dataPath;
        private readonly string? _modPath;

        public string DataPath => _dataPath;
        public string ModPath => _modPath!;

        public FileRepository(string dataPath, string? modPath = null)
        {
            _dataPath = dataPath;
            _modPath = modPath;
        }

        public void Dispose()
        {
            foreach (var archive in _re3Archives)
            {
                archive.Dispose();
            }
            _re3Archives.Clear();
        }

        public string GetDataPath(string path)
        {
            return NormalizePath(Path.Combine(_dataPath, path));
        }

        public string GetModPath(string path)
        {
            return NormalizePath(Path.Combine(_modPath, path));
        }

        public void AddRE3Archive(string path)
        {
            var re3Archive = new RE3Archive(path);
            _re3Archives.Add(re3Archive);

            var dir = Path.GetDirectoryName(path);
            for (int i = 0; i < re3Archive.NumFiles; i++)
            {
                var path1 = Path.Combine(dir, re3Archive[i]);
                var path2 = NormalizePath(path1);
                var index = i;
                _re3Paths.Add(path2, () => new MemoryStream(re3Archive.GetFileContents(index)));
            }
        }

        public string[] GetFiles(string path)
        {
            var result = new List<string>();
            path = NormalizePath(path);
            foreach (var kvp in _re3Paths)
            {
                var filePath = kvp.Key;
                if (filePath.StartsWith(path, StringComparison.OrdinalIgnoreCase))
                {
                    if (filePath.IndexOf('\\', path.Length + 1) == -1)
                    {
                        result.Add(filePath);
                    }
                }
            }
            return result.ToArray();
        }

        public Stream GetStream(string path)
        {
            if (File.Exists(path))
                return new FileStream(path, FileMode.Open, FileAccess.Read);

            var normalizedPath = NormalizePath(path);
            if (_re3Paths.TryGetValue(normalizedPath, out var getStream))
            {
                return getStream();
            }
            throw new IOException($"File does not exist: '{normalizedPath}'");
        }

        public byte[] GetBytes(string path)
        {
            using var fs = GetStream(path);
            var len = (int)fs.Length;
            var bytes = new byte[len];
            fs.Read(bytes, 0, len);
            return bytes;
        }

        public void Copy(string src, string dst)
        {
            if (File.Exists(src))
            {
                File.Copy(src, dst, true);
            }
            else
            {
                var stream = GetStream(src);
                using var fs = new FileStream(dst, FileMode.Create);
                stream.CopyTo(fs);
            }
        }

        private static string NormalizePath(string path)
        {
            var sb = new StringBuilder();
            foreach (var ch in path)
            {
                if (ch == '/')
                    sb.Append('\\');
                else
                    sb.Append(ch);
            }
            if (sb[sb.Length - 1] == '\\')
                sb.Remove(sb.Length - 1, 1);
            return sb.ToString();
        }
    }
}
