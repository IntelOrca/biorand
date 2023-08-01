using System;

namespace IntelOrca.Biohazard
{
    public class ReInstallConfig
    {
        private string[] _installPath = new string[3];
        private bool[] _enabled = new bool[3];

        public bool RandomizeTitleVoice { get; set; } = true;

        public string GetInstallPath(int index)
        {
            var result = _installPath[index];
            if (result == null)
            {
                throw new Exception($"RE {index + 1} path not set up.");
            }
            return result;
        }
        public bool IsEnabled(int index) => _enabled[index];

        public void SetInstallPath(int index, string path)
        {
            _installPath[index] = path;
        }

        public void SetEnabled(int index, bool value)
        {
            _enabled[index] = value;
        }

        public string GetInstallPath(BioVersion version)
        {
            switch (version)
            {
                case BioVersion.Biohazard1:
                    return GetInstallPath(0);
                case BioVersion.Biohazard2:
                    return GetInstallPath(1);
                case BioVersion.Biohazard3:
                    return GetInstallPath(2);
                default:
                    throw new InvalidOperationException();
            }
        }

        public bool IsEnabled(BioVersion version)
        {
            switch (version)
            {
                case BioVersion.Biohazard1:
                    return IsEnabled(0);
                case BioVersion.Biohazard2:
                    return IsEnabled(1);
                case BioVersion.Biohazard3:
                    return IsEnabled(2);
                default:
                    throw new InvalidOperationException();
            }
        }
    }
}
