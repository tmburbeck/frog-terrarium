namespace FrogTerrarium.Simulation
{
    /// <summary>A tank size the player can upgrade to.</summary>
    public sealed class TankSize
    {
        public string Name;
        /// <summary>Half the width and depth in metres (the tank runs from -HalfX to +HalfX).</summary>
        public float HalfX, HalfZ;
        /// <summary>Price in money mode.</summary>
        public float Cost;
        public string Size => (2f * HalfX).ToString("0") + " × " + (2f * HalfZ).ToString("0") + " m";
    }

    /// <summary>
    /// A style set: the colours of the ground (soil, moss, sand, rock), the backdrop behind the tank,
    /// and a matching palette offered when colouring parts. Purely looks: it never changes how
    /// the tank works.
    /// </summary>
    public sealed class TankStyle
    {
        public string Name;
        public string Description;
        public float Cost;
        /// <summary>Soil, moss, sand and rock colours, as red, green, blue bytes (12 values).</summary>
        public byte[] Ground;
        /// <summary>The backdrop colour behind the tank.</summary>
        public byte BackR, BackG, BackB;
        /// <summary>Six part colours that go with the style (18 values).</summary>
        public byte[] Palette;
    }

    public static class TankLooks
    {
        /// <summary>The sizes, smallest first. A new tank is the first one.</summary>
        public static readonly TankSize[] Sizes =
        {
            new TankSize { Name = "Standard", HalfX = 10f, HalfZ = 6f, Cost = 0f },
            new TankSize { Name = "Large", HalfX = 13f, HalfZ = 8f, Cost = 500f },
            new TankSize { Name = "Huge", HalfX = 16f, HalfZ = 10f, Cost = 1200f },
        };

        /// <summary>The style sets. Never reorder: saves store the number. Add new ones to the end.</summary>
        public static readonly TankStyle[] Styles =
        {
            new TankStyle { Name = "Classic", Cost = 0f, Description = "Brown soil, green moss, pale sand and grey stone.",
                Ground = new byte[] { 84, 61, 41, 64, 102, 36, 189, 168, 122, 117, 115, 110 }, BackR = 31, BackG = 41, BackB = 36,
                Palette = new byte[] { 130, 125, 118, 110, 75, 45, 60, 130, 55, 196, 170, 120, 80, 78, 74, 150, 190, 70 } },
            new TankStyle { Name = "Rainforest", Cost = 150f, Description = "Dark rich earth, vivid moss and mossy stone in a deep green gloom.",
                Ground = new byte[] { 52, 36, 24, 46, 128, 40, 150, 120, 80, 86, 104, 80 }, BackR = 14, BackG = 38, BackB = 22,
                Palette = new byte[] { 40, 110, 45, 90, 160, 60, 70, 45, 30, 200, 60, 50, 240, 180, 40, 110, 70, 150 } },
            new TankStyle { Name = "Desert", Cost = 150f, Description = "Orange earth, dry olive moss, pale sand and red rock.",
                Ground = new byte[] { 160, 92, 48, 120, 120, 60, 226, 200, 150, 168, 78, 52 }, BackR = 92, BackG = 70, BackB = 50,
                Palette = new byte[] { 200, 120, 70, 230, 205, 160, 150, 60, 40, 120, 130, 70, 90, 70, 55, 240, 230, 210 } },
            new TankStyle { Name = "Riverbank", Cost = 150f, Description = "Grey silt, bright moss, pebbles and blue-grey stone.",
                Ground = new byte[] { 96, 88, 76, 70, 140, 60, 170, 166, 150, 104, 116, 128 }, BackR = 40, BackG = 54, BackB = 66,
                Palette = new byte[] { 120, 130, 140, 170, 165, 150, 80, 100, 70, 60, 90, 120, 140, 110, 80, 220, 220, 210 } },
            new TankStyle { Name = "Volcanic", Cost = 200f, Description = "Black earth, lime moss, grey ash and dark red rock.",
                Ground = new byte[] { 30, 28, 28, 120, 170, 40, 110, 108, 104, 100, 36, 30 }, BackR = 28, BackG = 16, BackB = 14,
                Palette = new byte[] { 40, 38, 38, 140, 40, 30, 230, 110, 30, 120, 170, 40, 90, 90, 90, 250, 200, 60 } },
            new TankStyle { Name = "Moonlit", Cost = 200f, Description = "Blue-black earth, teal moss, silver sand and slate, under night blue.",
                Ground = new byte[] { 40, 44, 62, 40, 110, 100, 170, 178, 190, 84, 92, 110 }, BackR = 10, BackG = 14, BackB = 34,
                Palette = new byte[] { 70, 80, 120, 40, 120, 110, 190, 195, 210, 120, 90, 170, 30, 34, 50, 230, 230, 170 } },
        };

        /// <summary>The biggest listed size the tank already is (0 for smaller tanks).</summary>
        public static int SizeIndex(World w)
        {
            int best = 0;
            for (int k = 0; k < Sizes.Length; k++)
                if (w.HalfSizeX >= Sizes[k].HalfX - 1e-3f && w.HalfSizeZ >= Sizes[k].HalfZ - 1e-3f) best = k;
            return best;
        }

        /// <summary>The next bigger size, or null if the tank is already the biggest.</summary>
        public static TankSize NextSize(World w)
        {
            foreach (var s in Sizes)
                if (s.HalfX > w.HalfSizeX + 1e-3f && s.HalfZ > w.HalfSizeZ + 1e-3f) return s;
            return null;
        }

        /// <summary>
        /// Upgrades the tank to the next size, paying in money mode. The old tank sits in the middle of
        /// the new one, so nothing moves; the new ground around it is flat soil. Returns false and why
        /// if it is already the biggest or there is not enough money.
        /// </summary>
        public static bool Upgrade(World w, out string why)
        {
            why = "";
            var next = NextSize(w);
            if (next == null) { why = "This is already the biggest tank."; return false; }
            if (!w.Economy.Spend(next.Cost, "A " + next.Name.ToLowerInvariant() + " tank", out why)) return false;
            w.ResizeTank(next.HalfX, next.HalfZ);
            return true;
        }

        public static bool StyleOwned(World w, int style) =>
            style == 0 || !w.Economy.Enabled || (w.Economy.StylesOwned & (1u << style)) != 0;

        /// <summary>Switches to a style set, buying it first in money mode if needed.</summary>
        public static bool UseStyle(World w, int style, out string why)
        {
            why = "";
            if (style < 0 || style >= Styles.Length) { why = "No such style."; return false; }
            if (!StyleOwned(w, style))
            {
                if (!w.Economy.Spend(Styles[style].Cost, "The " + Styles[style].Name + " style", out why)) return false;
                w.Economy.StylesOwned |= 1u << style;
            }
            w.Style = (byte)style;
            return true;
        }
    }
}
