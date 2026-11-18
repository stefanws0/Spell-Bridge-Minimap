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
        // Per EnemyRank (Small, Big, Heavy, MiniBoss): the mark's size (circumradius in texels) and colour. The shape
        // comes from EnemyMarks: circle, triangle, square, star.
        static readonly float[] RankRadius = { 1.85f, 4.0f, 3.9f, 5.6f };
        static readonly Rgba[] RankColor = { new Rgba(235, 64, 52), new Rgba(255, 150, 40), new Rgba(240, 78, 168), new Rgba(180, 90, 255) };
        const float PinnedEnemySize = 18f;
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
        readonly Sprite[] pieceSprites;
        readonly Sprite crosshair;
        readonly Sprite[] enemyMarks;   // indexed by EnemyShape, white (tinted by rank)
        readonly Dictionary<string, Sprite> counterSprites = new Dictionary<string, Sprite>();
        // Counters in the game's own font (see GameFont): one text per running objective, over a pill.
        readonly RectTransform counterRoot;
        readonly List<TMPro.TextMeshProUGUI> counterTexts = new List<TMPro.TextMeshProUGUI>();
        readonly List<string> counterShown = new List<string>();
        readonly List<float> counterWidth = new List<float>();
        readonly Dictionary<int, Sprite> pills = new Dictionary<int, Sprite>();
        bool gameFontBroken;
        const float CounterHeight = 18f, CounterFontSize = 15f, CounterIcon = 16f;
        static readonly Color CounterPillColor = new Color(24f / 255f, 24f / 255f, 26f / 255f, 0.92f);
        static readonly Color CounterTextColor = new Color(253f / 255f, 248f / 255f, 206f / 255f, 1f);
        readonly Dictionary<System.IntPtr, Vector2> spriteAspect = new Dictionary<System.IntPtr, Vector2>();
        const float PieceSize = 15f, CrosshairSize = 26f, GuideDotSize = 3f, GuideDotSpacing = 7f;
        const float PinnedPieceSize = 18f;      // DrawPoint draws a pinned icon at 80%, so about a piece's own size
        // The game's own icons (with their dark edge, see IconRecolor): pieces, objectives and hunt targets, zone centres.
        const float GameIconSize = 18f, ObjectiveIconSize = 22f, ZoneIconSize = 18f;
        const int CounterScale = 2, CounterPadLeft = 6;
        static readonly Color GuideColor = new Color(0.99f, 0.97f, 0.81f, 0.55f);
        static readonly Color DonePiece = new Color(1f, 1f, 1f, 0.45f);
        static readonly Rgba CounterText = new Rgba(253, 248, 206);
        static readonly Rgba CounterPill = new Rgba(24, 24, 26, 235);
        const float GhostSize = 16f, PillarSize = 18f;
        // Objective colours (the game's icons are white copies the minimap tints): blue for most, the herding pen
        // green, objects to push yellow until they're in the zone, the hunt target gold, the sacrifice red, and the
        // clean objective its goo's colour (ObjectivePieces.GooColour).
        static readonly Color ObjectiveBlue = new Color(0.36f, 0.62f, 1f, 1f);
        static readonly Color PenGreen = new Color(0.47f, 0.86f, 0.55f, 1f);
        static readonly Color PushYellow = new Color(1f, 0.82f, 0.25f, 1f);
        static readonly Color HuntGold = new Color(1f, 0.76f, 0.18f, 1f);
        static readonly Color SacrificeRed = new Color(0.96f, 0.3f, 0.28f, 1f);
        const float ZoneRingAlpha = 0.9f, ZoneFillAlpha = 0.15f, DoneAlpha = 0.45f;
        // The order objective's bonfires, as the game colours them: waiting, lit in the right order, wrong.
        static readonly Color PillarIdle = ObjectiveBlue;
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
            crosshair = SpriteFactory.ToSprite(ShapeSprites.Crosshair(64));
            // The minimap's own piece icons, for when the game's icon isn't known. Indexed by PieceKind: Mushroom,
            // Crate, Herd, Statue, Hunt.
            pieceSprites = new[]
            {
                SpriteFactory.ToSprite(ShapeSprites.Mushroom(48)), SpriteFactory.ToSprite(ShapeSprites.Crate(48)),
                SpriteFactory.ToSprite(ShapeSprites.Paw(48)), SpriteFactory.ToSprite(ShapeSprites.Statue(48)), crosshair,
            };
            enemyMarks = new[]
            {
                SpriteFactory.ToSprite(ShapeSprites.EnemyMark(48, EnemyShape.Circle)), SpriteFactory.ToSprite(ShapeSprites.EnemyMark(48, EnemyShape.Triangle)),
                SpriteFactory.ToSprite(ShapeSprites.EnemyMark(48, EnemyShape.Square)), SpriteFactory.ToSprite(ShapeSprites.EnemyMark(48, EnemyShape.Star)),
            };

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
            counterRoot = UiBuilder.NewRect("Counters", panel);

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
                    // Push zone (blue) / herding pen (green) at its real size, a soft fill and an edge, once found.
                    if (cfg.HideUndiscoveredObjectives.Value && !discovery.IsKnown(z.Key)) continue;
                    Vector2 at = ToUnity(MapMath.WorldToPanel(view, z.Center));
                    Color c = ZoneTint(z);
                    zones.Place(roundShape, WithAlpha(c, ZoneFillAlpha), at, z.Radius * 2f * scale, 0f);
                    zones.Place(ring, WithAlpha(c, ZoneRingAlpha), at, z.Radius * 2f * scale, 0f);
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
            if (cfg.ShowObjectives.Value)
            {
                DrawPieces(s.Objectives, view, discovery);
                DrawZoneIcons(s.Objectives, view, discovery);
            }

            foreach (var poi in s.Pois)
                if (!(poi.Kind == PoiKind.Boss && s.Bosses.Count > 0 && cfg.ShowEnemies.Value)) // the boss icon below replaces the game's pointer
                    DrawPoi(poi, view, discovery, s.Objectives);
            if (cfg.ShowEnemies.Value)
                foreach (var b in s.Bosses) DrawPoint(boss, Color.white, MapMath.WorldToPanel(view, b), BossSize, half, clamp: true);
            if (cfg.ShowEnemies.Value)
                foreach (var e in s.PointedEnemies)
                {
                    // An enemy the game points at from the screen edge: on the map it's already in the enemy layer;
                    // off the map, its rank's mark is pinned to the edge.
                    if (e.Target == null) continue;
                    Vector3 w = e.Target.position;
                    NVec2 p = MapMath.WorldToPanel(view, new NVec2(w.x, w.z));
                    if (round ? MapMath.IsInsideCircle(p, half) : MapMath.IsInside(p, half)) continue;
                    int rank = Mathf.Min((int)e.Rank, RankColor.Length - 1);
                    Rgba c = RankColor[rank];
                    DrawPoint(enemyMarks[(int)EnemyMarks.ShapeOf(e.Rank)], new Color(c.R / 255f, c.G / 255f, c.B / 255f, 1f), p, PinnedEnemySize, half, clamp: true);
                }
            if (cfg.ShowObjectives.Value)
                foreach (var t in s.Objectives.HuntTargets)
                {
                    // The game's hunt icon in gold, following the hunted enemy once found, pinned to the edge when
                    // it's off the map (a crosshair until the game's icon is known).
                    if (t.Target == null) continue;
                    if (cfg.HideUndiscoveredObjectives.Value && !discovery.IsKnown(t.Key)) continue;
                    Vector3 w = t.Target.position;
                    NVec2 hp = MapMath.WorldToPanel(view, new NVec2(w.x, w.z));
                    var huntIcon = s.Objectives.PieceIcon(t.Target, PieceKind.Hunt);
                    if (huntIcon != null) DrawIcon(hp, ObjectiveIconSize, half, HuntGold, huntIcon, pin: true);
                    else DrawPoint(crosshair, Color.white, hp, CrosshairSize, half, clamp: true);
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
                    var outlineShape = EnemyMarks.ShapeOf((EnemyRank)rank);
                    if (outlineShape == EnemyShape.Circle) dotPixels.FillCircle(x, y, RankRadius[rank] + DotOutline, EnemyOutline);
                    else EnemyMarks.Fill(dotPixels, outlineShape, x, y, RankRadius[rank], DotOutline, EnemyOutline);
                }
                // Small first, mini-bosses last, so the important ones stay on top in a crowd.
                for (int rank = 0; rank < RankRadius.Length; rank++)
                {
                    var shape = EnemyMarks.ShapeOf((EnemyRank)rank);
                    foreach (var (x, y, r) in dotSpots)
                    {
                        if (r != rank) continue;
                        if (shape == EnemyShape.Circle) dotPixels.FillCircle(x, y, RankRadius[rank], RankColor[rank]);
                        else EnemyMarks.Fill(dotPixels, shape, x, y, RankRadius[rank], 0f, RankColor[rank]);
                    }
                }
            }
            if (!any && dotsEmpty) return; // already blank on the GPU
            SpriteFactory.Upload(dotTexture, dotPixels, keepReadable: true);
            dotsEmpty = !any;
        }

        void DrawPoi(Poi poi, in MapView view, ObjectiveDiscovery discovery, ObjectivePieces o)
        {
            if (poi.Target == null) return;
            bool pickup = poi.Kind == PoiKind.Pickup || poi.Kind == PoiKind.Health;
            if (pickup ? !cfg.ShowPickups.Value : !cfg.ShowObjectives.Value) return;
            // The order objective's bonfires too, each one on its own.
            // Pickups too: the game only points at those for the bird mage.
            bool found = discovery.IsKnown(poi.Key);
            if ((poi.Kind == PoiKind.Objective || pickup) && cfg.HideUndiscoveredObjectives.Value && !found) return;

            Vector3 w = poi.Target.position;
            NVec2 p = MapMath.WorldToPanel(view, new NVec2(w.x, w.z));
            if (poi.Pillar != null)
            {
                // An order-objective bonfire: the game's icon in its current state's colour, pinned to the edge
                // while the game points at it.
                Color c = PillarIdle;
                try
                {
                    var st = poi.Pillar.state;
                    c = st == OrderObjectiveInteractableState.Correct ? PillarCorrect
                      : st == OrderObjectiveInteractableState.Incorrect ? PillarWrong : PillarIdle;
                }
                catch { } // despawned
                DrawIcon(p, PillarSize, view.HalfSize, c, poi.WhiteSprite != null ? poi.WhiteSprite : GameIcons.White(GameIcons.Order), pin: poi.Shown);
                return;
            }
            if (poi.Kind == PoiKind.Objective)
            {
                // A push zone's or herding pen's objective: the zone's own icon stands for it (see DrawZoneIcons).
                if (CoveredByZone(o, w, discovery)) return;
                // The game's icon, coloured by the minimap (the statue look for a petrified-enemies objective).
                string name = GameIcons.NameOf(poi.Sprite);
                bool statue = name == GameIcons.Petrified;
                Sprite white = statue ? GameIcons.Statue() : poi.WhiteSprite;
                if (white != null)
                {
                    DrawIcon(p, ObjectiveIconSize, view.HalfSize, statue ? Color.white : ObjectiveTint(name, o), white, pin: poi.ClampToEdge);
                    return;
                }
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
        /// The running objectives' pieces, as the game's own icons: mushrooms and llamas in blue, objects to push in
        /// yellow (blue once in the zone), statues in grey with blue details. Each one only once found. Pieces that
        /// still have to go somewhere get a faint dotted line to their zone once that's found; llamas in the pen are
        /// faded. A piece off the map is pinned to the rim while the game points at it from the screen edge.
        /// </summary>
        void DrawPieces(ObjectivePieces o, in MapView view, ObjectiveDiscovery discovery)
        {
            bool hideUnfound = cfg.HideUndiscoveredObjectives.Value;
            foreach (var piece in o.Pieces)
            {
                if (piece.Target == null) continue;
                // Each piece only once someone in the party has had it on screen, or the game has pointed at it.
                if (hideUnfound && !discovery.IsKnown(piece.Key)) continue;
                Vector3 w = piece.Target.position;
                NVec2 p = MapMath.WorldToPanel(view, new NVec2(w.x, w.z));
                if (piece.HasGoal && !piece.Done && (!hideUnfound || discovery.IsKnown(piece.GoalKey)))
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
                bool pointed = o.IsPointedAt(piece.Target);
                Sprite icon = PieceIcon(o, piece.Target, piece.Kind);
                if (icon != null || piece.Kind == PieceKind.Mushroom)
                {
                    // The game's icon (a blue dot for a mushroom until it's known).
                    DrawIcon(p, GameIconSize, view.HalfSize, PieceTint(piece.Kind, piece.Done), icon, pin: pointed);
                    continue;
                }
                // The game's icon isn't known (yet): the minimap's own.
                Sprite sprite = pieceSprites[(int)piece.Kind];
                Color color = piece.Done ? DonePiece : Color.white;
                if (!(round ? MapMath.IsInsideCircle(p, view.HalfSize) : MapMath.IsInside(p, view.HalfSize)))
                {
                    // Off the map: only where the game itself points at the piece from the screen edge, pinned to the rim.
                    if (pointed) DrawPoint(sprite, color, p, PinnedPieceSize, view.HalfSize, clamp: true);
                    continue;
                }
                icons.Place(sprite, color, ToUnity(p), PieceSize, 0f);
            }
        }

        /// <summary>
        /// The push zones' and herding pens' icons in their middle, once found: they stand for their objective, so
        /// off the map they're pinned to the rim like an objective.
        /// </summary>
        void DrawZoneIcons(ObjectivePieces o, in MapView view, ObjectiveDiscovery discovery)
        {
            foreach (var z in o.Zones)
            {
                if (cfg.HideUndiscoveredObjectives.Value && !discovery.IsKnown(z.Key)) continue;
                var icon = GameIcons.White(z.Icon);
                if (icon == null) continue;                                   // the ring alone until it's known
                DrawIcon(MapMath.WorldToPanel(view, z.Center), ZoneIconSize, view.HalfSize, ZoneTint(z), icon, pin: true);
            }
        }

        /// <summary>True if an objective at <paramref name="w"/> is a zone's (near its middle) and that zone is drawn.</summary>
        bool CoveredByZone(ObjectivePieces o, Vector3 w, ObjectiveDiscovery discovery)
        {
            foreach (var z in o.Zones)
            {
                float dx = w.x - z.Center.X, dz = w.z - z.Center.Y;
                if (dx * dx + dz * dz > z.Radius * z.Radius * 0.25f) continue;
                if (GameIcons.White(z.Icon) == null) continue;               // no zone icon to stand for it
                if (!cfg.HideUndiscoveredObjectives.Value || discovery.IsKnown(z.Key)) return true;
            }
            return false;
        }

        /// <summary>The game's icon for a piece: the statue look for statues, else a white copy to tint. Null until known.</summary>
        static Sprite PieceIcon(ObjectivePieces o, Transform piece, PieceKind kind) =>
            kind == PieceKind.Statue ? GameIcons.Statue() : o.PieceIcon(piece, kind);

        static Color PieceTint(PieceKind kind, bool done)
        {
            switch (kind)
            {
                case PieceKind.Crate: return done ? ObjectiveBlue : PushYellow;          // yellow until it's in the zone
                case PieceKind.Herd: return done ? WithAlpha(ObjectiveBlue, DoneAlpha) : ObjectiveBlue;
                case PieceKind.Statue: return Color.white;                               // already grey with blue
                case PieceKind.Hunt: return HuntGold;
                default: return ObjectiveBlue;
            }
        }

        static Color ZoneTint(in ObjectiveZone z) => z.Icon == GameIcons.HerdingZone ? PenGreen : ObjectiveBlue;

        /// <summary>An objective's colour, by the game's icon on its pointer.</summary>
        static Color ObjectiveTint(string icon, ObjectivePieces o)
        {
            switch (icon)
            {
                case GameIcons.Sacrifice: return SacrificeRed;
                case GameIcons.Hunt: return HuntGold;
                case GameIcons.Clean: return o.GooColour ?? ObjectiveBlue;
                case GameIcons.HerdingZone: return PenGreen;
                case GameIcons.Push: return PushYellow;
                default: return ObjectiveBlue;
            }
        }

        static Color WithAlpha(Color c, float a)
        {
            c.a = a;
            return c;
        }

        /// <summary>
        /// A pill per running objective along the top of the minimap: its piece icon and done/total, in the game's
        /// own font once it's known (the minimap's pixel font until then, or if the game font can't be used).
        /// </summary>
        void DrawCounters(ObjectivePieces o, float half)
        {
            bool gameFont = !gameFontBroken && GameFont.TryFind(Time.unscaledTime) && EnsureCounterTexts(o.Counts.Count);
            for (int i = 0; i < counterTexts.Count; i++)
            {
                bool use = gameFont && i < o.Counts.Count;
                if (counterTexts[i].enabled != use) counterTexts[i].enabled = use;
            }
            for (int i = 0; i < o.Counts.Count; i++)
            {
                var c = o.Counts[i];
                string text = $"{c.Done}/{c.Total}";
                if (gameFont)
                {
                    try
                    {
                        DrawGameFontCounter(i, c.Kind, text, o, half);
                        continue;
                    }
                    catch (System.Exception e)
                    {
                        // Never let the counters take the whole minimap down: back to the pixel font for good.
                        gameFont = false;
                        gameFontBroken = true;
                        foreach (var t in counterTexts) t.enabled = false;
                        LogOnce.Error("drawing a counter in the game's font (keeping the pixel font)", e);
                    }
                }
                if (!counterSprites.TryGetValue(text, out var sprite))
                {
                    var img = PixelFont.Render(text, CounterScale, CounterText, CounterPill, 2, 1, CounterPadLeft);
                    counterSprites[text] = sprite = SpriteFactory.ToSprite(img, FilterMode.Point);
                }
                float w = sprite.rect.width, h = sprite.rect.height;     // one canvas unit per pixel
                var at = new Vector2(0f, half - 12f - i * (h + 4f));
                icons.Place(sprite, Color.white, at, new Vector2(w, h), 0f);
                var iconAt = at + new Vector2(-w * 0.5f + CounterPadLeft * CounterScale * 0.5f + 2f, 0f);
                PlaceCounterIcon(iconAt, h - 2f, c.Kind, o);
            }
        }

        void DrawGameFontCounter(int i, PieceKind kind, string text, ObjectivePieces o, float half)
        {
            var t = counterTexts[i];
            if (counterShown[i] != text)
            {
                counterShown[i] = text;
                t.text = text;
                counterWidth[i] = Mathf.Ceil(t.GetPreferredValues(text).x);
            }
            float textW = counterWidth[i];
            const float padLeft = 4f, gap = 3f, padRight = 8f;
            int pillW = Mathf.CeilToInt(padLeft + CounterIcon + gap + textW + padRight);
            if (!pills.TryGetValue(pillW, out var pill))
                pills[pillW] = pill = SpriteFactory.ToSprite(ShapeSprites.Pill(pillW, (int)CounterHeight));
            var at = new Vector2(0f, half - 13f - i * (CounterHeight + 4f));
            float left = at.x - pillW * 0.5f;
            icons.Place(pill, CounterPillColor, at, new Vector2(pillW, CounterHeight), 0f);
            var iconAt = new Vector2(left + padLeft + CounterIcon * 0.5f + 1f, at.y);
            PlaceCounterIcon(iconAt, CounterIcon, kind, o);
            var rt = t.rectTransform;
            rt.anchoredPosition = new Vector2(left + padLeft + CounterIcon + gap + textW * 0.5f, at.y);
            rt.sizeDelta = new Vector2(textW + 2f, CounterHeight);
        }

        /// <summary>Makes sure there are <paramref name="n"/> counter texts in the game's font; false if they can't be made.</summary>
        bool EnsureCounterTexts(int n)
        {
            try
            {
                while (counterTexts.Count < n)
                {
                    var go = new GameObject("Counter");
                    go.transform.SetParent(counterRoot, false);
                    var t = go.AddComponent<TMPro.TextMeshProUGUI>();
                    t.font = GameFont.Font;
                    t.fontSize = CounterFontSize;
                    t.color = CounterTextColor;
                    t.alignment = TMPro.TextAlignmentOptions.Center;
                    t.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
                    t.overflowMode = TMPro.TextOverflowModes.Overflow;
                    t.richText = false;
                    t.raycastTarget = false;
                    t.enabled = false;
                    counterTexts.Add(t);
                    counterShown.Add(null);
                    counterWidth.Add(0f);
                }
                return true;
            }
            catch (System.Exception e)
            {
                gameFontBroken = true;
                LogOnce.Error("making the counters in the game's font (keeping the pixel font)", e);
                return false;
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
            EdgePin(p, size, half, out Vector2 iconAt, out Vector2 arrowAt, out float angle);
            icons.Place(sprite, color, iconAt, size * 0.8f, 0f);
            icons.Place(arrow, color, arrowAt, 10f, angle - 90f);
        }

        /// <summary>Where an off-map icon of <paramref name="size"/> sits on the edge, and its arrow pointing outwards.</summary>
        void EdgePin(NVec2 p, float size, float half, out Vector2 iconAt, out Vector2 arrowAt, out float angle)
        {
            NVec2 iconPos, arrowPos;
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
            iconAt = ToUnity(iconPos);
            arrowAt = ToUnity(arrowPos);
        }

        /// <summary>
        /// One of the game's icons at <paramref name="p"/>; off the map, pinned to the edge with an arrow if
        /// <paramref name="pin"/>.
        /// </summary>
        void DrawIcon(NVec2 p, float size, float half, Color color, Sprite white, bool pin)
        {
            if (round ? MapMath.IsInsideCircle(p, half) : MapMath.IsInside(p, half))
            {
                PlaceIcon(ToUnity(p), size, color, white);
                return;
            }
            if (!pin) return;
            EdgePin(p, size, half, out Vector2 iconAt, out Vector2 arrowAt, out float angle);
            PlaceIcon(iconAt, size * 0.8f, color, white);
            icons.Place(arrow, color == Color.white ? Cream : WithAlpha(color, 1f), arrowAt, 10f, angle - 90f);
        }

        /// <summary>
        /// A copy of one of the game's icons (see IconRecolor) in <paramref name="color"/>, keeping its shape; a dot
        /// in that colour until the icon is known.
        /// </summary>
        void PlaceIcon(Vector2 at, float size, Color color, Sprite white)
        {
            if (white == null)
            {
                icons.Place(disc, color, at, size * 0.6f, 0f);
                return;
            }
            icons.Place(white, color, at, Fit(white, size), 0f);
        }

        /// <summary>A counter's piece icon: the game's, coloured like the pieces, else the minimap's own.</summary>
        void PlaceCounterIcon(Vector2 at, float size, PieceKind kind, ObjectivePieces o)
        {
            Sprite icon = PieceIcon(o, null, kind);
            if (icon != null || kind == PieceKind.Mushroom) PlaceIcon(at, size, PieceTint(kind, done: false), icon);
            else icons.Place(pieceSprites[(int)kind], Color.white, at, size, 0f);
        }

        /// <summary>A sprite's size that keeps its shape, with its longer side <paramref name="size"/>.</summary>
        Vector2 Fit(Sprite s, float size)
        {
            if (!spriteAspect.TryGetValue(s.Pointer, out var a))
            {
                Rect r = s.rect;
                float m = Mathf.Max(1f, Mathf.Max(r.width, r.height));
                spriteAspect[s.Pointer] = a = new Vector2(r.width / m, r.height / m);
            }
            return a * size;
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
