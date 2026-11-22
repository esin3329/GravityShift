using System.Collections.Generic;
using UnityEngine;

namespace GravityShift
{
    internal sealed class Psychic
    {
        public float Mana { get; private set; } = Tuning.MaxMana;
        public Body Grabbed { get; private set; }

        public void Reset()
        {
            Release();
            Mana = Tuning.MaxMana;
        }

        private void Release()
        {
            if (Grabbed == null) return;
            Grabbed.Held = false;
            Grabbed = null;
        }

        public void Tick(GameState game, Controls input, float step)
        {
            if (Grabbed == null) Mana = Mathf.Min(Tuning.MaxMana, Mana + 18 * step);
            if (input.Has(ActionButton.Primary))
            {
                var target = game.PickBody(input.Pointer);
                if (target != null)
                {
                    if (!target.Manipulable) game.Note(target.Position, "무효");
                    else if (Mana < Tuning.FlipCost) game.Note(game.Player.Position, "정신력 부족");
                    else
                    {
                        Flip(target, Tuning.FlipDuration, game);
                        if (target.IsEnemy) target.Stun = Mathf.Max(target.Stun, 0.3f);
                    }
                }
            }

            if (input.Has(ActionButton.Secondary) && Grabbed == null)
            {
                var target = game.PickBody(input.Pointer);
                if (target != null && target.Manipulable && Mana > 5)
                {
                    Grabbed = target;
                    target.Held = true;
                }
                else if (target != null) game.Note(target.Position, target.Manipulable ? "정신력 부족" : "무효");
            }
            if (Grabbed != null)
            {
                if (!Grabbed.Alive || !input.SecondaryHeld || Mana <= 0) Release();
                else
                {
                    Grabbed.Velocity = Vector2.ClampMagnitude((input.Pointer - Grabbed.Position) * 12, 1000 * Tuning.Pixel);
                    Mana = Mathf.Max(0, Mana - 32 * step);
                    if (Grabbed.IsEnemy) Grabbed.Stun = Mathf.Max(Grabbed.Stun, 0.35f);
                }
            }

            if (!input.Has(ActionButton.Flip)) return;
            if (game.Player.Override.HasValue) game.Player.Override = null;
            else if (Mana >= Tuning.FlipCost) Flip(game.Player, float.PositiveInfinity, game);
            else game.Note(game.Player.Position, "정신력 부족");
        }

        private void Flip(Body body, float duration, GameState game)
        {
            body.Override = body.Gravity.Turn(2);
            body.OverrideTime = duration;
            Mana -= Tuning.FlipCost;
            game.Burst(body.Position, Palette.Psychic, 12, 160);
        }
    }

    internal sealed class GravityZone
    {
        public readonly Rect Bounds;
        public GravityDirection Direction;

        public GravityZone(Rect bounds, GravityDirection direction)
        {
            Bounds = bounds;
            Direction = direction;
        }
    }

    internal sealed class Engineer
    {
        private float cooldown;
        public GravityDirection Direction { get; private set; }
        public readonly List<GravityZone> Zones = new List<GravityZone>(Tuning.MaxZones);

        public void Reset()
        {
            cooldown = 0;
            Direction = GravityDirection.Down;
            Zones.Clear();
        }

        public GravityDirection? DirectionAt(Vector2 point)
        {
            for (int i = Zones.Count - 1; i >= 0; i--)
                if (Zones[i].Bounds.Contains(point)) return Zones[i].Direction;
            return null;
        }

        public Rect? Preview(RoomMap map, Vector2 pointer)
        {
            int col = Mathf.FloorToInt(pointer.x);
            int row = Mathf.FloorToInt(-pointer.y);
            int half = Tuning.BeamWidth / 2;
            if (map.IsSolid(col, row)) return null;

            if (Direction.Vector().y != 0)
            {
                int first = row, last = row;
                while (!map.IsSolid(col, first - 1)) first--;
                while (!map.IsSolid(col, last + 1)) last++;
                return new Rect(col - half, -last - 1, Tuning.BeamWidth, last - first + 1);
            }

            int left = col, right = col;
            while (!map.IsSolid(left - 1, row)) left--;
            while (!map.IsSolid(right + 1, row)) right++;
            return new Rect(left, -row - half - 1, right - left + 1, Tuning.BeamWidth);
        }

        public void Tick(GameState game, Controls input, float step)
        {
            cooldown = Mathf.Max(0, cooldown - step);
            if (input.Wheel != 0) Direction = Direction.Turn(Mathf.Sign(input.Wheel) > 0 ? -1 : 1);
            if (input.Has(ActionButton.Rotate)) Direction = Direction.Turn(1);

            if (input.Has(ActionButton.Primary) && cooldown <= 0)
            {
                Rect? bounds = Preview(game.Map, input.Pointer);
                if (bounds.HasValue)
                {
                    Zones.Add(new GravityZone(bounds.Value, Direction));
                    if (Zones.Count > Tuning.MaxZones) Zones.RemoveAt(0);
                    cooldown = 0.25f;
                    game.Burst(bounds.Value.center, Palette.Engineer, 14, 200);
                }
            }

            if (input.Has(ActionButton.Secondary))
            {
                for (int i = Zones.Count - 1; i >= 0; i--)
                {
                    if (!Zones[i].Bounds.Contains(input.Pointer)) continue;
                    Zones.RemoveAt(i);
                    break;
                }
            }
            if (input.Has(ActionButton.Flip) && Zones.Count > 0)
            {
                foreach (var zone in Zones) zone.Direction = zone.Direction.Turn(2);
                game.AddShake(4);
            }
        }
    }
}
