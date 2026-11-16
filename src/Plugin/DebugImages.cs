using System;
using System.IO;
using SpellBrigadeMinimap.Core;

namespace SpellBrigadeMinimap
{
    /// <summary>Saves the minimap's working pictures as PNGs when [Debug] SaveDebugImages is on.</summary>
    internal static class DebugImages
    {
        static string runStamp = "run";

        public static void BeginRun() => runStamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");

        public static void Save(string name, PixelBuffer img)
        {
            if (img == null || !Mod.Settings.SaveDebugImages.Value) return;
            try
            {
                string folder = Path.Combine(BepInEx.Paths.BepInExRootPath, "minimap-debug");
                Directory.CreateDirectory(folder);
                string path = Path.Combine(folder, $"{runStamp}-{name}.png");
                File.WriteAllBytes(path, PngWriter.Encode(img));
                Mod.Log.LogInfo($"Saved {path}");
            }
            catch (Exception e) { LogOnce.Error("saving a debug picture", e); }
        }
    }
}
