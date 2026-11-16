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
        readonly Sprite disc, ring, arrow, diamond, roundShape, badgeRing, burst, burstErupting, burstSpent, portal, tornado, boss, ghost;
        readonly HashSet<ulong> knownGroups = new HashSet<ulong>();
        readonly Sprite[] pieceSprites;
        readonly Sprite crosshair;
        readonly Dictionary<string, Sprite> counterSprites = new Dictionary<string, Sprite>();
        const float PieceSize = 15f, CrosshairSize = 26f, GuideDotSize = 3f, GuideDotSpacing = 7f;
        const int CounterScale = 2, CounterPadLeft = 6;
        static readonly Color ZoneColor = new Color(0.47f, 0.86f, 0.55f, 0.9f);
        static readonly Color ZoneFill = new Color(0.47f, 0.86f, 0.55f, 0.15f);
        static readonly Color GuideColor = new Color(0.99f, 0.97f, 0.81f, 0.55f);
        static readonly Color DonePiece = new Color(1f, 1f, 1f, 0.45f);
        static readonly Rgba CounterText = new Rgba(253, 248, 206);
        static readonly Rgba CounterPill = new Rgba(24, 24, 26, 235);
        const float GhostSize = 16f, PillarSize = 18f;
        // The order objective's mushrooms, as the game colours them: waiting, hit in the right order, wrong.
        static readonly Color PillarIdle = new Color(0.36f, 0.62f, 1f, 1f);
        static readonly Color PillarCorrect = new Color(0.36f, 0.95f, 0.45f, 1f);
        static readonly Color PillarWrong = new Color(1f, 0.32f, 0.3f, 1f);

        BakedMap map;
        bool visible = true, dotsEmpty, round;
        float panelInset = SquareBorder;
        float lastSize = -1f, lastMargin = -1f, lastBottomOffset = -1f, lastLift = -1f, lift;
        readonly HudAvoider hud = new HudAvoider();
        readonly Canvas canvas;
        const float HudGap = 8f;                 // canvas units kept between the game's panel and the minimap
        const float MaxHudMoveShare = 0.45f;     // never move more than this share of the screen height
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
            ghost = SpriteFactory.ToSprite(ShapeSprites.Ghost(48));
            // Indexed by PieceKind: Mushroom, Crate, Herd, Stone.
            pieceSprites = new[]
            {
                SpriteFactory.ToSprite(ShapeSprites.Mushroom(48)), SpriteFactory.ToSprite(ShapeSprites.Crate(48)),
                SpriteFactory.ToSprite(ShapeSprites.Paw(48)), SpriteFactory.ToSprite(ShapeSprites.Stone(48)),
            };
            crosshair = SpriteFactory.ToSprite(ShapeSprites.Crosshair(64));

            root = new GameObject("SpellBrigadeMinimap");
            Object.DontDestroyOnLoad(root);
            canvas = root.AddComponent<Canvas>();
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
            lift = cfg.AvoidGameHud.Value
                ? hud.Tick(Time.unscaledTime, Time.unscaledDeltaTime, HomeScreenBox(out bool bottom), bottom, canvas,
                    Mathf.Max(0.01f, canvas.scaleFactor), HudGap, MaxHudMoveShare * 1080f)
                : 0f;
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
            if (cfg.ShowObjectives.Value)
                foreach (var z in s.Objectives.Zones)
                {
                    // Push zone / herding pen at its real size: a soft fill and a green edge.
                    Vector2 at = ToUnity(MapMath.WorldToPanel(view, z.Center));
                    zones.Place(roundShape, ZoneFill, at, z.Radius * 2f * scale, 0f);
                    zones.Place(ring, ZoneColor, at, z.Radius * 2f * scale, 0f);
                }
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
            if (cfg.ShowObjectives.Value) DrawPieces(s.Objectives, view);

            // Once anyone has seen one mushroom of an order objective, show all of them.
            knownGroups.Clear();
            foreach (var poi in s.Pois)
                if (poi.Pillar != null && (discovery.IsKnown(poi.Key) || discovery.IsKnown(poi.GroupKey))) knownGroups.Add(poi.GroupKey);
            foreach (var poi in s.Pois)
                if (!(poi.Kind == PoiKind.Boss && s.Bosses.Count > 0 && cfg.ShowEnemies.Value)) // the boss icon below replaces the game's pointer
                    DrawPoi(poi, view, discovery);
            if (cfg.ShowEnemies.Value)
                foreach (var b in s.Bosses) DrawPoint(boss, Color.white, MapMath.WorldToPanel(view, b), BossSize, half, clamp: true);
            if (cfg.ShowObjectives.Value)
                foreach (var t in s.Objectives.HuntTargets)
                {
                    // A crosshair that follows the hunted enemy, pinned to the edge when it's off the map.
                    if (t == null) continue;
                    Vector3 w = t.position;
                    DrawPoint(crosshair, Color.white, MapMath.WorldToPanel(view, new NVec2(w.x, w.z)), CrosshairSize, half, clamp: true);
                }
            if (cfg.ShowMoreEvents.Value)
                foreach (var m in markers)
                    if (!m.Zone) DrawPoint(diamond, m.Color, MapMath.WorldToPanel(view, m.Pos), 16f, half, clamp: true);
            if (cfg.ShowTeammates.Value)
                foreach (var p in s.Players)
                {
                    // Dead teammates are a ghost in their own colour, the living a dot.
                    DrawPoint(p.Ghost ? ghost : disc, p.Color, MapMath.WorldToPanel(view, p.Pos), p.Ghost ? GhostSize : 13f, half, clamp: true);
                }
            if (cfg.ShowObjectives.Value) DrawCounters(s.Objectives, half);
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
            bool found = discovery.IsKnown(poi.Key) || (poi.Pillar != null && knownGroups.Contains(poi.GroupKey));
            if (poi.Kind == PoiKind.Objective && cfg.HideUndiscoveredObjectives.Value && !found) return;

            Vector3 w = poi.Target.position;
            NVec2 p = MapMath.WorldToPanel(view, new NVec2(w.x, w.z));
            if (poi.Pillar != null)
            {
                // An order-objective mushroom: a dark badge ringed in its current state's colour.
                if (!(round ? MapMath.IsInsideCircle(p, view.HalfSize) : MapMath.IsInside(p, view.HalfSize))) return;
                Color c = PillarIdle;
                try
                {
                    var st = poi.Pillar.state;
                    c = st == OrderObjectiveInteractableState.Correct ? PillarCorrect
                      : st == OrderObjectiveInteractableState.Incorrect ? PillarWrong : PillarIdle;
                }
                catch { } // despawned
                Vector2 at = ToUnity(p);
                icons.Place(disc, BadgeColor, at, PillarSize, 0f);
                icons.Place(badgeRing, c, at, PillarSize, 0f);
                if (poi.Sprite != null) icons.Place(poi.Sprite, Color.Lerp(c, Color.white, 0.35f), at, PillarSize * 0.66f, 0f);
                else icons.Place(disc, c, at, PillarSize * 0.5f, 0f);
                return;
            }
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

        /// <summary>
        /// The running objectives' pieces: mushrooms, objects to push, animals to herd, stones to break. Pieces that
        /// still have to go somewhere get a faint dotted line to their zone; pieces already there are faded.
        /// </summary>
        void DrawPieces(ObjectivePieces o, in MapView view)
        {
            foreach (var piece in o.Pieces)
            {
                if (piece.Target == null) continue;
                Vector3 w = piece.Target.position;
                NVec2 p = MapMath.WorldToPanel(view, new NVec2(w.x, w.z));
                if (piece.HasGoal && !piece.Done)
                {
                    NVec2 g = MapMath.WorldToPanel(view, piece.Goal);
                    float dist = NVec2.Distance(p, g);
                    int steps = (int)(dist / GuideDotSpacing);
                    for (int k = 1; k < steps; k++)
                    {
                        NVec2 d = NVec2.Lerp(p, g, (float)k / steps);
                        if (round ? MapMath.IsInsideCircle(d, view.HalfSize) : MapMath.IsInside(d, view.HalfSize))
                            icons.Place(disc, GuideColor, ToUnity(d), GuideDotSize, 0f);
                    }
                }
                if (!(round ? MapMath.IsInsideCircle(p, view.HalfSize) : MapMath.IsInside(p, view.HalfSize))) continue;
                icons.Place(pieceSprites[(int)piece.Kind], piece.Done ? DonePiece : Color.white, ToUnity(p), PieceSize, 0f);
            }
        }

        /// <summary>A pill per running objective along the top of the minimap: its piece icon and done/total.</summary>
        void DrawCounters(ObjectivePieces o, float half)
        {
            for (int i = 0; i < o.Counts.Count; i++)
            {
                var c = o.Counts[i];
                string text = $"{c.Done}/{c.Total}";
                if (!counterSprites.TryGetValue(text, out var sprite))
                {
                    var img = PixelFont.Render(text, CounterScale, CounterText, CounterPill, 2, 1, CounterPadLeft);
                    counterSprites[text] = sprite = SpriteFactory.ToSprite(img, FilterMode.Point);
                }
                float w = sprite.rect.width, h = sprite.rect.height;     // one canvas unit per pixel
                var at = new Vector2(0f, half - 12f - i * (h + 4f));
                icons.Place(sprite, Color.white, at, new Vector2(w, h), 0f);
                icons.Place(pieceSprites[(int)c.Kind], Color.white, at + new Vector2(-w * 0.5f + CounterPadLeft * CounterScale * 0.5f + 2f, 0f), h - 2f, 0f);
            }
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
                && bottomOffset == lastBottomOffset && corner == lastCorner && lift == lastLift) return;
            lastShape = shape;
            lastSize = size;
            lastMargin = margin;
            lastBottomOffset = bottomOffset;
            lastCorner = corner;
            lastLift = lift;

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
            frame.anchoredPosition = new Vector2(right ? -margin : margin, top ? -(margin + lift) : margin + bottomOffset + lift);
            frame.sizeDelta = new Vector2(size, size);
        }

        /// <summary>Where the minimap sits in screen pixels before moving out of the game HUD's way.</summary>
        ScreenBox HomeScreenBox(out bool bottom)
        {
            ScreenCorner corner = cfg.Corner.Value;
            bool right = corner == ScreenCorner.TopRight || corner == ScreenCorner.BottomRight;
            bottom = corner == ScreenCorner.BottomLeft || corner == ScreenCorner.BottomRight;
            float s = Mathf.Max(0.01f, canvas.scaleFactor);
            float size = cfg.Size.Value * s, margin = cfg.Margin.Value * s, below = (cfg.Margin.Value + cfg.BottomOffset.Value) * s;
            float w = Screen.width, h = Screen.height;
            float xMin = right ? w - margin - size : margin;
            float yMin = bottom ? below : h - margin - size;
            return new ScreenBox(xMin, yMin, xMin + size, yMin + size);
        }

        static Vector2 ToUnity(NVec2 v) => new Vector2(v.X, v.Y);
    }
}
