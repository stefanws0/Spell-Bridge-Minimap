using System.Collections.Generic;

namespace SpellBrigadeMinimap.Core
{
    /// <summary>
    /// A 3 x 5 pixel font for short counters ("3/8") on a rounded pill, matching the minimap's pixel look.
    /// Only digits and '/' exist; other characters are skipped.
    /// </summary>
    public static class PixelFont
    {
        // Rows top to bottom, 3 bits each (left = 4).
        static readonly Dictionary<char, int[]> Glyphs = new Dictionary<char, int[]>
        {
            ['0'] = new[] { 7, 5, 5, 5, 7 },
            ['1'] = new[] { 2, 6, 2, 2, 7 },
            ['2'] = new[] { 7, 1, 7, 4, 7 },
            ['3'] = new[] { 7, 1, 7, 1, 7 },
            ['4'] = new[] { 5, 5, 7, 1, 1 },
            ['5'] = new[] { 7, 4, 7, 1, 7 },
            ['6'] = new[] { 7, 4, 7, 5, 7 },
            ['7'] = new[] { 7, 1, 2, 2, 2 },
            ['8'] = new[] { 7, 5, 7, 5, 7 },
            ['9'] = new[] { 7, 5, 7, 1, 7 },
            ['/'] = new[] { 1, 1, 2, 4, 4 },
        };

        /// <summary>
        /// Draws <paramref name="text"/> in blocks of <paramref name="scale"/> x <paramref name="scale"/> pixels on a
        /// pill of <paramref name="bg"/>, with padding in blocks (<paramref name="padLeft"/> extra on the left, room
        /// for an icon). Row 0 is the bottom, like every PixelBuffer.
        /// </summary>
        public static PixelBuffer Render(string text, int scale, Rgba fg, Rgba bg, int padX, int padY, int padLeft)
        {
            var glyphs = new List<int[]>();
            foreach (char ch in text ?? "")
                if (Glyphs.TryGetValue(ch, out var g)) glyphs.Add(g);
            int textW = glyphs.Count > 0 ? glyphs.Count * 4 - 1 : 0;
            int wBlocks = padLeft + padX * 2 + textW, hBlocks = 5 + padY * 2;
            var img = new PixelBuffer(wBlocks * scale, hBlocks * scale);

            // Pill: a rectangle with round ends (radius = half the height).
            float r = img.Height * 0.5f;
            for (int y = 0; y < img.Height; y++)
                for (int x = 0; x < img.Width; x++)
                {
                    float px = x + 0.5f, py = y + 0.5f;
                    float cx = px < r ? r : px > img.Width - r ? img.Width - r : px;
                    float dx = px - cx, dy = py - r;
                    if (dx * dx + dy * dy <= r * r) img.SetPixel(x, y, bg);
                }

            for (int i = 0; i < glyphs.Count; i++)
                for (int row = 0; row < 5; row++)
                    for (int col = 0; col < 3; col++)
                    {
                        if ((glyphs[i][row] & (4 >> col)) == 0) continue;
                        int bx = padLeft + padX + i * 4 + col, by = padY + (4 - row);
                        for (int sy = 0; sy < scale; sy++)
                            for (int sx = 0; sx < scale; sx++)
                                img.SetPixel(bx * scale + sx, by * scale + sy, fg);
                    }
            return img;
        }
    }
}
