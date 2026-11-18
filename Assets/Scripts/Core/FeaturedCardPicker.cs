using System;

namespace Overlink.Core
{
    /// <summary>Chooses which featured card the main menu shows. Kept free of engine types so it can be unit tested.</summary>
    public static class FeaturedCardPicker
    {
        public static int PickIndex(int count, Func<int, int> randomBelow)
        {
            if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
            if (randomBelow == null) throw new ArgumentNullException(nameof(randomBelow));

            int index = randomBelow(count);
            if (index < 0 || index >= count)
                throw new InvalidOperationException("randomBelow returned " + index + " for count " + count + ".");
            return index;
        }

        public static string ShortFactionToken(Faction faction)
        {
            switch (faction)
            {
                case Faction.Fsti: return "FSTI";
                case Faction.Frti: return "FRTI";
                case Faction.Fpb: return "FPB";
                default: throw new ArgumentOutOfRangeException(nameof(faction), faction, "Unknown faction.");
            }
        }

        public static string FullFactionName(Faction faction)
        {
            switch (faction)
            {
                case Faction.Fsti: return "Frontier Science and Technology Institute";
                case Faction.Frti: return "Frontier Research and Technology Institute";
                case Faction.Fpb: return "Frontier Planning Bureau";
                default: throw new ArgumentOutOfRangeException(nameof(faction), faction, "Unknown faction.");
            }
        }
    }
}
