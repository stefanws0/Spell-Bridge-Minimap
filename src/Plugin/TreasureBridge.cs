using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using NVec2 = System.Numerics.Vector2;

namespace SpellBrigadeMinimap
{
    /// <summary>What the minimap draws of a treasure hunt.</summary>
    internal sealed class TreasureView
    {
        public bool Active;
        public NVec2 Center;
        public float Radius;
        public readonly List<NVec2> Pieces = new List<NVec2>();
        public Sprite Icon, Circle;
        public Color Orange = new Color(1f, 0.55f, 0.16f, 1f);
    }

    /// <summary>
    /// Reads Treasure Maps' public static SpellBrigadeTreasureMaps.TreasureApi by reflection when that mod is
    /// loaded (ApiVersion 1): HuntActive, CircleCenter, CircleRadius, PiecesOnGround, PieceIcon, CircleSprite, Orange.
    /// </summary>
    internal sealed class TreasureBridge
    {
        bool resolved, available;
        FieldInfo active, center, radius, pieces, icon, circle, orange;

        public void Read(TreasureView into)
        {
            into.Active = false;
            into.Pieces.Clear();
            if (!resolved) Resolve();
            if (!available) return;
            try
            {
                into.Active = (bool)active.GetValue(null);
                var c = (Vector3)center.GetValue(null);
                into.Center = new NVec2(c.x, c.z);
                into.Radius = (float)radius.GetValue(null);
                if (pieces.GetValue(null) is IList list)
                    foreach (object o in list)
                        if (o is Vector3 p) into.Pieces.Add(new NVec2(p.x, p.z));
                into.Icon = icon.GetValue(null) as Sprite;
                into.Circle = circle.GetValue(null) as Sprite;
                into.Orange = (Color)orange.GetValue(null);
            }
            catch (Exception e)
            {
                available = false;
                LogOnce.Error("reading the treasure hunt (treasure layer turned off)", e);
            }
        }

        void Resolve()
        {
            resolved = true;
            var asm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "SpellBrigadeTreasureMaps");
            if (asm == null)
            {
                Mod.Log.LogInfo("Treasure Maps isn't installed; the treasure layer is off.");
                return;
            }
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Static;
            var api = asm.GetType("SpellBrigadeTreasureMaps.TreasureApi");
            var version = api?.GetField("ApiVersion", flags);
            if (version == null || !(version.GetValue(null) is int v) || v != 1)
            {
                LogOnce.Warn("treasure-api", "Treasure Maps is installed but its version doesn't match this minimap; the treasure layer is off.");
                return;
            }
            active = api.GetField("HuntActive", flags);
            center = api.GetField("CircleCenter", flags);
            radius = api.GetField("CircleRadius", flags);
            pieces = api.GetField("PiecesOnGround", flags);
            icon = api.GetField("PieceIcon", flags);
            circle = api.GetField("CircleSprite", flags);
            orange = api.GetField("Orange", flags);
            available = active != null && center != null && radius != null && pieces != null && icon != null
                        && circle != null && orange != null;
            if (available) Mod.Log.LogInfo("Treasure Maps found; showing treasure hunts.");
            else LogOnce.Warn("treasure-shape", "Treasure Maps is installed but its fields changed; the treasure layer is off.");
        }
    }
}
