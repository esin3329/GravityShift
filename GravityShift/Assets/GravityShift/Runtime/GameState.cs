using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace GravityShift
{
    internal sealed class GameState
    {
        // Match the HUD's rotation warning window.
        private const float RotationWarningTime = 1.5f;
        private const float ImpactCueInterval = 0.16f;

        private List<RoomDefinition> run;
        private int entryHealth;
        private float restartTimer;
        private float nextImpactCueTime;

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
        public float Hitstop { get; private set; }
        public bool Paused { get; private set; }
        public event Action<Vector2, Color, int, float> BurstRequested;
        public event Action<Vector2, string> NoteRequested;
        public event Action<SfxKind> SfxRequested;
        private int spawnSerial;

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
            Hitstop = 0;
            Paused = false;
            spawnSerial = 0;
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
            AddBodies(Map.Inverters, BodyKind.Inverter);
            AddBodies(Map.Crates, BodyKind.Crate);
            EnemiesLeft = Map.Walkers.Count + Map.Anchors.Count + Map.Inverters.Count;
            DoorOpen = false;
            ShowBanner(Room.Title, Room.Subtitle, 2.6f);
        }

        private void AddBodies(IEnumerable<Vector2> positions, BodyKind kind)
        {
            foreach (Vector2 pos in positions)
            {
                int facing = kind == BodyKind.Walker || kind == BodyKind.Anchor || kind == BodyKind.Inverter
                    ? SeedRandom.FacingSign(Seed, RoomIndex, spawnSerial++) : 1;
                Bodies.Add(new Body(kind, pos, RoomGravity, facing));
            }
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
            if (input.Has(ActionButton.Pause))
            {
                Paused = !Paused;
                PlaySfx(SfxKind.Click);
            }
            if (input.Has(ActionButton.NewRun))
            {
                StartRun();
                return;
            }
            if (input.Has(ActionButton.Retry))
            {
                LoadRoom(RoomIndex, entryHealth);
                return;
            }
            if (Paused) return;
            if (Hitstop > 0f)
            {
                Hitstop = Mathf.Max(0f, Hitstop - dt);
                return;
            }

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
                else Psychic.Disarm();
                UsesEngineer = !UsesEngineer;
                Player.Override = null;
                Note(Player.Position, UsesEngineer ? "엔지니어" : "사이킥");
                PlaySfx(SfxKind.Click);
            }

            if (Room.RotateEvery > 0)
            {
                float previousTimer = RotationTimer;
                RotationTimer -= dt;
                if (RotationTimer <= 0)
                {
                    RoomGravity = RoomGravity.Turn(1);
                    RotationTimer += Room.RotateEvery;
                    AddShake(8);
                }
                else if (previousTimer >= RotationWarningTime && RotationTimer < RotationWarningTime)
                    PlaySfx(SfxKind.RotationWarning);
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
            {
                if (body.IsEnemy) EnemiesLeft++;
                if (body.Kind != BodyKind.Inverter) continue;
                // Apply pulses after every body's timers and movement have advanced.
                body.Pulse -= dt;
                if (body.Pulse > 0f || body.Stun > 0f || body.Held) continue;
                body.Pulse = Tuning.InverterPeriod;
                InvertPulse(body);
            }
            if (!DoorOpen && EnemiesLeft == 0)
            {
                DoorOpen = true;
                Note(Player.Position, "출구 개방!");
                Burst(Player.Position, Palette.Accents[Room.Zone], 18, 200);
                PlaySfx(SfxKind.ExitOpen);
            }

            if (!DoorOpen || !Player.Alive || !Map.ContainsTile(Player.Bounds, 'D', 2 * Tuning.Pixel)) return;
            if (RoomIndex + 1 < run.Count) LoadRoom(RoomIndex + 1, Player.Health + 1);
            else EndRun("프로토타입 클리어!", SfxKind.Clear);
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
            // Enemy collisions already give damage feedback through Hit.
            else if (Time >= nextImpactCueTime)
            {
                nextImpactCueTime = Time + ImpactCueInterval;
                PlaySfx(SfxKind.Impact);
            }
        }

        private void Damage(Body body, int amount)
        {
            if (!body.Alive) return;
            body.Health -= amount;
            body.Flash = 0.15f;
            RequestHitstop();
            if (body.Alive)
            {
                if (body == Player) PlaySfx(SfxKind.Hurt);
                else if (body.IsEnemy) PlaySfx(SfxKind.Hit);
                return;
            }
            if (body == Player)
            {
                Burst(body.Position, Palette.Dash, 30, 300);
                EndRun("런 실패", SfxKind.Death);
            }
            else
            {
                Color death = body.Kind == BodyKind.Anchor ? Palette.Anchor :
                    body.Kind == BodyKind.Inverter ? Palette.Inverter : Palette.Walker;
                Burst(body.Position, death, 20, 260);
                PlaySfx(SfxKind.Hit);
                AddShake(4);
            }
        }

        private void EndRun(string title, SfxKind cue)
        {
            restartTimer = 4f;
            ShowBanner(title, $"시드 {Seed} · {RoomIndex + 1}/{run.Count}방 · N 새 런 · R 이 방", restartTimer);
            PlaySfx(cue);
        }

        private void ShowBanner(string title, string subtitle, float seconds)
        {
            BannerTitle = title;
            BannerSubtitle = subtitle;
            BannerTime = seconds;
        }

        private void InvertPulse(Body source)
        {
            void Flip(Body body)
            {
                if (!body.Alive || !body.Manipulable || body == source) return;
                if ((body.Position - source.Position).sqrMagnitude > Tuning.InverterRadius * Tuning.InverterRadius) return;
                body.Override = GravityFor(body).Turn(2);
                body.OverrideTime = Tuning.InverterDuration;
            }

            Flip(Player);
            foreach (Body body in Bodies) Flip(body);
            Burst(source.Position, Palette.Inverter, 16, 180);
            PlaySfx(SfxKind.Pulse);
        }

        public void AddShake(float amount) => Shake = Mathf.Max(Shake, amount);
        public void Burst(Vector2 point, Color color, int count, float speed) => BurstRequested?.Invoke(point, color, count, speed * Tuning.Pixel);
        public void Note(Vector2 point, string message) => NoteRequested?.Invoke(point, message);
        public void PlaySfx(SfxKind kind) => SfxRequested?.Invoke(kind);
        private void RequestHitstop() { if (Hitstop <= 0f) Hitstop = 0.06f; }
    }
}
