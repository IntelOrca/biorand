using System;
using System.Runtime.Remoting.Messaging;
using System.Text;

namespace IntelOrca.Biohazard
{
    public class RandoConfig
    {
        private readonly static char[] _pwdChars = new[]
        {
            '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'E', 'F', 'G',
            'H', 'J', 'K', 'L', 'M', 'N', 'P', 'Q', 'R', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z',
        };

        public const int MaxSeed = 0b11111_11111_11111_11111;
        public const byte LatestVersion = 4;

        public byte Version { get; set; } = LatestVersion;
        public byte Game { get; set; } = 2;
        public byte GameVariant { get; set; } = 0;
        public int Seed { get; set; }

        // Flags
        public bool ProtectFromSoftLock { get; set; } = true;
        public bool ChangePlayer { get; set; }
        public bool RandomDoors { get; set; }
        public bool RandomNPCs { get; set; } = true;
        public bool RandomEnemies { get; set; } = true;
        public bool RandomItems { get; set; } = true;
        public bool RandomBgm { get; set; } = true;
        public bool ShuffleItems { get; set; } = true;
        public bool AlternativeRoutes { get; set; } = true;
        public bool IncludeDocuments { get; set; }

        public bool IncludeBGMRE1 { get; set; }
        public bool IncludeBGMRE2 { get; set; }
        public bool IncludeBGMRE3 { get; set; }
        public bool IncludeBGMRE4 { get; set; }
        public bool IncludeBGMOther { get; set; }

        public bool IncludeNPCRE1 { get; set; }
        public bool IncludeNPCRE2 { get; set; }
        public bool IncludeNPCRE3 { get; set; }
        public bool IncludeNPCOther { get; set; }


        // Numbers
        public byte Player0 { get; set; }
        public byte Player1 { get; set; }
        public byte RatioAmmo { get; set; } = 16;
        public byte RatioHealth { get; set; } = 16;
        public byte RatioInkRibbons { get; set; } = 16;
        public byte AmmoQuantity { get; set; } = 4;
        public byte EnemyDifficulty { get; set; } = 2;
        public byte AreaCount { get; set; } = 3;
        public byte AreaSize { get; set; } = 7;

        public static RandoConfig FromString(string code)
        {
            var result = new RandoConfig();
            var reader = new Reader(code);
            reader.ReadDigit();
            result.Version = reader.ReadDigit();
            result.Game = reader.ReadDigit();
            result.GameVariant = Math.Min(reader.ReadDigit(), (byte)1);

            result.Seed = reader.ReadInt32(20);

            result.ProtectFromSoftLock = reader.ReadFlag();
            result.RandomDoors = reader.ReadFlag();
            result.RandomNPCs = reader.ReadFlag();
            result.RandomEnemies = reader.ReadFlag();
            result.RandomItems = reader.ReadFlag();

            result.RandomBgm = reader.ReadFlag();
            result.ShuffleItems = reader.ReadFlag();
            result.AlternativeRoutes = reader.ReadFlag();
            result.IncludeDocuments = reader.ReadFlag();
            result.ChangePlayer = reader.ReadFlag();

            result.RatioAmmo = reader.ReadDigit();
            result.RatioHealth = reader.ReadDigit();
            result.RatioInkRibbons = reader.ReadDigit();

            result.AmmoQuantity = reader.ReadByte(3);
            result.EnemyDifficulty = reader.ReadByte(2);

            result.AreaCount = reader.ReadByte(2);
            result.AreaSize = reader.ReadByte(3);

            result.IncludeBGMRE1 = reader.ReadFlag();
            result.IncludeBGMRE2 = reader.ReadFlag();
            reader.ReadFlag();
            reader.ReadFlag();
            result.IncludeBGMOther = reader.ReadFlag();

            result.IncludeNPCRE1 = reader.ReadFlag();
            result.IncludeNPCRE2 = reader.ReadFlag();
            reader.ReadFlag();
            reader.ReadFlag();
            result.IncludeNPCOther = reader.ReadFlag();

            result.Player0 = reader.ReadDigit();
            result.Player1 = reader.ReadDigit();
            return result;
        }

        public RandoConfig Clone() => FromString(ToString());

        public RandoConfig WithPlayerScenario(int player, int scenario)
        {
            var result = Clone();
            result.Player = player;
            result.Scenario = scenario;
            return result;
        }

        public override string ToString()
        {
            var writer = new Writer();
            writer.Write('R');
            writer.WriteDigit(Version);
            writer.WriteDigit(Game);
            writer.WriteDigit(GameVariant);
            writer.Write('-');
            writer.Write(20, Seed);
            writer.Write('-');

            writer.Write(ProtectFromSoftLock);
            writer.Write(RandomDoors);
            writer.Write(RandomNPCs);
            writer.Write(RandomEnemies);
            writer.Write(RandomItems);

            writer.Write(RandomBgm);
            writer.Write(ShuffleItems);
            writer.Write(AlternativeRoutes);
            writer.Write(IncludeDocuments);
            writer.Write(ChangePlayer);

            writer.WriteDigit(RatioAmmo);
            writer.WriteDigit(RatioHealth);
            writer.WriteDigit(RatioInkRibbons);

            writer.Write(3, AmmoQuantity);
            writer.Write(2, EnemyDifficulty);

            writer.Write(2, AreaCount);
            writer.Write(3, AreaSize);

            writer.Write(IncludeBGMRE1);
            writer.Write(IncludeBGMRE2);
            writer.Write(false);
            writer.Write(false);
            writer.Write(IncludeBGMOther);

            writer.Write(IncludeNPCRE1);
            writer.Write(IncludeNPCRE2);
            writer.Write(false);
            writer.Write(false);
            writer.Write(IncludeNPCOther);

            writer.WriteDigit(Player0);
            writer.WriteDigit(Player1);
            return writer.ToString();
        }

        private static char ToPwdChar(byte c)
        {
            return _pwdChars[c];
        }

        public int Scenario
        {
            get => GameVariant & 1;
            set => GameVariant = (byte)((GameVariant & ~1) | (value & 1));
        }
        
        public int Player
        {
            get => (GameVariant >> 1) & 1;
            set => GameVariant = (byte)((GameVariant & ~2) | ((value & 1) << 1));
        }

        private class Reader
        {
            private string _s;
            private int _index;
            private int _current;
            private int _bitsRemaining;

            public Reader(string s)
            {
                _s = s;
            }

            private byte PeekByte()
            {
                if (_index < _s.Length)
                {
                    var c = _s[_index++];
                    if (c == '-')
                        return PeekByte();

                    var v = Array.IndexOf(_pwdChars, char.ToUpper(c));
                    var result = v == -1 ? (byte)0 : (byte)v;
                    return result;
                }
                return 0;
            }

            private void EnsureBits(int count)
            {
                while (_bitsRemaining < count)
                {
                    var next = PeekByte();
                    _current |= next << _bitsRemaining;
                    _bitsRemaining += 5;
                }
            }

            public bool ReadFlag() => ReadInt32(1) == 1;

            public byte ReadDigit() => (byte)ReadInt32(5);

            public byte ReadByte(int numBits) => (byte)ReadInt32(numBits);

            public int ReadInt32(int numBits)
            {
                EnsureBits(numBits);
                var result = _current & ((1 << numBits) - 1);
                _current >>= numBits;
                _bitsRemaining -= numBits;
                return result;
            }
        }

        private class Writer
        {
            private StringBuilder _sb = new StringBuilder();
            private int _current;
            private int _currentBitCount;

            public void Write(bool value) => Write(1, value ? 1 : 0);

            public void WriteDigit(byte value) => Write(5, value);

            public void Write(int numBits, int value)
            {
                var mask = (1 << numBits) - 1;
                value &= mask;
                _current |= value << _currentBitCount;
                _currentBitCount += numBits;
                Flush();
            }

            public void Write(char c)
            {
                _sb.Append(c);
            }

            private void Flush()
            {
                while (_currentBitCount >= 5)
                {
                    var value = (byte)(_current & 0b11111);
                    var c = ToPwdChar(value);
                    _sb.Append(c);
                    _current >>= 5;
                    _currentBitCount -= 5;
                }
            }

            public override string ToString() => _sb.ToString();
        }
    }
}
