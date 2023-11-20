using System;
using System.Collections.Generic;
using System.Linq;

namespace IntelOrca.Biohazard.BioRand.Events
{
    public class PointOfInterest
    {
        public int Id { get; set; }
        public string[]? Tags { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }
        public int D { get; set; }
        public int Cut { get; set; }
        public int? CloseCut { get; set; }
        public int[]? Cuts { get; set; }
        public int[]? Edges { get; set; }

        public REPosition Position => new REPosition(X, Y, Z, D);

        public int[] AllCuts
        {
            get
            {
                var cuts = new List<int> { Cut };
                if (Cuts != null)
                    cuts.AddRange(Cuts);
                if (CloseCut != null)
                    cuts.Add(CloseCut.Value);
                return cuts.ToArray();
            }
        }

        public bool HasTag(string tag)
        {
            return Tags.Contains(tag);
        }

        public override string ToString() => $"Id = {Id} Tags = [{string.Join(", ", Tags)}] Cut = {Cut} Position = {Position}";
    }
}
