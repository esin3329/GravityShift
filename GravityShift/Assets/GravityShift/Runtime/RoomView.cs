using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;
using Object = UnityEngine.Object;

namespace GravityShift
{
    internal sealed class RoomView : IDisposable
    {
        private readonly Transform root;
        private readonly Material material;
        private readonly Sprite flat;
        private readonly Sprite rounded;
        private readonly Sprite triangle;
        private readonly Sprite ring;
        private readonly List<SpriteRenderer> doors = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> doorBars = new List<SpriteRenderer>();
        private readonly Dictionary<Body, Actor> actors = new Dictionary<Body, Actor>();
        private readonly List<Body> expired = new List<Body>();
        private readonly List<Effect> particles = new List<Effect>();
        private readonly Stack<SpriteRenderer> particlePool = new Stack<SpriteRenderer>();
        private readonly List<SpriteRenderer> zoneShapes = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> zoneArrows = new List<SpriteRenderer>();
        private readonly SpriteRenderer pointer;
        private readonly SpriteRenderer tether;
        private readonly SpriteRenderer slash;
        private readonly SpriteRenderer[] outline = new SpriteRenderer[4];
        private Transform stage;
        private Actor player;
        private int roomVersion;

        private sealed class Actor
        {
            public Transform Root;
            public Transform Shape;
            public SpriteRenderer Outline;
            public SpriteRenderer Body;
            public SpriteRenderer Visor;
            public SpriteRenderer Mark;
            public SpriteRenderer Ring;
            public SpriteRenderer StunMark;
            public SpriteRenderer Pulse;
            public SpriteRenderer[] Pupils;
            public SpriteRenderer[] Health;
        }

        private struct Effect
        {
            public SpriteRenderer Renderer;
            public Vector2 Position;
            public Vector2 Velocity;
            public Color Color;
            public float Life;
        }

        public RoomView(Material material)
        {
            this.material = material;
            root = new GameObject("Room Rendering").transform;
            flat = MakeSprite((x, y) => true);
            rounded = MakeSprite((x, y) =>
            {
                float dx = Mathf.Max(0, Mathf.Abs(x - 0.5f) - 0.25f);
                float dy = Mathf.Max(0, Mathf.Abs(y - 0.5f) - 0.25f);
                return dx * dx + dy * dy <= 0.25f * 0.25f;
            });
            triangle = MakeSprite((x, y) => Mathf.Abs(x - 0.5f) <= (1 - y) * 0.5f);
            ring = MakeSprite((x, y) =>
            {
                float d = (new Vector2(x, y) - Vector2.one * 0.5f).sqrMagnitude;
                return d > 0.38f * 0.38f && d < 0.49f * 0.49f;
            });
            pointer = Create("Pointer", root, 32);
            tether = Create("Telekinesis Tether", root, 23);
            slash = Create("Attack", root, 24, rounded);
            for (int i = 0; i < outline.Length; i++) outline[i] = Create("Target Outline", root, 25);
            for (int i = 0; i < Tuning.MaxZones; i++)
            {
                zoneShapes.Add(Create("Gravity Field", root, 1));
                zoneArrows.Add(Create("Gravity Direction", root, 3, triangle));
            }
        }

        private static Sprite MakeSprite(Func<float, float, bool> visible)
        {
            const int side = 32;
            var texture = new Texture2D(side, side, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[side * side];
            for (int y = 0; y < side; y++)
                for (int x = 0; x < side; x++)
                    pixels[y * side + x] = visible((x + 0.5f) / side, (y + 0.5f) / side)
                        ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, side, side), Vector2.one * 0.5f, side, 0, SpriteMeshType.FullRect);
        }

        private SpriteRenderer Create(string name, Transform parent, int layer, Sprite sprite = null)
        {
            var renderer = new GameObject(name).AddComponent<SpriteRenderer>();
            renderer.transform.SetParent(parent, false);
            renderer.sprite = sprite ?? flat;
            renderer.sharedMaterial = material;
            renderer.sortingOrder = layer;
            return renderer;
        }

        private static void Pose(SpriteRenderer sprite, Vector2 center, Vector2 size, Color color, float angle = 0)
        {
            sprite.transform.localPosition = center;
            sprite.transform.localScale = new Vector3(size.x, size.y, 1);
            sprite.transform.localRotation = Quaternion.Euler(0, 0, angle);
            sprite.color = color;
        }

        private SpriteRenderer Block(string name, Transform parent, Vector2 center, Vector2 size, Color color, int layer)
        {
            var sprite = Create(name, parent, layer);
            Pose(sprite, center, size, color);
            return sprite;
        }

        private void BuildRoom(GameState game)
        {
            if (stage != null)
            {
                stage.gameObject.SetActive(false);
                Object.Destroy(stage.gameObject);
            }
            foreach (var particle in particles) Recycle(particle.Renderer);
            particles.Clear();
            actors.Clear();
            doors.Clear();
            doorBars.Clear();
            stage = new GameObject("Current Room").transform;
            stage.SetParent(root, false);
            var size = new Vector2(RoomMap.Width, RoomMap.Height);
            int theme = game.Room.Zone;
            Block("Background", stage, new Vector2(size.x, -size.y) * 0.5f, size, Palette.Backgrounds[theme], -10);
            Color grid = Palette.Alpha(Palette.Accents[theme], 0.08f);
            for (int col = 0; col < RoomMap.Width; col++)
                Block("Grid", stage, new Vector2(col, -size.y / 2), new Vector2(Tuning.Pixel, size.y), grid, -9);
            for (int row = 0; row < RoomMap.Height; row++)
                Block("Grid", stage, new Vector2(size.x / 2, -row), new Vector2(size.x, Tuning.Pixel), grid, -9);

            if (theme == 2)
            {
                var stars = new System.Random(120);
                for (int i = 0; i < 65; i++)
                    Block("Star", stage, new Vector2((float)stars.NextDouble() * size.x, -(float)stars.NextDouble() * size.y),
                        Vector2.one * 0.035f, Palette.Alpha(Color.white, 0.45f), -8);
            }

            for (int row = 0; row < RoomMap.Height; row++)
            {
                for (int col = 0; col < RoomMap.Width; col++)
                {
                    Vector2 center = RoomMap.CellCenter(col, row);
                    switch (game.Map.At(col, row))
                    {
                        case '#': BuildWall(game.Map, col, row, center, theme); break;
                        case '^': BuildSpikes(game.Map, col, row, center); break;
                        case 'D':
                            doors.Add(Block("Exit", stage, center, Vector2.one * 0.88f, Color.white, 6));
                            for (int i = -1; i <= 1; i++)
                                doorBars.Add(Block("Exit Bars", stage, center + Vector2.right * i * 0.22f,
                                    new Vector2(0.055f, 0.74f), Palette.Rgb(0x566174), 7));
                            break;
                    }
                }
            }
            player = MakeActor(game.Player);
            roomVersion = game.RoomVersion;
        }

        private void BuildWall(RoomMap map, int col, int row, Vector2 center, int theme)
        {
            Block("Wall", stage, center, Vector2.one, Palette.Walls[theme], 5);
            Color edge = Palette.WallEdges[theme];
            var horizontal = new Vector2(1, 3 * Tuning.Pixel);
            var vertical = new Vector2(horizontal.y, 1);
            const float offset = 0.453125f;
            if (!map.IsSolid(col, row - 1)) Block("Edge", stage, center + Vector2.up * offset, horizontal, edge, 6);
            if (!map.IsSolid(col, row + 1)) Block("Edge", stage, center + Vector2.down * offset, horizontal, edge, 6);
            if (!map.IsSolid(col - 1, row)) Block("Edge", stage, center + Vector2.left * offset, vertical, edge, 6);
            if (!map.IsSolid(col + 1, row)) Block("Edge", stage, center + Vector2.right * offset, vertical, edge, 6);
        }

        private void BuildSpikes(RoomMap map, int col, int row, Vector2 center)
        {
            int angle = map.IsSolid(col, row + 1) ? 0 : map.IsSolid(col, row - 1) ? 180 :
                map.IsSolid(col - 1, row) ? -90 : map.IsSolid(col + 1, row) ? 90 : 0;
            var group = new GameObject("Spikes").transform;
            group.SetParent(stage, false);
            group.localPosition = center;
            group.localRotation = Quaternion.Euler(0, 0, angle);
            for (int i = -1; i <= 1; i++)
                Pose(Create("Spike", group, 6, triangle), new Vector2(i / 3f, -0.15f), new Vector2(0.29f, 0.69f), Palette.Spikes);
            Block("Spike Base", group, new Vector2(0, -0.44f), new Vector2(1, 0.12f), Palette.Rgb(0xa3243d), 6);
        }

        private Actor MakeActor(Body body)
        {
            var actor = new Actor { Root = new GameObject(body.Kind.ToString()).transform };
            actor.Root.SetParent(stage, false);
            actor.Shape = new GameObject("Body").transform;
            actor.Shape.SetParent(actor.Root, false);
            bool round = body.Kind != BodyKind.Crate;
            actor.Outline = Create("Outline", actor.Shape, 11, round ? rounded : flat);
            actor.Body = Create("Silhouette", actor.Shape, 12, round ? rounded : flat);
            actor.Mark = Create("Gravity Override", actor.Root, 17, triangle);

            if (body.Kind == BodyKind.Player)
            {
                actor.Visor = Block("Visor", actor.Shape, new Vector2(0.14f, 0.13f), new Vector2(0.30f, 0.16f), Palette.Psychic, 14);
                for (int side = -1; side <= 1; side += 2)
                    Block("Foot", actor.Shape, new Vector2(side * 0.14f, -0.40f), new Vector2(0.16f, 0.08f), Palette.Ink, 13);
            }
            else if (body.IsEnemy)
            {
                actor.Pupils = new SpriteRenderer[2];
                int pupil = 0;
                for (int side = -1; side <= 1; side += 2)
                {
                    Block("Eye", actor.Shape, new Vector2(side * 0.16f, 0.13f), new Vector2(0.20f, 0.18f), Color.white, 14);
                    actor.Pupils[pupil++] = Block("Pupil", actor.Shape, new Vector2(side * 0.16f, 0.13f), new Vector2(0.08f, 0.10f), Palette.Ink, 15);
                    Block("Foot", actor.Shape, new Vector2(side * 0.16f, -0.40f), new Vector2(0.16f, 0.08f), Palette.Ink, 13);
                }
                if (body.Kind == BodyKind.Anchor)
                    actor.Ring = Create("Anchor Ring", actor.Shape, 16, ring);
                actor.StunMark = Create("Stun", actor.Shape, 18, triangle);
                actor.StunMark.enabled = false;
                if (body.Kind == BodyKind.Inverter)
                {
                    actor.Pulse = Create("Inverter Pulse", actor.Root, 8, ring);
                    actor.Pulse.enabled = false;
                }
                actor.Health = new SpriteRenderer[body.MaxHealth];
                for (int i = 0; i < actor.Health.Length; i++)
                    actor.Health[i] = Block("Health", actor.Root, new Vector2((i - (body.MaxHealth - 1) / 2f) * 0.18f, 0.56f),
                        new Vector2(0.12f, 0.08f), Palette.Spikes, 16);
            }
            else
            {
                var wood = Palette.Rgb(0x5e3d1a);
                for (int direction = -1; direction <= 1; direction += 2)
                    Pose(Create("Brace", actor.Shape, 13), Vector2.zero, new Vector2(0.94f, 0.055f), wood, 45 * direction);
            }

            return actor;
        }

        private static void DrawActor(Actor actor, Body body, GameState game)
        {
            actor.Root.localPosition = body.Position;
            actor.Root.gameObject.SetActive(body.Alive && (!(body is PlayerBody p) || p.Blink <= 0 || (int)(game.Time * 20) % 2 == 0));
            float squash = 1f - 0.15f * Mathf.Clamp01(body.Squash / 0.1f);
            actor.Shape.localScale = new Vector3(1f, squash, 1f);
            actor.Shape.localRotation = Quaternion.Euler(0, 0, body.Kind == BodyKind.Crate ? 0 : body.Angle);
            Color color = body.Kind == BodyKind.Player ? Palette.Ash : body.Kind == BodyKind.Walker ? Palette.Walker :
                body.Kind == BodyKind.Anchor ? Palette.Anchor : body.Kind == BodyKind.Inverter ? Palette.Inverter : Palette.Rgb(0x9c6a35);
            if (body.Held) color = Color.Lerp(color, Palette.Psychic, 0.65f);
            if (body is PlayerBody dashing && dashing.DashTime > 0f) color = Color.Lerp(Palette.Ash, Palette.Dash, 0.65f);
            Pose(actor.Outline, Vector2.zero, body.Size + Vector2.one * 0.08f, Palette.Ink);
            Pose(actor.Body, Vector2.zero, body.Size, body.Flash > 0 ? Color.white : color);
            if (actor.Pupils != null)
            {
                Vector2 toPlayer = game.Player.Position - body.Position;
                Vector2 look = (Vector2)(Quaternion.Euler(0f, 0f, -body.Angle) * (Vector3)toPlayer);
                look = Vector2.ClampMagnitude(look, 0.045f);
                int side = -1;
                foreach (SpriteRenderer pupil in actor.Pupils)
                {
                    pupil.transform.localPosition = new Vector2(side * 0.16f, 0.13f) + look;
                    side += 2;
                }
            }
            if (actor.Ring != null)
                Pose(actor.Ring, Vector2.zero, Vector2.one * 0.62f, Color.Lerp(Palette.Anchor, Color.white, 0.55f));
            if (actor.StunMark != null)
            {
                actor.StunMark.enabled = body.Stun > 0f;
                if (actor.StunMark.enabled)
                    Pose(actor.StunMark, new Vector2(Mathf.Sin(game.Time * 12f) * 0.08f, 0.58f), Vector2.one * 0.2f, Palette.Sparks);
            }
            if (actor.Pulse != null)
            {
                actor.Pulse.enabled = body.InverterTelegraph;
                if (actor.Pulse.enabled)
                {
                    float charge = 1f - Mathf.Clamp01(body.Pulse / Tuning.InverterTelegraph);
                    const float scale = Tuning.InverterRadius * 2f / 0.88f;
                    Pose(actor.Pulse, Vector2.zero, Vector2.one * scale, Palette.Alpha(Palette.Inverter, 0.25f + 0.55f * charge));
                }
            }
            actor.Mark.enabled = body.Override.HasValue;
            if (actor.Mark.enabled)
                Pose(actor.Mark, body.Gravity.Vector() * 0.65f, Vector2.one * 0.29f, Palette.Psychic, 180 - (int)body.Gravity * 90);

            if (actor.Visor != null)
            {
                int facing = body.Gravity == GravityDirection.Down || body.Gravity == GravityDirection.Right ? body.Facing : -body.Facing;
                Pose(actor.Visor, new Vector2(facing * 0.14f, 0.13f), new Vector2(0.30f, 0.16f),
                    game.UsesEngineer ? Palette.Engineer : Palette.Psychic);
            }
            if (actor.Health != null)
                for (int i = 0; i < actor.Health.Length; i++)
                    actor.Health[i].color = i < body.Health ? Palette.Rgb(0xff9a8a) : Palette.Alpha(Color.white, 0.2f);
        }

        public void Draw(GameState game, Controls input, float delta)
        {
            if (roomVersion != game.RoomVersion) BuildRoom(game);
            root.localPosition = Random.insideUnitCircle * (game.Shake * Tuning.Pixel * 0.5f);
            for (int i = 0; i < doors.Count; i++)
                doors[i].color = game.DoorOpen ? Palette.Alpha(Palette.Accents[game.Room.Zone], 0.7f + 0.25f * Mathf.Sin(game.Time * 5)) : Palette.Rgb(0x1b1f27);
            foreach (var bar in doorBars) bar.enabled = !game.DoorOpen;

            DrawActor(player, game.Player, game);
            expired.Clear();
            foreach (var entry in actors)
                if (!entry.Key.Alive) expired.Add(entry.Key);
            foreach (Body dead in expired)
            {
                Object.Destroy(actors[dead].Root.gameObject);
                actors.Remove(dead);
            }
            foreach (Body body in game.Bodies)
            {
                if (!actors.TryGetValue(body, out var actor))
                {
                    actor = MakeActor(body);
                    actors.Add(body, actor);
                }
                DrawActor(actor, body, game);
            }

            slash.enabled = game.Player.AttackTime > 0 && game.Player.Alive;
            if (slash.enabled)
            {
                Color slashColor = game.UsesEngineer ? Palette.Engineer : Palette.Psychic;
                Pose(slash, game.Player.AttackBounds.center, game.Player.AttackBounds.size, Palette.Alpha(slashColor, 0.4f));
            }
            DrawAbilities(game, input);
            DrawParticles(delta);
        }

        private void DrawAbilities(GameState game, Controls input)
        {
            for (int i = 0; i < zoneShapes.Count; i++)
            {
                bool visible = game.UsesEngineer && i < game.Engineer.Zones.Count;
                zoneShapes[i].enabled = zoneArrows[i].enabled = visible;
                if (!visible) continue;
                var zone = game.Engineer.Zones[i];
                Pose(zoneShapes[i], zone.Bounds.center, zone.Bounds.size, Palette.Alpha(Palette.Engineer, 0.22f));
                Pose(zoneArrows[i], zone.Bounds.center, Vector2.one * 0.7f, Palette.Engineer, 180 - (int)zone.Direction * 90);
            }

            pointer.enabled = input.PointerInRoom;
            Rect? bounds = null;
            Color color = game.UsesEngineer ? Palette.Engineer : Palette.Psychic;
            if (game.UsesEngineer)
            {
                pointer.sprite = triangle;
                if (input.PointerInRoom) bounds = game.Engineer.Preview(game.Map, input.Pointer);
                Pose(pointer, input.Pointer, Vector2.one * 0.65f, bounds.HasValue ? color : Color.gray, 180 - (int)game.Engineer.Direction * 90);
            }
            else
            {
                pointer.sprite = ring;
                var target = input.PointerInRoom ? game.PickBody(input.Pointer) : null;
                if (target != null && target != game.Psychic.Grabbed)
                {
                    var rect = target.Bounds;
                    rect.min -= Vector2.one * 0.10f;
                    rect.max += Vector2.one * 0.10f;
                    bounds = rect;
                    if (!target.Manipulable) color = Color.gray;
                }
                Pose(pointer, input.Pointer, Vector2.one * 0.55f, color);
            }

            foreach (var edge in outline) edge.enabled = bounds.HasValue;
            if (bounds.HasValue)
            {
                const float width = 0.045f;
                Rect box = bounds.Value;
                Pose(outline[0], new Vector2(box.xMin, box.center.y), new Vector2(width, box.height), color);
                Pose(outline[1], new Vector2(box.xMax, box.center.y), new Vector2(width, box.height), color);
                Pose(outline[2], new Vector2(box.center.x, box.yMin), new Vector2(box.width, width), color);
                Pose(outline[3], new Vector2(box.center.x, box.yMax), new Vector2(box.width, width), color);
            }

            var grabbed = game.UsesEngineer ? null : game.Psychic.Grabbed;
            tether.enabled = grabbed != null && grabbed.Alive && game.Player.Alive;
            if (tether.enabled)
            {
                Vector2 offset = grabbed.Position - game.Player.Position;
                Pose(tether, game.Player.Position + offset * 0.5f, new Vector2(offset.magnitude, 0.06f),
                    Palette.Alpha(Palette.Psychic, 0.7f), Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg);
            }
        }

        public void Burst(Vector2 center, Color color, int count, float speed)
        {
            for (int i = 0; i < count; i++)
            {
                var sprite = particlePool.Count > 0 ? particlePool.Pop() : Create("Particle", root, 27);
                sprite.enabled = true;
                Pose(sprite, center, Vector2.one * Random.Range(0.06f, 0.15f), color);
                particles.Add(new Effect
                {
                    Renderer = sprite,
                    Position = center,
                    Velocity = Random.insideUnitCircle * speed,
                    Color = color,
                    Life = Random.Range(0.3f, 0.7f)
                });
            }
        }

        private void DrawParticles(float delta)
        {
            for (int i = particles.Count - 1; i >= 0; i--)
            {
                var effect = particles[i];
                effect.Life -= delta;
                if (effect.Life <= 0)
                {
                    Recycle(effect.Renderer);
                    particles.RemoveAt(i);
                    continue;
                }
                effect.Position += effect.Velocity * delta;
                effect.Velocity *= Mathf.Pow(0.92f, delta * 60);
                effect.Renderer.transform.localPosition = effect.Position;
                effect.Renderer.color = Palette.Alpha(effect.Color, Mathf.Clamp01(effect.Life * 2.5f));
                particles[i] = effect;
            }
        }

        private void Recycle(SpriteRenderer sprite)
        {
            sprite.enabled = false;
            particlePool.Push(sprite);
        }

        public void Dispose()
        {
            if (root != null) Object.Destroy(root.gameObject);
            foreach (Sprite sprite in new[] { flat, rounded, triangle, ring })
            {
                Object.Destroy(sprite.texture);
                Object.Destroy(sprite);
            }
        }
    }
}
