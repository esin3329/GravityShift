using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GravityShift
{
    internal sealed class HudView : IDisposable
    {
        private static readonly string[] Arrows = { "↓", "←", "↑", "→" };
        private readonly Canvas canvas;
        private readonly RectTransform panel;
        private readonly Font font;
        private readonly Sprite healthFull;
        private readonly Sprite healthEmpty;
        private readonly Sprite fieldFull;
        private readonly Sprite fieldEmpty;
        private readonly Sprite panel9Slice;
        private readonly UnityEngine.UI.Image[] health = new UnityEngine.UI.Image[Tuning.MaxHealth];
        private readonly UnityEngine.UI.Image[] zones = new UnityEngine.UI.Image[Tuning.MaxZones];
        private readonly UnityEngine.UI.Image manaTrack;
        private readonly UnityEngine.UI.Image manaFill;
        private readonly UnityEngine.UI.Text style;
        private readonly UnityEngine.UI.Text direction;
        private readonly UnityEngine.UI.Text room;
        private readonly UnityEngine.UI.Text rotation;
        private readonly UnityEngine.UI.Text seed;
        private readonly UnityEngine.UI.Text status;
        private readonly UnityEngine.UI.Text help;
        private readonly CanvasGroup banner;
        private readonly GameObject pauseRoot;
        private readonly UnityEngine.UI.Text bannerTitle;
        private readonly UnityEngine.UI.Text bannerSubtitle;
        private readonly List<FloatingNote> notes = new List<FloatingNote>();
        private int roomVersion;
        private int roomTextVersion = -1;
        private GravityDirection roomTextGravity;
        private bool hasStyle;
        private bool lastEngineer;
        private int lastDirection = int.MinValue;
        private int lastRotationTenths = int.MinValue;
        private uint lastSeed;
        private bool hasSeed;
        private bool hasStatus;
        private bool lastDoorOpen;
        private int lastEnemiesLeft;
        private bool hasHelp;
        private bool lastHelpEngineer;

        private struct FloatingNote
        {
            public UnityEngine.UI.Text Label;
            public Vector2 Position;
            public float Life;
        }

        public HudView(Transform parent, VisualAssets assets)
        {
            if (assets == null) throw new ArgumentNullException(nameof(assets));
            assets.RequireComplete();
            font = assets.HudFont;
            healthFull = assets.HealthFull;
            healthEmpty = assets.HealthEmpty;
            fieldFull = assets.FieldFull;
            fieldEmpty = assets.FieldEmpty;
            panel9Slice = assets.Panel9Slice;

            canvas = new GameObject("Game HUD", typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
            canvas.transform.SetParent(parent, false);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(960, 544);
            scaler.referencePixelsPerUnit = 32;
            scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;
            panel = Frame("Playfield", canvas.transform, 0, 0, 960, 544);
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.anchoredPosition = Vector2.zero;

            Color panelColor = new Color(1f, 1f, 1f, 0.9f);
            Image("Header", panel, 0, 0, 960, 32, panelColor, panel9Slice, UnityEngine.UI.Image.Type.Sliced);
            for (int i = 0; i < health.Length; i++)
                health[i] = Image("Health", panel, 10 + i * 18, 8, 16, 16, Color.white, healthEmpty);

            style = Label("Style", panel, 112, 0, 70, 32, 14, TextAnchor.MiddleLeft);
            manaTrack = Image("Mana", panel, 180, 12, 120, 8, Palette.Alpha(Color.white, 0.13f));
            manaFill = Image("Fill", manaTrack.transform, 0, 0, 120, 8, Palette.Psychic);
            for (int i = 0; i < zones.Length; i++)
                zones[i] = Image("Field Capacity", panel, 180 + i * 16, 8, 16, 16, Color.white, fieldEmpty);
            direction = Label("Field Direction", panel, 235, 0, 64, 32, 13, TextAnchor.MiddleLeft);
            room = Label("Room", panel, 325, 0, 258, 32, 15, TextAnchor.MiddleCenter);
            rotation = Label("Rotation", panel, 589, 0, 106, 32, 12, TextAnchor.MiddleLeft);
            seed = Label("Seed", panel, 723, 0, 125, 32, 11, TextAnchor.MiddleRight);
            seed.color = Palette.Rgb(0xa0afbf);
            status = Label("Exit Status", panel, 855, 0, 92, 32, 13, TextAnchor.MiddleRight);

            Image("Controls Backdrop", panel, 0, 512, 960, 32, panelColor, panel9Slice, UnityEngine.UI.Image.Type.Sliced);
            var keys = Label("Common Controls", panel, 0, 512, 960, 16, 11, TextAnchor.MiddleCenter);
            keys.text = "A/D 이동 (좌우 중력일 땐 W/S)  ·  Space 점프  ·  Shift 대시  ·  J 공격  ·  Tab 스타일  ·  Esc 일시정지  ·  R 재시작  ·  N 새 런";
            keys.color = Palette.Rgb(0xb8c6d7);
            help = Label("Style Controls", panel, 0, 526, 960, 16, 11, TextAnchor.MiddleCenter);

            var band = Image("Room Banner", panel, 0, 222, 960, 90, panelColor, panel9Slice, UnityEngine.UI.Image.Type.Sliced);
            banner = band.gameObject.AddComponent<CanvasGroup>();
            banner.blocksRaycasts = false;
            bannerTitle = Label("Title", band.transform, 0, 7, 960, 53, 31, TextAnchor.MiddleCenter);
            bannerSubtitle = Label("Subtitle", band.transform, 0, 56, 960, 26, 16, TextAnchor.MiddleCenter);

            var dim = Image("Pause Scrim", panel, 0, 0, 960, 544, Palette.Alpha(Palette.Ink, 0.72f));
            pauseRoot = dim.gameObject;
            dim.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;
            var pauseTitle = Label("Pause Title", dim.transform, 0, 220, 960, 48, 31, TextAnchor.MiddleCenter);
            pauseTitle.text = "일시정지";
            var pauseHint = Label("Pause Hint", dim.transform, 0, 274, 960, 28, 16, TextAnchor.MiddleCenter);
            pauseHint.text = "Esc 계속  ·  R 이 방  ·  N 새 런";
            pauseHint.color = Palette.Dash;
            pauseRoot.SetActive(false);
        }

        private static RectTransform Frame(string name, Transform parent, float x, float y, float width, float height)
        {
            var frame = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            frame.SetParent(parent, false);
            frame.anchorMin = frame.anchorMax = frame.pivot = new Vector2(0, 1);
            frame.anchoredPosition = new Vector2(x, -y);
            frame.sizeDelta = new Vector2(width, height);
            return frame;
        }

        private static UnityEngine.UI.Image Image(string name, Transform parent, float x, float y, float width, float height,
            Color color, Sprite sprite = null, UnityEngine.UI.Image.Type type = UnityEngine.UI.Image.Type.Simple)
        {
            var image = Frame(name, parent, x, y, width, height).gameObject.AddComponent<UnityEngine.UI.Image>();
            image.sprite = sprite;
            image.type = type;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private UnityEngine.UI.Text Label(string name, Transform parent, float x, float y, float width, float height, int size, TextAnchor align)
        {
            var text = Frame(name, parent, x, y, width, height).gameObject.AddComponent<UnityEngine.UI.Text>();
            text.font = font;
            text.fontSize = size;
            text.alignment = align;
            text.color = Palette.Ash;
            text.raycastTarget = false;
            return text;
        }


        public void Draw(GameState game, float dt)
        {
            bool engineer = game.UsesEngineer;
            Color accent = Palette.Accents[game.Room.Zone];
            Color styleColor = engineer ? Palette.Engineer : Palette.Psychic;
            for (int i = 0; i < health.Length; i++)
            {
                Sprite icon = i < game.Player.Health ? healthFull : healthEmpty;
                if (health[i].sprite != icon) health[i].sprite = icon;
            }

            if (!hasStyle || engineer != lastEngineer)
            {
                style.text = engineer ? "엔지니어" : "사이킥";
                lastEngineer = engineer;
                hasStyle = true;
            }
            style.color = styleColor;
            manaTrack.enabled = manaFill.enabled = !engineer;
            manaFill.rectTransform.sizeDelta = new Vector2(120 * game.Psychic.Mana / Tuning.MaxMana, 8);
            for (int i = 0; i < zones.Length; i++)
            {
                zones[i].enabled = engineer;
                Sprite icon = i < game.Engineer.Zones.Count ? fieldFull : fieldEmpty;
                if (zones[i].sprite != icon) zones[i].sprite = icon;
            }

            int directionIndex = engineer ? (int)game.Engineer.Direction : -1;
            if (directionIndex != lastDirection)
            {
                direction.text = directionIndex < 0 ? string.Empty : "방향 " + Arrows[directionIndex];
                lastDirection = directionIndex;
            }
            direction.color = styleColor;

            if (roomTextVersion != game.RoomVersion || roomTextGravity != game.RoomGravity)
            {
                room.text = game.Room.Title + "  " + Arrows[(int)game.RoomGravity];
                roomTextVersion = game.RoomVersion;
                roomTextGravity = game.RoomGravity;
            }

            int rotationDisplay = game.Room.RotateEvery > 0 ? Mathf.RoundToInt(game.RotationTimer * 10f) : -1;
            if (rotationDisplay != lastRotationTenths)
            {
                rotation.text = rotationDisplay < 0 ? string.Empty : $"회전 {rotationDisplay * 0.1f:0.0}s";
                lastRotationTenths = rotationDisplay;
            }
            rotation.color = game.RotationTimer < 1.5f ? Palette.Spikes : Palette.Ash;

            if (!hasSeed || lastSeed != game.Seed)
            {
                seed.text = "시드 " + game.Seed;
                lastSeed = game.Seed;
                hasSeed = true;
            }

            if (!hasStatus || lastDoorOpen != game.DoorOpen || lastEnemiesLeft != game.EnemiesLeft)
            {
                status.text = game.DoorOpen ? "출구 개방 ▶" : $"남은 적 {game.EnemiesLeft}";
                lastDoorOpen = game.DoorOpen;
                lastEnemiesLeft = game.EnemiesLeft;
                hasStatus = true;
            }
            status.color = game.DoorOpen ? accent : Palette.Ash;

            if (!hasHelp || lastHelpEngineer != engineer)
            {
                help.text = engineer
                    ? "좌클릭 중력 발판 설치  ·  휠/Q 방향 회전  ·  우클릭 발판 제거  ·  F 모든 발판 반전"
                    : "좌클릭 대상 중력 반전  ·  우클릭 홀드 잡기, 놓으면 던지기  ·  F 내 중력 반전";
                lastHelpEngineer = engineer;
                hasHelp = true;
            }
            help.color = styleColor;

            if (pauseRoot.activeSelf != game.Paused) pauseRoot.SetActive(game.Paused);
            banner.alpha = game.Paused ? 0 : Mathf.Clamp01(game.BannerTime * 1.5f);
            if (bannerTitle.text != game.BannerTitle) bannerTitle.text = game.BannerTitle;
            if (bannerSubtitle.text != game.BannerSubtitle) bannerSubtitle.text = game.BannerSubtitle;
            bannerSubtitle.color = accent;
            if (roomVersion != game.RoomVersion)
            {
                roomVersion = game.RoomVersion;
                foreach (var note in notes) Object.Destroy(note.Label.gameObject);
                notes.Clear();
            }
            for (int i = notes.Count - 1; i >= 0; i--)
            {
                var note = notes[i];
                note.Life -= dt;
                if (note.Life <= 0)
                {
                    Object.Destroy(note.Label.gameObject);
                    notes.RemoveAt(i);
                    continue;
                }
                note.Position.y += dt * 30;
                note.Label.rectTransform.anchoredPosition = note.Position;
                note.Label.color = Palette.Alpha(styleColor, Mathf.Clamp01(note.Life * 2));
                notes[i] = note;
            }
        }

        public void Note(Vector2 point, string message)
        {
            var label = Label("Feedback", panel, point.x * 32 - 80, -point.y * 32 - 30, 160, 24, 13, TextAnchor.MiddleCenter);
            label.text = message;
            notes.Add(new FloatingNote { Label = label, Position = label.rectTransform.anchoredPosition, Life = 1.1f });
        }

        public void Dispose()
        {
            if (canvas != null) Object.Destroy(canvas.gameObject);
        }
    }
}
