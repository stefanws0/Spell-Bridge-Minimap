namespace SpellBrigadeMinimap.Core
{
    /// <summary>An 8-bit RGBA color, laid out like Unity's RGBA32 texture format.</summary>
    public readonly struct Rgba
    {
        public readonly byte R, G, B, A;

        public Rgba(byte r, byte g, byte b, byte a = 255)
        {
            R = r; G = g; B = b; A = a;
        }

        public static readonly Rgba Clear = new Rgba(0, 0, 0, 0);
        public static readonly Rgba White = new Rgba(255, 255, 255, 255);

        public override string ToString() => $"({R},{G},{B},{A})";
    }
}
