using System.Collections.Generic;
using SpellBrigadeMinimap.Core;
using UnityEngine;
using UnityEngine.UI;
using NVec2 = System.Numerics.Vector2;

namespace SpellBrigadeMinimap
{
    /// <summary>Owns the minimap's UI objects and draws one frame from scanned data.</summary>
    internal sealed class MinimapView
    {
        const int DotTextureSize = 192;
        const float DotOutline = 1.2f;   // texels
        // Per EnemyRank (Small, Big, MiniBoss): radius in texels and colour.
        static readonly float[] RankRadius = { 1.85f, 2.6f, 3.3f };
        static readonly Rgba[] RankColor = { new Rgba(235, 64, 52), new Rgba(255, 150, 40), new Rgba(180, 90, 255) };
        const float BossSize = 26f;
        static readonly Rgba EnemyOutline = new Rgba(20, 20, 22, 235);
        const float SquareBorder = 2f;
        static readonly Color FrameColor = new Color(0.85f, 0.88f, 0.95f, 0.9f);
        static readonly Color Background = new Color(0.09f, 0.09f, 0.1f, 0.92f);
        static readonly Color BadgeColor = new Color(0.08f, 0.08f, 0.09f, 0.95f);
        static readonly Color Cream = new Color(253f / 255f, 248f / 255f, 206f / 255f, 1f);
        static readonly Color HealthColor = new Color(0.95f, 0.25f, 0.3f, 1f);
        static readonly Color ObjectiveColor = new Color(1f, 0.85f, 0.3f, 1f);
        static readonly Color BossColor = new Color(0.8f, 0.2f, 1f, 1f);
        static readonly Color DownedColor = new Color(1f, 0.35f, 0.35f, 1f);
        static readonly Color DangerColor = new Color(1f, 0.3f, 0.2f, 1f);
        static readonly Color PickupColor = new Color(0.5f, 1f, 0.6f, 1f);
        static readonly Color PortalOpen = new Color(0.62f, 0.99f, 0.98f, 1f);     // the portals' cyan swirl in game
        static readonly Color PortalClosed = new Color(0.62f, 0.99f, 0.98f, 0.35f);
        const float GeyserSize = 16f, SpentGeyserSize = 14f, EruptingGeyserSize = 19f, PortalSize = 18f, TornadoSize = 18f;

        readonly MinimapConfig cfg;
        readonly GameObject root;
        readonly RectTransform frame, panel, mapRoot;
        readonly Image frameImg, panelImg, bezel;
        readonly RectMask2D squareMask;
        readonly Mask roundMask;
        readonly CanvasGroup group;
        readonly RawImage layout;
        readonly IconPool zones, icons;
        readonly Image you;
        readonly PixelBuffer dotPixels = new PixelBuffer(DotTextureSize, DotTextureSize);
        readonly List<(float x, float y, int rank)> dotSpots = new List<(float, float, int)>();
        readonly Texture2D dotTexture;
        readonly Sprite disc, ring, arrow, diamond, roundShape, badgeRing, burst, burstErupting, burstSpent, portal, tornado, boss;

        BakedMap map;
        bool visible = true, dotsEmpty, round;
        float panelInset = SquareBorder;
        float lastSize = -1f, lastMargin = -1f, lastBottomOffset = -1f;
        ScreenCorner lastCorner = (ScreenCorner)(-1);
        MapShape lastShape = (MapShape)(-1);

        public MinimapView(MinimapConfig cfg)
        {
            this.cfg = cfg;
            disc = SpriteFactory.ToSprite(ShapeSprites.Disc(32, 0.12f));
            ring = SpriteFactory.ToSprite(ShapeSprites.Ring(128, 0.03f));
            arrow = SpriteFactory.ToSprite(ShapeSprites.Arrow(32));
            diamond = SpriteFactory.ToSprite(ShapeSprites.Diamond(32, 0.1f));
            roundShape = SpriteFactory.ToSprite(ShapeSprites.Disc(256, 0f));
            badgeRing = SpriteFactory.ToSprite(ShapeSprites.Ring(64, 0.12f));
            burst = SpriteFactory.ToSprite(ShapeSprites.Burst(48));
            burstErupting = SpriteFactory.ToSprite(ShapeSprites.Burst(48, new Rgba(255, 140, 30), new Rgba(214, 80, 12)));
            burstSpent = SpriteFactory.ToSprite(ShapeSprites.Burst(48, new Rgba(150, 152, 158), new Rgba(112, 114, 120)));
            portal = SpriteFactory.ToSprite(ShapeSprites.Portal(48));
            tornado = SpriteFactory.ToSprite(ShapeSprites.Tornado(48));
            boss = SpriteFactory.ToSprite(ShapeSprites.BossSkull(64, new Rgba(222, 58, 58)));

            root = new GameObject("SpellBrigadeMinimap");
            Object.DontDestroyOnLoad(root);
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 300;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;

            // Square: the frame is a filled square in the border colour and the panel is inset by a thin border.
            // Round: the frame is invisible, the panel is a disc inset to the bezel's inner edge, and the bezel
            // (gold rim + inner shadow) is drawn on top. The panel masks everything inside it either way.
            frameImg = UiBuilder.NewImage("Frame", root.transform);
            frameImg.color = FrameColor;
            frame = frameImg.rectTransform;
            group = frame.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            panelImg = UiBuilder.NewImage("Panel", frame);
            panelImg.color = Background;
            panel = panelImg.rectTransform;
            UiBuilder.Stretch(panel, SquareBorder);
            squareMask = panel.gameObject.AddComponent<RectMask2D>();
            roundMask = panel.gameObject.AddComponent<Mask>();
            roundMask.showMaskGraphic = true;

            mapRoot = UiBuilder.NewRect("MapRoot", panel);
            layout = UiBuilder.NewRawImage("Layout", mapRoot);
            layout.enabled = false;

            zones = new IconPool(UiBuilder.NewRect("Zones", panel));

            var dots = UiBuilder.NewRawImage("Enemies", panel);
            UiBuilder.Stretch(dots.rectTransform, 0f);
            dotTexture = SpriteFactory.ToTexture(dotPixels, FilterMode.Bilinear, keepReadable: true);
            dots.texture = dotTexture;

            icons = new IconPool(UiBuilder.NewRect("Icons", panel));

            you = UiBuilder.NewImage("You", panel);
            you.sprite = arrow;
            you.rectTransform.sizeDelta = new Vector2(18f, 18f);

            bezel = UiBuilder.NewImage("Bezel", frame); // after the panel, so it draws on top of the map
            bezel.sprite = SpriteFactory.ToSprite(ShapeSprites.Bezel(384));
            UiBuilder.Stretch(bezel.rectTransform, 0f);

            ApplyLayout();
        }

        public void SetVisible(bool show)
        {
            if (show == visible) return;
            visible = show;
            root.SetActive(show);
        }

        public void SetMap(BakedMap baked)
        {
            map = baked;
            layout.texture = baked != null ? baked.Texture : null;
            layout.enabled = baked != null;
        }

        public void Render(WorldScanner s, List<EventMarker> markers, ObjectiveDiscovery discovery, float yaw, float radius)
        {
            ApplyLayout();
            group.alpha = cfg.Opacity.Value;
            float half = (cfg.Size.Value - 2f * panelInset) * 0.5f;
            var view = new MapView(s.Center, yaw, radius, half);
            float scale = view.Scale;

            // Level layout: a world-aligned image under a root turned by the yaw (matches MapMath.WorldToPanel).
            mapRoot.localEulerAngles = new Vector3(0f, 0f, yaw);
            if (map != null)
            {
                var lt = layout.rectTransform;
                lt.sizeDelta = new Vector2(map.WorldWidth * scale, map.WorldHeight * scale);
                float dx = map.OriginX + map.WorldWidth * 0.5f - s.Center.X;
                float dz = map.OriginZ + map.WorldHeight * 0.5f - s.Center.Y;
                lt.anchoredPosition = new Vector2(dx * scale, dz * scale);
            }

            zones.Begin();
            if (cfg.ShowMoreEvents.Value)
                foreach (var m in markers)
                    if (m.Zone && m.Radius > 0f)
                        zones.Place(ring, m.Color, ToUnity(MapMath.WorldToPanel(view, m.Pos)), m.Radius * 2f * scale, 0f);
            if (cfg.ShowMapFeatures.Value)
                foreach (var g in s.Geysers)
                    if (g.State != GeyserState.Erupting)
                        DrawFeature(zones, g.State == GeyserState.Ready ? burst : burstSpent, Color.white, g.Pos, view,
                            g.State == GeyserState.Ready ? GeyserSize : SpentGeyserSize);
            zones.End();

            DrawEnemies(s, view);

            icons.Begin();
            if (cfg.ShowMapFeatures.Value)
                foreach (var f in s.Features)
                    if (f.Kind == FeatureKind.Portal)
                        DrawFeature(icons, portal, f.Active ? PortalOpen : PortalClosed, f.Pos, view, PortalSize);
            if (cfg.ShowMapFeatures.Value)
            {
                foreach (var t in s.Tornadoes) DrawFeature(icons, tornado, Color.white, t, view, TornadoSize);
                // Erupting geysers: orange, above the enemy dots, shaking a little.
                float time = Time.unscaledTime;
                foreach (var g in s.Geysers)
                    if (g.State == GeyserState.Erupting)
                        DrawFeature(icons, burstErupting, Color.white, g.Pos, view, EruptingGeyserSize,
                            new Vector2(Mathf.Sin(time * 53f) * 1.3f, Mathf.Cos(time * 61f) * 1.1f), Mathf.Sin(time * 41f) * 7f);
            }
            foreach (var poi in s.Pois)
                if (!(poi.Kind == PoiKind.Boss && s.Bosses.Count > 0 && cfg.ShowEnemies.Value)) // the boss icon below replaces the game's pointer
                    DrawPoi(poi, view, discovery);
            if (cfg.ShowEnemies.Value)
                foreach (var b in s.Bosses) DrawPoint(boss, Color.white, MapMath.WorldToPanel(view, b), BossSize, half, clamp: true);
            if (cfg.ShowMoreEvents.Value)
                foreach (var m in markers)
                    if (!m.Zone) DrawPoint(diamond, m.Color, MapMath.WorldToPanel(view, m.Pos), 16f, half, clamp: true);
            if (cfg.ShowTeammates.Value)
                foreach (var p in s.Players)
                {
                    Color c = p.Color;
                    if (p.Ghost) c.a = 0.5f;
                    DrawPoint(disc, c, MapMath.WorldToPanel(view, p.Pos), p.Ghost ? 10f : 13f, half, clamp: true);
                }
            icons.End();

            you.rectTransform.localEulerAngles = new Vector3(0f, 0f, MapMath.HeadingToPanelAngle(s.Facing, yaw));
        }

        void DrawEnemies(WorldScanner s, in MapView view)
        {
            bool any = false;
            dotPixels.Clear();
            if (cfg.ShowEnemies.Value)
            {
                // Dark outlines first, then the dots, so crowded dots don't cover each other's colour and every dot
                // stands out on any ground (the ice map's red bushes included).
                float toPx = DotTextureSize / (2f * view.HalfSize);
                dotSpots.Clear();
                foreach (var e in s.Enemies)
                {
                    NVec2 p = MapMath.WorldToPanel(view, e.Pos);
                    if (!MapMath.IsInside(p, view.HalfSize)) continue;
                    any = true;
                    float x = (p.X + view.HalfSize) * toPx;
                    float y = (p.Y + view.HalfSize) * toPx;
                    int rank = Mathf.Min((int)e.Rank, RankRadius.Length - 1);
                    dotSpots.Add((x, y, rank));
                    dotPixels.FillCircle(x, y, RankRadius[rank] + DotOutline, EnemyOutline);
                }
                // Small first, mini-bosses last, so the important ones stay on top in a crowd.
                for (int rank = 0; rank < RankRadius.Length; rank++)
                    foreach (var (x, y, r) in dotSpots)
                        if (r == rank) dotPixels.FillCircle(x, y, RankRadius[rank], RankColor[rank]);
            }
            if (!any && dotsEmpty) return; // already blank on the GPU
            SpriteFactory.Upload(dotTexture, dotPixels, keepReadable: true);
            dotsEmpty = !any;
        }

        void DrawPoi(Poi poi, in MapView view, ObjectiveDiscovery discovery)
        {
            if (poi.Target == null) return;
            bool pickup = poi.Kind == PoiKind.Pickup || poi.Kind == PoiKind.Health;
            if (pickup ? !cfg.ShowPickups.Value : !cfg.ShowObjectives.Value) return;
            if (poi.Kind == PoiKind.Objective && cfg.HideUndiscoveredObjectives.Value && !discovery.IsKnown(poi.Key)) return;

            Vector3 w = poi.Target.position;
            NVec2 p = MapMath.WorldToPanel(view, new NVec2(w.x, w.z));
            if (pickup)
            {
                // Pickups sit on a dark badge (the HUD's circle style) so they read on any ground;
                // health potions get a bigger badge with a cream ring.
                if (!(round ? MapMath.IsInsideCircle(p, view.HalfSize) : MapMath.IsInside(p, view.HalfSize))) return;
                bool health = poi.Kind == PoiKind.Health;
                float badge = health ? 24f : 18f;
                Vector2 at = ToUnity(p);
                icons.Place(disc, BadgeColor, at, badge, 0f);
                if (health) icons.Place(badgeRing, Cream, at, badge, 0f);
                if (poi.Sprite != null) icons.Place(poi.Sprite, poi.Color, at, badge * 0.7f, 0f);
                else icons.Place(disc, KindColor(poi.Kind), at, badge * 0.5f, 0f);
                return;
            }

            Sprite sprite = poi.Sprite != null ? poi.Sprite : diamond;
            Color color = poi.Sprite != null ? poi.Color : KindColor(poi.Kind);
            float size = poi.Kind == PoiKind.Boss ? 24f : 20f;
            DrawPoint(sprite, color, p, size, view.HalfSize, poi.ClampToEdge);
        }

        /// <summary>An upright icon for a fixed level feature, only while it's on the minimap.</summary>
        void DrawFeature(IconPool pool, Sprite sprite, Color color, NVec2 world, in MapView view, float size,
            Vector2 shake = default, float tilt = 0f)
        {
            NVec2 p = MapMath.WorldToPanel(view, world);
            if (!(round ? MapMath.IsInsideCircle(p, view.HalfSize) : MapMath.IsInside(p, view.HalfSize))) return;
            pool.Place(sprite, color, ToUnity(p) + shake, size, tilt);
        }

        /// <summary>Draws an icon; if it's off the panel and <paramref name="clamp"/> is set, pins it to the edge with an arrow.</summary>
        void DrawPoint(Sprite sprite, Color color, NVec2 p, float size, float half, bool clamp)
        {
            if (round ? MapMath.IsInsideCircle(p, half) : MapMath.IsInside(p, half))
            {
                icons.Place(sprite, color, ToUnity(p), size, 0f);
                return;
            }
            if (!clamp) return;
            NVec2 iconPos, arrowPos;
            float angle;
            if (round)
            {
                MapMath.ClampToCircle(p, half, size * 0.5f + 8f, out iconPos, out angle);
                MapMath.ClampToCircle(p, half, 5f, out arrowPos, out _);
            }
            else
            {
                MapMath.ClampToEdge(p, half, size * 0.5f + 8f, out iconPos, out angle);
                MapMath.ClampToEdge(p, half, 5f, out arrowPos, out _);
            }
            icons.Place(sprite, color, ToUnity(iconPos), size * 0.8f, 0f);
            icons.Place(arrow, color, ToUnity(arrowPos), 10f, angle - 90f);
        }

        static Color KindColor(PoiKind kind)
        {
            switch (kind)
            {
                case PoiKind.Boss: return BossColor;
                case PoiKind.Downed: return DownedColor;
                case PoiKind.Danger: return DangerColor;
                case PoiKind.Pickup: return PickupColor;
                case PoiKind.Health: return HealthColor;
                default: return ObjectiveColor;
            }
        }

        void ApplyLayout()
        {
            MapShape shape = cfg.Shape.Value;
            float size = cfg.Size.Value, margin = cfg.Margin.Value, bottomOffset = cfg.BottomOffset.Value;
            ScreenCorner corner = cfg.Corner.Value;
            if (shape == lastShape && size == lastSize && margin == lastMargin
                && bottomOffset == lastBottomOffset && corner == lastCorner) return;
            lastShape = shape;
            lastSize = size;
            lastMargin = margin;
            lastBottomOffset = bottomOffset;
            lastCorner = corner;

            round = shape == MapShape.Round;
            frameImg.enabled = !round;
            panelImg.sprite = round ? roundShape : null;
            roundMask.enabled = round;
            squareMask.enabled = !round;
            bezel.enabled = round;
            // Round: the map ends just under the bezel's inner edge (1 px overlap so no gap shows).
            panelInset = round ? Mathf.Max(1f, size * ShapeSprites.BezelThickness - 1f) : SquareBorder;
            UiBuilder.Stretch(panel, panelInset);

            bool right = corner == ScreenCorner.TopRight || corner == ScreenCorner.BottomRight;
            bool top = corner == ScreenCorner.TopLeft || corner == ScreenCorner.TopRight;
            var anchor = new Vector2(right ? 1f : 0f, top ? 1f : 0f);
            frame.anchorMin = anchor;
            frame.anchorMax = anchor;
            frame.pivot = anchor;
            frame.anchoredPosition = new Vector2(right ? -margin : margin, top ? -margin : margin + bottomOffset);
            frame.sizeDelta = new Vector2(size, size);
        }

        static Vector2 ToUnity(NVec2 v) => new Vector2(v.X, v.Y);
    }
}
