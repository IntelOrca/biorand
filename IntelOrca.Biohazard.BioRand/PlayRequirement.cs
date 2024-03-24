using System;
using System.Text.RegularExpressions;

namespace IntelOrca.Biohazard.BioRand
{
    internal readonly struct PlayRequirement
    {
        public PlayRequirementKind Kind { get; }
        public int Id { get; }

        public PlayRequirement(PlayRequirementKind kind, int id)
        {
            Kind = kind;
            Id = id;
        }

        public string ToString(IItemHelper? helper)
        {
            if (helper != null && Kind == PlayRequirementKind.Key)
                return $"key({helper.GetItemName((byte)Id)})";
            return ToString();
        }

        public override string ToString() => $"{Kind.ToString().ToLower()}({Id})";

        public static PlayRequirement Parse(object input)
        {
            if (!TryParse(input, out var result))
                throw new ArgumentException(nameof(input));
            return result;
        }

        public static bool TryParse(object input, out PlayRequirement result)
        {
            result = default;

            if (input is int i)
            {
                result = new PlayRequirement(PlayRequirementKind.Key, i);
                return true;
            }

            if (!(input is string s))
                return false;

            var match = Regex.Match(s, @"(\w+)\((\d+)\)");
            if (!match.Success)
                throw new ArgumentException(nameof(input));

            if (!Enum.TryParse<PlayRequirementKind>(match.Groups[1].Value, true, out var kind))
                return false;

            if (!int.TryParse(match.Groups[2].Value, out var id))
                return false;

            result = new PlayRequirement(kind, id);
            return true;
        }
    }
}
