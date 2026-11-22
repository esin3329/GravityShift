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
        private readonly VisualAssets visualAssets;
        private readonly Sprite flat;
        private readonly Sprite[] playerIdleFrames;
        private readonly Sprite[] playerRunFrames;
        private readonly Sprite playerRise;
        private readonly Sprite playerFall;
        private readonly Sprite[] playerDashFrames;
        private readonly Sprite[] playerAttackFrames;
        private readonly Sprite playerVisor;
        private readonly Sprite[] walkerIdleFrames;
        private readonly Sprite[] walkerWalkFrames;
        private readonly Sprite[] anchorIdleFrames;
        private readonly Sprite[] anchorWalkFrames;
        private readonly Sprite[] inverterIdleFrames;
        private readonly Sprite[] inverterWalkFrames;
        private readonly Sprite[] puffFrames;
        private readonly Sprite enemyPupil;
        private readonly Sprite anchorRing;
        private readonly Sprite stunStar;
        private readonly Sprite crateSprite;
        private readonly Sprite spikesSprite;
        private readonly Sprite exitClosedSprite;
        private readonly Sprite exitOpenSprite;
        private readonly Sprite gravityBeamSprite;
        private readonly Sprite gravityArrowSprite;
        private readonly Sprite inverterWarningRingSprite;
        private readonly Sprite telekinesisTetherSprite;
        private readonly List<SpriteRenderer> doors = new List<SpriteRenderer>();
        private readonly Dictionary<Body, Actor> actors = new Dictionary<Body, Actor>();
        private readonly List<Body> expired = new List<Body>();
        private readonly List<Effect> particles = new List<Effect>();
        private readonly Stack<SpriteRenderer> particlePool = new Stack<SpriteRenderer>();
        private readonly List<SpriteRenderer> zoneShapes = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> zoneArrows = new List<SpriteRenderer>();
        private readonly SpriteRenderer pointer;
        private readonly SpriteRenderer tether;
        private readonly SpriteRenderer[] outline = new SpriteRenderer[4];
        private Transform stage;
        private Actor player;
        private int roomVersion;
        private bool doorsOpen;
        private bool pointerUsesEngineer = true;

        private sealed class Actor
        {
            public Transform Root;
            public Transform Shape;
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
            public float Age;
            public float Duration;
        }

        public RoomView(Material material, VisualAssets visualAssets)
        {
            if (material == null) throw new ArgumentNullException(nameof(material));
            if (visualAssets == null) throw new ArgumentNullException(nameof(visualAssets));

            this.material = material;
            this.visualAssets = visualAssets;
            playerIdleFrames = visualAssets.PlayerIdleFrames;
            playerRunFrames = visualAssets.PlayerRunFrames;
            playerRise = visualAssets.PlayerRise;
            playerFall = visualAssets.PlayerFall;
            playerDashFrames = visualAssets.PlayerDashFrames;
            playerAttackFrames = visualAssets.PlayerAttackFrames;
            playerVisor = visualAssets.PlayerVisor;
            walkerIdleFrames = visualAssets.WalkerIdleFrames;
            walkerWalkFrames = visualAssets.WalkerWalkFrames;
            anchorIdleFrames = visualAssets.AnchorIdleFrames;
            anchorWalkFrames = visualAssets.AnchorWalkFrames;
            inverterIdleFrames = visualAssets.InverterIdleFrames;
            inverterWalkFrames = visualAssets.InverterWalkFrames;
            puffFrames = visualAssets.PuffFrames;
            enemyPupil = visualAssets.EnemyPupil;
            anchorRing = visualAssets.AnchorRing;
            stunStar = visualAssets.StunStar;
            crateSprite = visualAssets.Crate;
            spikesSprite = visualAssets.Spikes;
            exitClosedSprite = visualAssets.ExitClosed;
            exitOpenSprite = visualAssets.ExitOpen;
            gravityBeamSprite = visualAssets.GravityBeam;
            gravityArrowSprite = visualAssets.GravityArrow;
            inverterWarningRingSprite = visualAssets.InverterWarningRing;
            telekinesisTetherSprite = visualAssets.TelekinesisTether;

            root = new GameObject("Room Rendering").transform;
            flat = MakeFlatSprite();
            pointer = Create("Pointer", root, 32, gravityArrowSprite);
            Pose(pointer, Vector2.zero, Vector2.one * 0.65f, Color.white);
            tether = Create("Telekinesis Tether", root, 23, telekinesisTetherSprite);
            tether.drawMode = SpriteDrawMode.Tiled;
            for (int i = 0; i < Tuning.MaxZones; i++)
            {
                SpriteRenderer shape = Create("Gravity Field", root, 1, gravityBeamSprite);
                shape.drawMode = SpriteDrawMode.Tiled;
                zoneShapes.Add(shape);
                zoneArrows.Add(Create("Gravity Direction", root, 3, gravityArrowSprite));
            }
            for (int i = 0; i < outline.Length; i++) outline[i] = Create("Target Outline", root, 25);
        }

        private static Sprite MakeFlatSprite()
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f, 1, 0, SpriteMeshType.FullRect);
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

        private static void Pose(SpriteRenderer sprite, Vector2 center, Vector2 scale, Color color, float angle = 0)
        {
            sprite.transform.localPosition = center;
            sprite.transform.localScale = new Vector3(scale.x, scale.y, 1);
            sprite.transform.localRotation = Quaternion.Euler(0, 0, angle);
            sprite.color = color;
        }

        private static void PoseSprite(SpriteRenderer sprite, Vector2 center, Vector2 size, Color color, float angle = 0)
        {
            Vector3 sourceSize = sprite.sprite.bounds.size;
            Pose(sprite, center, new Vector2(size.x / sourceSize.x, size.y / sourceSize.y), color, angle);
        }

        private static void PoseTiled(SpriteRenderer sprite, Vector2 center, Vector2 size, Color color, float angle = 0)
        {
            sprite.size = size;
            sprite.transform.localPosition = center;
            sprite.transform.localScale = Vector3.one;
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
            doorsOpen = game.DoorOpen;
            stage = new GameObject("Current Room").transform;
            stage.SetParent(root, false);

            int theme = game.Room.Zone;
            SpriteRenderer background = Create("Background", stage, -10, visualAssets.BackgroundFor(theme));
            PoseSprite(background, new Vector2(RoomMap.Width * 0.5f, -RoomMap.Height * 0.5f),
                new Vector2(RoomMap.Width, RoomMap.Height), Color.white);
            VisualTileSet tiles = visualAssets.TilesFor(theme);

            for (int row = 0; row < RoomMap.Height; row++)
            {
                for (int col = 0; col < RoomMap.Width; col++)
                {
                    Vector2 center = RoomMap.CellCenter(col, row);
                    switch (game.Map.At(col, row))
                    {
                        case '#': BuildWall(game.Map, col, row, center, tiles); break;
                        case '^': BuildSpikes(game.Map, col, row, center); break;
                        case 'D':
                            SpriteRenderer door = Create("Exit", stage, 6, game.DoorOpen ? exitOpenSprite : exitClosedSprite);
                            PoseSprite(door, center, Vector2.one, Color.white);
                            doors.Add(door);
                            break;
                    }
                }
            }
            player = MakeActor(game.Player);
            roomVersion = game.RoomVersion;
        }

        private void BuildWall(RoomMap map, int col, int row, Vector2 center, VisualTileSet tiles)
        {
            int exposedMask = 0;
            if (!map.IsSolid(col, row - 1)) exposedMask |= 1;
            if (!map.IsSolid(col + 1, row)) exposedMask |= 2;
            if (!map.IsSolid(col, row + 1)) exposedMask |= 4;
            if (!map.IsSolid(col - 1, row)) exposedMask |= 8;

            SpriteRenderer wall = Create("Wall", stage, 5, tiles.Edge(exposedMask));
            PoseSprite(wall, center, Vector2.one, Color.white);

            if (map.IsSolid(col, row - 1) && map.IsSolid(col + 1, row) && !map.IsSolid(col + 1, row - 1))
                AddInnerCorner(tiles, 0, center);
            if (map.IsSolid(col + 1, row) && map.IsSolid(col, row + 1) && !map.IsSolid(col + 1, row + 1))
                AddInnerCorner(tiles, 1, center);
            if (map.IsSolid(col, row + 1) && map.IsSolid(col - 1, row) && !map.IsSolid(col - 1, row + 1))
                AddInnerCorner(tiles, 2, center);
            if (map.IsSolid(col - 1, row) && map.IsSolid(col, row - 1) && !map.IsSolid(col - 1, row - 1))
                AddInnerCorner(tiles, 3, center);
        }

        private void AddInnerCorner(VisualTileSet tiles, int corner, Vector2 center)
        {
            SpriteRenderer overlay = Create("Wall Inner Corner", stage, 6, tiles.InnerCorner(corner));
            PoseSprite(overlay, center, Vector2.one, Color.white);
        }

        private void BuildSpikes(RoomMap map, int col, int row, Vector2 center)
        {
            int angle = map.IsSolid(col, row + 1) ? 0 : map.IsSolid(col, row - 1) ? 180 :
                map.IsSolid(col - 1, row) ? -90 : map.IsSolid(col + 1, row) ? 90 : 0;
            SpriteRenderer spikes = Create("Spikes", stage, 6, spikesSprite);
            PoseSprite(spikes, center, Vector2.one, Color.white, angle);
        }

        private Sprite InitialSprite(Body body)
        {
            if (body.Kind == BodyKind.Player) return playerIdleFrames[0];
            if (body.Kind == BodyKind.Walker) return walkerIdleFrames[0];
            if (body.Kind == BodyKind.Anchor) return anchorIdleFrames[0];
            if (body.Kind == BodyKind.Inverter) return inverterIdleFrames[0];
            return crateSprite;
        }

        private Actor MakeActor(Body body)
        {
            var actor = new Actor { Root = new GameObject(body.Kind.ToString()).transform };
            actor.Root.SetParent(stage, false);
            actor.Shape = new GameObject("Body").transform;
            actor.Shape.SetParent(actor.Root, false);
            actor.Body = Create("Artwork", actor.Shape, 12, InitialSprite(body));
            actor.Mark = Create("Gravity Override", actor.Root, 17, gravityArrowSprite);
            Pose(actor.Mark, Vector2.zero, Vector2.one * 0.29f, Color.white);

            if (body.Kind == BodyKind.Player)
            {
                actor.Visor = Create("Visor", actor.Shape, 14, playerVisor);
            }
            else if (body.IsEnemy)
            {
                actor.Pupils = new SpriteRenderer[2];
                int pupil = 0;
                for (int side = -1; side <= 1; side += 2)
                {
                    SpriteRenderer eye = Create("Pupil", actor.Shape, 15, enemyPupil);
                    PoseSprite(eye, new Vector2(side * 0.16f, 0.13f), Vector2.one * 0.18f, Color.white);
                    actor.Pupils[pupil++] = eye;
                }
                if (body.Kind == BodyKind.Anchor)
                {
                    actor.Ring = Create("Anchor Ring", actor.Shape, 16, anchorRing);
                    PoseSprite(actor.Ring, Vector2.zero, Vector2.one * 0.62f, Color.white);
                }
                actor.StunMark = Create("Stun", actor.Shape, 18, stunStar);
                PoseSprite(actor.StunMark, new Vector2(0, 0.78f), Vector2.one * 0.24f, Color.white);
                actor.StunMark.enabled = false;
                if (body.Kind == BodyKind.Inverter)
                {
                    actor.Pulse = Create("Inverter Pulse", actor.Root, 8, inverterWarningRingSprite);
                    PoseSprite(actor.Pulse, Vector2.zero, Vector2.one * (Tuning.InverterRadius * 2), Color.white);
                    actor.Pulse.enabled = false;
                }
                actor.Health = new SpriteRenderer[body.MaxHealth];
                for (int i = 0; i < actor.Health.Length; i++)
                    actor.Health[i] = Block("Health", actor.Root, new Vector2((i - (body.MaxHealth - 1) / 2f) * 0.18f, 0.82f),
                        new Vector2(0.12f, 0.08f), Palette.Spikes, 16);
            }

            return actor;
        }

        private static int FrameIndex(Sprite[] frames, float time, float framesPerSecond) =>
            Mathf.FloorToInt(time * framesPerSecond) % frames.Length;

        private Sprite AnimatedSprite(Body body, GameState game)
        {
            float time = game.Time;
            if (body.Kind == BodyKind.Player)
            {
                PlayerBody playerBody = (PlayerBody)body;
                if (playerBody.DashTime > 0f) return playerDashFrames[FrameIndex(playerDashFrames, time, 14f)];
                if (playerBody.AttackTime > 0f) return playerAttackFrames[FrameIndex(playerAttackFrames, time, 18f)];
                if (!body.Grounded)
                    return Vector2.Dot(body.Velocity, body.Gravity.Vector()) < 0f ? playerRise : playerFall;
                int axis = body.VerticalGravity ? 0 : 1;
                return Mathf.Abs(body.Velocity[axis]) > 0.15f
                    ? playerRunFrames[FrameIndex(playerRunFrames, time, 12f)]
                    : playerIdleFrames[FrameIndex(playerIdleFrames, time, 2.5f)];
            }

            int movementAxis = body.VerticalGravity ? 0 : 1;
            bool walking = body.Grounded && !body.Held && body.Stun <= 0f && Mathf.Abs(body.Velocity[movementAxis]) > 0.15f;
            if (body.Kind == BodyKind.Walker)
                return walking ? walkerWalkFrames[FrameIndex(walkerWalkFrames, time, 8f)] : walkerIdleFrames[FrameIndex(walkerIdleFrames, time, 2f)];
            if (body.Kind == BodyKind.Anchor)
                return walking ? anchorWalkFrames[FrameIndex(anchorWalkFrames, time, 8f)] : anchorIdleFrames[FrameIndex(anchorIdleFrames, time, 2f)];
            if (body.Kind == BodyKind.Inverter)
                return walking ? inverterWalkFrames[FrameIndex(inverterWalkFrames, time, 8f)] : inverterIdleFrames[FrameIndex(inverterIdleFrames, time, 2f)];
            return crateSprite;
        }

        private static float ArrowAngle(GravityDirection direction) => -(int)direction * 90f;

        private void DrawActor(Actor actor, Body body, GameState game)
        {
            actor.Root.localPosition = body.Position;
            actor.Root.gameObject.SetActive(body.Alive && (!(body is PlayerBody p) || p.Blink <= 0 || (int)(game.Time * 20) % 2 == 0));
            float squash = 1f - 0.15f * Mathf.Clamp01(body.Squash / 0.1f);
            actor.Shape.localScale = new Vector3(1f, squash, 1f);
            actor.Shape.localRotation = Quaternion.Euler(0, 0, body.Kind == BodyKind.Crate ? 0 : body.Angle);

            int facing = body.Gravity == GravityDirection.Down || body.Gravity == GravityDirection.Right ? body.Facing : -body.Facing;
            Sprite frame = AnimatedSprite(body, game);
            if (actor.Body.sprite != frame) actor.Body.sprite = frame;
            actor.Body.flipX = facing < 0;
            Color color = body.Kind == BodyKind.Player ? Color.white : body.Kind == BodyKind.Walker ? Palette.Walker :
                body.Kind == BodyKind.Anchor ? Palette.Anchor : body.Kind == BodyKind.Inverter ? Palette.Inverter : Color.white;
            if (body.Held) color = Color.Lerp(color, Palette.Psychic, 0.65f);
            if (body is PlayerBody dashing && dashing.DashTime > 0f) color = Color.Lerp(Palette.Ash, Palette.Dash, 0.65f);
            actor.Body.color = body.Flash > 0 ? Color.white : color;

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
            if (actor.StunMark != null)
            {
                actor.StunMark.enabled = body.Stun > 0f;
                if (actor.StunMark.enabled)
                    actor.StunMark.transform.localPosition = new Vector2(Mathf.Sin(game.Time * 12f) * 0.08f, 0.78f);
            }
            if (actor.Pulse != null)
            {
                actor.Pulse.enabled = body.InverterTelegraph;
                if (actor.Pulse.enabled)
                {
                    float charge = 1f - Mathf.Clamp01(body.Pulse / Tuning.InverterTelegraph);
                    actor.Pulse.color = Palette.Alpha(Palette.Inverter, 0.25f + 0.55f * charge);
                }
            }
            actor.Mark.enabled = body.Override.HasValue;
            if (actor.Mark.enabled)
                Pose(actor.Mark, body.Gravity.Vector() * 0.65f, Vector2.one * 0.29f, Palette.Psychic, ArrowAngle(body.Gravity));

            if (actor.Visor != null)
            {
                actor.Visor.flipX = actor.Body.flipX;
                actor.Visor.color = game.UsesEngineer ? Palette.Engineer : Palette.Psychic;
            }
            if (actor.Health != null)
                for (int i = 0; i < actor.Health.Length; i++)
                    actor.Health[i].color = i < body.Health ? Palette.Rgb(0xff9a8a) : Palette.Alpha(Color.white, 0.2f);
        }

        public void Draw(GameState game, Controls input, float delta)
        {
            if (roomVersion != game.RoomVersion) BuildRoom(game);
            root.localPosition = Random.insideUnitCircle * (game.Shake * Tuning.Pixel * 0.5f);
            if (doorsOpen != game.DoorOpen)
            {
                doorsOpen = game.DoorOpen;
                Sprite sprite = doorsOpen ? exitOpenSprite : exitClosedSprite;
                for (int i = 0; i < doors.Count; i++)
                {
                    doors[i].sprite = sprite;
                    doors[i].color = Color.white;
                }
            }
            if (doorsOpen)
            {
                Color doorColor = Color.white;
                doorColor.a = 0.7f + 0.25f * Mathf.Sin(game.Time * 5f);
                for (int i = 0; i < doors.Count; i++) doors[i].color = doorColor;
            }

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
                PoseTiled(zoneShapes[i], zone.Bounds.center, zone.Bounds.size, Color.white);
                Pose(zoneArrows[i], zone.Bounds.center, Vector2.one * 0.7f, Palette.Engineer, ArrowAngle(zone.Direction));
            }

            bool engineer = game.UsesEngineer;
            if (pointerUsesEngineer != engineer)
            {
                pointerUsesEngineer = engineer;
                pointer.sprite = engineer ? gravityArrowSprite : anchorRing;
            }

            pointer.enabled = input.PointerInRoom;
            Rect? bounds = null;
            Color color = engineer ? Palette.Engineer : Palette.Psychic;
            if (engineer)
            {
                if (input.PointerInRoom) bounds = game.Engineer.Preview(game.Map, input.Pointer);
                Pose(pointer, input.Pointer, Vector2.one * 0.65f, bounds.HasValue ? color : Color.gray,
                    ArrowAngle(game.Engineer.Direction));
            }
            else
            {
                var target = input.PointerInRoom ? game.PickBody(input.Pointer) : null;
                if (target != null && target != game.Psychic.Grabbed)
                {
                    var rect = target.Bounds;
                    rect.min -= Vector2.one * 0.10f;
                    rect.max += Vector2.one * 0.10f;
                    bounds = rect;
                    if (!target.Manipulable) color = Color.gray;
                }
                Pose(pointer, input.Pointer, Vector2.one * 1.1f, Color.white);
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

            var grabbed = engineer ? null : game.Psychic.Grabbed;
            tether.enabled = grabbed != null && grabbed.Alive && game.Player.Alive;
            if (tether.enabled)
            {
                Vector2 offset = grabbed.Position - game.Player.Position;
                PoseTiled(tether, game.Player.Position + offset * 0.5f, new Vector2(offset.magnitude, 0.25f),
                    Color.white, Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg);
            }
        }

        public void Burst(Vector2 center, Color color, int count, float speed)
        {
            for (int i = 0; i < count; i++)
            {
                SpriteRenderer sprite = particlePool.Count > 0 ? particlePool.Pop() : Create("Particle", root, 27, puffFrames[0]);
                sprite.sprite = puffFrames[0];
                sprite.enabled = true;
                PoseSprite(sprite, center, Vector2.one * Random.Range(0.18f, 0.30f), color);
                float duration = Random.Range(0.3f, 0.7f);
                particles.Add(new Effect
                {
                    Renderer = sprite,
                    Position = center,
                    Velocity = Random.insideUnitCircle * speed,
                    Color = color,
                    Life = duration,
                    Duration = duration
                });
            }
        }

        private void DrawParticles(float delta)
        {
            for (int i = particles.Count - 1; i >= 0; i--)
            {
                var effect = particles[i];
                effect.Life -= delta;
                effect.Age += delta;
                if (effect.Life <= 0)
                {
                    Recycle(effect.Renderer);
                    particles.RemoveAt(i);
                    continue;
                }
                effect.Position += effect.Velocity * delta;
                effect.Velocity *= Mathf.Pow(0.92f, delta * 60);
                int frame = Mathf.Min((int)(effect.Age / effect.Duration * puffFrames.Length), puffFrames.Length - 1);
                effect.Renderer.sprite = puffFrames[frame];
                effect.Renderer.transform.localPosition = effect.Position;
                effect.Renderer.color = effect.Color;
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
            if (flat != null)
            {
                Object.Destroy(flat.texture);
                Object.Destroy(flat);
            }
        }
    }
}
