using System;
using System.Text;

namespace rer
{
    public class RandoConfig
    {
        private readonly static char[] _pwdChars = new[]
        {
            '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'E', 'F', 'G',
            'H', 'J', 'K', 'L', 'M', 'N', 'P', 'Q', 'R', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z',
        };

        public const int MaxSeed = 0b11111_11111_11111_11111;

        public byte Version { get; set; } = 0;
        public byte Game { get; set; } = 2;
        public byte GameVariant { get; set; } = 0;
        public int Seed { get; set; }

        // Flags
        public bool ProtectFromSoftLock { get; set; }
        public bool RandomDoors { get; set; }
        public bool RandomNPCs { get; set; } = true;
        public bool RandomEnemies { get; set; } = true;
        public bool RandomItems { get; set; } = true;
        public bool RandomBgm { get; set; } = true;
        public bool ShuffleItems { get; set; } = true;
        public bool AlternativeRoutes { get; set; } = true;
        public bool IncludeDocuments { get; set; } = true;

        // Numbers
        public byte RatioAmmo { get; set; } = 16;
        public byte RatioHealth { get; set; } = 16;
        public byte RatioInkRibbons { get; set; } = 16;
        public byte AmmoQuantity { get; set; } = 4;
        public byte EnemyDifficulty { get; set; } = 2;

        public static RandoConfig FromString(string code)
        {
            var chars = code?.ToCharArray() ?? new char[0];
            Array.Resize(ref chars, 16);

            var result = new RandoConfig();
            // result.Version = ParseSingle(chars[1]);
            // result.Game = ParseSingle(chars[2]);
            result.GameVariant = Math.Min(ParseSingle(chars[3]), (byte)1);

            result.Seed = ParseMany(chars, 5, 4);

            result.SetFlags0(ParseSingle(chars[10]));
            result.SetFlags1(ParseSingle(chars[11]));
            result.RatioAmmo = ParseSingle(chars[12]);
            result.RatioHealth = ParseSingle(chars[13]);
            result.RatioInkRibbons = ParseSingle(chars[14]);
            result.SetD4(ParseSingle(chars[15]));

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
            var sb = new StringBuilder();
            sb.Append('R');
            AppendSingle(sb, Version);
            AppendSingle(sb, Game);
            AppendSingle(sb, GameVariant);
            sb.Append('-');
            AppendMany(sb, Seed, 20);
            sb.Append('-');
            AppendSingle(sb, GetFlags0());
            AppendSingle(sb, GetFlags1());
            AppendSingle(sb, RatioAmmo);
            AppendSingle(sb, RatioHealth);
            AppendSingle(sb, RatioInkRibbons);
            AppendSingle(sb, GetD4());
            return sb.ToString();
        }

        private void SetFlags0(byte value)
        {
            ProtectFromSoftLock = (value & (1 << 0)) != 0;
            RandomDoors = (value & (1 << 1)) != 0;
            RandomNPCs = (value & (1 << 2)) != 0;
            RandomEnemies = (value & (1 << 3)) != 0;
            RandomItems = (value & (1 << 4)) != 0;
        }

        private byte GetFlags0()
        {
            var result = 0;
            if (ProtectFromSoftLock)
                result |= 1 << 0;
            if (RandomDoors)
                result |= 1 << 1;
            if (RandomNPCs)
                result |= 1 << 2;
            if (RandomEnemies)
                result |= 1 << 3;
            if (RandomItems)
                result |= 1 << 4;
            return (byte)result;
        }

        private void SetFlags1(byte value)
        {
            RandomBgm = (value & (1 << 0)) != 0;
            ShuffleItems = (value & (1 << 1)) != 0;
            AlternativeRoutes = (value & (1 << 2)) != 0;
            IncludeDocuments = (value & (1 << 3)) != 0;
        }

        private byte GetFlags1()
        {
            var result = 0;
            if (RandomBgm)
                result |= 1 << 0;
            if (ShuffleItems)
                result |= 1 << 1;
            if (AlternativeRoutes)
                result |= 1 << 2;
            if (IncludeDocuments)
                result |= 1 << 3;
            return (byte)result;
        }

        private void SetD4(byte value)
        {
            AmmoQuantity = (byte)(value & 0b111);
            EnemyDifficulty = (byte)((value & 0b11000) >> 3);
        }

        private byte GetD4()
        {
            var result = 0;
            result |= AmmoQuantity & 0b111;
            result |= (EnemyDifficulty & 0b11) << 3;
            return (byte)result;
        }

        private static byte ParseSingle(char c)
        {
            var index = Array.IndexOf(_pwdChars, char.ToUpper(c));
            return index == -1 ? (byte)0 : (byte)index;
        }

        private static int ParseMany(char[] chars, int offset, int length)
        {
            var result = 0;
            for (int i = offset + length - 1; i >= offset; i--)
            {
                var c = chars[i];
                result <<= 5;
                result |= ParseSingle(c);
            }
            return result;
        }

        private static void AppendSingle(StringBuilder sb, int value)
        {
            AppendMany(sb, value, 5);
        }

        private static void AppendMany(StringBuilder sb, int value, int numBits)
        {
            var n = value & ((1 << numBits) - 1);
            for (int i = 0; i < numBits; i += 5)
            {
                var chr = ToPwdChar((byte)(n & 0b11111));
                sb.Append(chr);
                n >>= 5;
            }
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
    }
}
