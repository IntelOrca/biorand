using System;

namespace IntelOrca.Biohazard.BioRand
{
    public class ReInstallConfig
    {
        private string[] _installPath = new string[4];
        private bool[] _enabled = new bool[4];

        public bool EnableCustomContent { get; set; }
        public bool RandomizeTitleVoice { get; set; } = true;
        public bool MaxInventorySize { get; set; }
        public bool DoorSkip { get; set; }

        public string GetInstallPath(int index)
        {
            var result = _installPath[index];
            if (result == null)
            {
                if (index == 3)
                    throw new Exception($"RE:CVX path not set up.");
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
                case BioVersion.BiohazardCv:
                    return GetInstallPath(3);
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
                case BioVersion.BiohazardCv:
                    return IsEnabled(3);
                default:
                    throw new InvalidOperationException();
            }
        }
    }
}
