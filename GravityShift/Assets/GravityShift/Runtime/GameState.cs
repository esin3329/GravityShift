using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace GravityShift
{
    internal sealed class GameState
    {
        private List<RoomDefinition> run;
        private int entryHealth;
        private float restartTimer;

        public uint Seed { get; private set; }
        public float Time { get; private set; }
        public int RoomIndex { get; private set; }
        public int RoomVersion { get; private set; }
        public RoomDefinition Room => run[RoomIndex];
        public RoomMap Map { get; private set; }
        public PlayerBody Player { get; private set; }
        public readonly List<Body> Bodies = new List<Body>();
        public readonly Psychic Psychic = new Psychic();
        public readonly Engineer Engineer = new Engineer();
        public bool UsesEngineer { get; private set; }
        public GravityDirection RoomGravity { get; private set; }
        public float RotationTimer { get; private set; }
        public bool DoorOpen { get; private set; }
        public int EnemiesLeft { get; private set; }
        public string BannerTitle { get; private set; }
        public string BannerSubtitle { get; private set; }
        public float BannerTime { get; private set; }
        public float Shake { get; private set; }
        public event Action<Vector2, Color, int, float> BurstRequested;
        public event Action<Vector2, string> NoteRequested;

        public GameState(uint? seed) => StartRun(seed);

        private void StartRun(uint? seed = null)
        {
            Seed = seed ?? (uint)Random.Range(0, 1000000000);
            run = RoomGenerator.BuildRun(Seed);
            LoadRoom(0, Tuning.MaxHealth);
        }

        private void LoadRoom(int index, int health)
        {
            RoomIndex = index;
            RoomVersion++;
            entryHealth = Mathf.Min(health, Tuning.MaxHealth);
            restartTimer = 0;
            Shake = 0;
            Map = new RoomMap(Room.Map);
            RoomGravity = Room.Gravity;
            RotationTimer = Room.RotateEvery;
            Player = new PlayerBody(Map.PlayerStart, RoomGravity, entryHealth);
            Psychic.Reset();
            Engineer.Reset();
            Bodies.Clear();
            AddBodies(Map.Walkers, BodyKind.Walker);
            AddBodies(Map.Anchors, BodyKind.Anchor);
            AddBodies(Map.Crates, BodyKind.Crate);
            EnemiesLeft = Map.Walkers.Count + Map.Anchors.Count;
            DoorOpen = false;
            ShowBanner(Room.Title, Room.Subtitle, 2.6f);
        }

        private void AddBodies(IEnumerable<Vector2> positions, BodyKind kind)
        {
            foreach (Vector2 pos in positions) Bodies.Add(new Body(kind, pos, RoomGravity));
        }

        public GravityDirection GravityFor(Body body)
        {
            if (!body.Manipulable) return RoomGravity;
            return body.Override ?? (UsesEngineer ? Engineer.DirectionAt(body.Position) : null) ?? RoomGravity;
        }

        public Body PickBody(Vector2 point)
        {
            float best = float.PositiveInfinity;
            Body result = null;
            foreach (var body in Bodies)
            {
                if (!body.Alive) continue;
                Rect bounds = body.Bounds;
                const float padding = 8 * Tuning.Pixel;
                bounds.min -= Vector2.one * padding;
                bounds.max += Vector2.one * padding;
                if (!bounds.Contains(point)) continue;
                float distance = (body.Position - point).sqrMagnitude;
                if (distance >= best) continue;
                result = body;
                best = distance;
            }
            return result;
        }

        public void Tick(Controls input, float dt)
        {
            Time += dt;
            Shake = Mathf.Max(0, Shake - dt * 40);
            BannerTime = Mathf.Max(0, BannerTime - dt);
            if (restartTimer > 0)
            {
                restartTimer -= dt;
                if (restartTimer <= 0) StartRun();
                return;
            }

            if (input.Has(ActionButton.Style))
            {
                if (UsesEngineer) Engineer.Reset();
                else Psychic.Reset();
                UsesEngineer = !UsesEngineer;
                Player.Override = null;
                Note(Player.Position, UsesEngineer ? "엔지니어" : "사이킥");
            }
            if (input.Has(ActionButton.Retry))
            {
                LoadRoom(RoomIndex, entryHealth);
                return;
            }
            if (input.Has(ActionButton.NewRun))
            {
                StartRun();
                return;
            }

            if (Room.RotateEvery > 0)
            {
                RotationTimer -= dt;
                if (RotationTimer <= 0)
                {
                    RoomGravity = RoomGravity.Turn(1);
                    RotationTimer += Room.RotateEvery;
                    AddShake(8);
                }
            }
            if (UsesEngineer) Engineer.Tick(this, input, dt);
            else Psychic.Tick(this, input, dt);
            Player.Tick(this, input, dt);
            foreach (var body in Bodies)
                if (body.Alive) body.TickNpc(this, dt);
            ResolveInteractions();
            Bodies.RemoveAll(body => !body.Alive);

            EnemiesLeft = 0;
            foreach (var body in Bodies)
                if (body.IsEnemy) EnemiesLeft++;
            if (!DoorOpen && EnemiesLeft == 0)
            {
                DoorOpen = true;
                Note(Player.Position, "출구 개방!");
            }

            if (!DoorOpen || !Player.Alive || !Map.ContainsTile(Player.Bounds, 'D', 2 * Tuning.Pixel)) return;
            if (RoomIndex + 1 < run.Count) LoadRoom(RoomIndex + 1, Player.Health + 1);
            else
            {
                restartTimer = 4;
                ShowBanner("프로토타입 클리어!", "처음부터 다시 시작합니다", restartTimer);
            }
        }

        private void ResolveInteractions()
        {
            if (!Player.Alive) return;

            foreach (var body in Bodies)
            {
                if (!body.Alive || !body.IsEnemy) continue;
                if (!body.Held && body.Stun <= 0 && Player.Invulnerability <= 0 && Player.Bounds.Overlaps(body.Bounds))
                {
                    HurtPlayer(body.Position);
                    body.Velocity = ((body.Position - Player.Position).normalized * 220 - body.Gravity.Vector() * 200) * Tuning.Pixel;
                    body.Stun = 0.7f;
                }
                if (Map.ContainsTile(body.Bounds, '^', 4 * Tuning.Pixel)) Damage(body, body.Health);
            }
            if (Map.ContainsTile(Player.Bounds, '^', 4 * Tuning.Pixel))
                HurtPlayer(Player.Position + Player.Gravity.Vector());

            foreach (var source in Bodies)
            {
                if (!source.Alive || source.CollisionCooldown > 0) continue;
                float speed = source.Velocity.magnitude;
                if (speed < Tuning.BodyImpact) continue;
                foreach (var target in Bodies)
                {
                    if (target == source || !target.Alive || !target.IsEnemy || !source.Bounds.Overlaps(target.Bounds)) continue;
                    Damage(target, 1 + Mathf.FloorToInt((speed - Tuning.BodyImpact) / Tuning.ImpactStep));
                    target.Velocity += source.Velocity * 0.6f;
                    target.Stun = 0.5f;
                    source.Velocity *= -0.3f;
                    source.CollisionCooldown = target.CollisionCooldown = 0.25f;
                    Burst((source.Position + target.Position) * 0.5f, Palette.Sparks, 12, 220);
                    AddShake(5);
                    break;
                }
            }
        }

        private void HurtPlayer(Vector2 origin)
        {
            if (Player.Invulnerability > 0 || !Player.Alive) return;
            Damage(Player, 1);
            Player.Invulnerability = Player.Blink = 1;
            Player.Velocity = ((Player.Position - origin).normalized * 300 - Player.Gravity.Vector() * 350) * Tuning.Pixel;
            AddShake(7);
            Burst(Player.Position, Palette.Spikes, 10, 200);
        }

        public void Attack()
        {
            float axis = Player.Facing * 420;
            Vector2 knockback = Player.VerticalGravity ? new Vector2(axis, 0) : new Vector2(0, axis);
            foreach (var body in Bodies)
            {
                if (!body.Alive || !Player.AttackBounds.Overlaps(body.Bounds)) continue;
                body.Velocity = (knockback - body.Gravity.Vector() * 300) * Tuning.Pixel;
                body.CollisionCooldown = 0.12f;
                if (body.IsEnemy)
                {
                    Damage(body, 1);
                    body.Stun = 0.45f;
                }
                Burst(body.Position, Color.white, 8, 180);
                AddShake(3);
            }
        }

        public void WallImpact(Body body, float speed)
        {
            Burst(body.Position, Color.white, 6, 140);
            if (speed > 700 * Tuning.Pixel) AddShake(5);
            if (body.IsEnemy) Damage(body, 1 + Mathf.FloorToInt((speed - Tuning.WallImpact) / Tuning.ImpactStep));
        }

        private void Damage(Body body, int amount)
        {
            if (!body.Alive) return;
            body.Health -= amount;
            body.Flash = 0.15f;
            if (body.Alive) return;
            if (body == Player)
            {
                Burst(body.Position, Palette.Dash, 30, 300);
                restartTimer = 2.4f;
                ShowBanner("런 실패", "처음부터 다시 시작합니다", restartTimer);
            }
            else
            {
                Burst(body.Position, Palette.Walker, 20, 260);
                AddShake(4);
            }
        }

        private void ShowBanner(string title, string subtitle, float seconds)
        {
            BannerTitle = title;
            BannerSubtitle = subtitle;
            BannerTime = seconds;
        }

        public void AddShake(float amount) => Shake = Mathf.Max(Shake, amount);
        public void Burst(Vector2 point, Color color, int count, float speed) => BurstRequested?.Invoke(point, color, count, speed * Tuning.Pixel);
        public void Note(Vector2 point, string message) => NoteRequested?.Invoke(point, message);
    }
}
