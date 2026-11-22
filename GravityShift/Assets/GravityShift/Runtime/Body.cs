using UnityEngine;

namespace GravityShift
{
    internal enum BodyKind { Player, Walker, Anchor, Inverter, Crate }

    internal class Body
    {
        public readonly BodyKind Kind;
        public readonly Vector2 Size;
        public readonly int MaxHealth;
        public Vector2 Position;
        public Vector2 Velocity;
        public int Health;
        public bool Grounded;
        public bool Held;
        public float Stun;
        public float Flash;
        public float CollisionCooldown;
        public float OverrideTime;
        public float Angle;
        public int Facing;
        public float Squash;
        public float Pulse = 2f;
        public GravityDirection Gravity;
        public GravityDirection? Override;

        public Body(BodyKind kind, Vector2 position, GravityDirection gravity, int facing = 1)
        {
            Kind = kind;
            Position = position;
            Gravity = gravity;
            Angle = (int)gravity * -90;
            Size = (kind == BodyKind.Player ? new Vector2(22, 24) :
                kind == BodyKind.Crate ? new Vector2(28, 28) : new Vector2(24, 24)) * Tuning.Pixel;
            MaxHealth = kind == BodyKind.Player ? Tuning.MaxHealth : kind == BodyKind.Anchor ? 5 : kind == BodyKind.Crate ? 1 : 3;
            Health = MaxHealth;
            Facing = facing;
            Stun = IsEnemy ? 2 : 0;
        }

        public bool Alive => Health > 0;
        public bool IsEnemy => Kind == BodyKind.Walker || Kind == BodyKind.Anchor || Kind == BodyKind.Inverter;
        public bool InverterTelegraph => Kind == BodyKind.Inverter && Pulse <= Tuning.InverterTelegraph;
        public bool Manipulable => Kind != BodyKind.Anchor;
        public bool VerticalGravity => Gravity.Vector().y != 0;
        public Rect Bounds => new Rect(Position - Size * 0.5f, Size);

        public void TickTimers(float step)
        {
            Flash = Mathf.Max(0, Flash - step);
            Stun = Mathf.Max(0, Stun - step);
            CollisionCooldown = Mathf.Max(0, CollisionCooldown - step);
            Squash = Mathf.Max(0, Squash - step);
            if (!Override.HasValue) return;
            OverrideTime -= step;
            if (OverrideTime <= 0) Override = null;
        }

        public void Move(GameState game, float step, bool applyGravity = true)
        {
            Gravity = game.GravityFor(this);
            Vector2 down = Gravity.Vector();
            if (!Held && applyGravity)
            {
                Velocity += down * (Tuning.Gravity * step);
                float fallSpeed = Vector2.Dot(Velocity, down);
                if (fallSpeed > Tuning.MaxFall) Velocity -= down * (fallSpeed - Tuning.MaxFall);
            }

            bool wasGrounded = Grounded;
            Grounded = false;
            Slide(0, game, step);
            Slide(1, game, step);
            if (!wasGrounded && Grounded) Squash = 0.1f;
            Angle = Mathf.LerpAngle(Angle, (int)Gravity * -90, step * 14);
        }

        // Axis-separated tile collision keeps identical geometry for all four directions.
        private void Slide(int axis, GameState game, float step)
        {
            float distance = Velocity[axis] * step;
            if (distance == 0) return;
            Position[axis] += distance;
            RectInt cells = RoomMap.OverlappingCells(Bounds);
            float limit = Position[axis];
            bool hit = false;
            for (int row = cells.yMin; row < cells.yMax; row++)
            {
                for (int col = cells.xMin; col < cells.xMax; col++)
                {
                    if (!game.Map.IsSolid(col, row)) continue;
                    Rect tile = RoomMap.CellRect(col, row);
                    float min = axis == 0 ? tile.xMin : tile.yMin;
                    float max = axis == 0 ? tile.xMax : tile.yMax;
                    float half = Size[axis] * 0.5f;
                    limit = distance > 0 ? Mathf.Min(limit, min - half) : Mathf.Max(limit, max + half);
                    hit = true;
                }
            }
            if (!hit) return;
            Position[axis] = limit;
            Grounded |= distance * Gravity.Vector()[axis] > 0;
            float speed = Mathf.Abs(Velocity[axis]);
            Velocity[axis] = Kind == BodyKind.Crate && speed > 200 * Tuning.Pixel ? -Velocity[axis] * 0.25f : 0;
            if (speed > Tuning.WallImpact) game.WallImpact(this, speed);
        }

        public void TickNpc(GameState game, float step)
        {
            TickTimers(step);
            int axis = VerticalGravity ? 0 : 1;
            if (Grounded && !Held)
            {
                float desired = 0;
                float acceleration = Kind == BodyKind.Crate ? 700 : 600;
                if (IsEnemy && Stun <= 0)
                {
                    Vector2 delta = game.Player.Position - Position;
                    bool chase = game.Player.Alive && delta.sqrMagnitude < Mathf.Pow(200 * Tuning.Pixel, 2);
                    if (chase && Mathf.Abs(delta[axis]) > 4 * Tuning.Pixel) Facing = delta[axis] > 0 ? 1 : -1;
                    var ahead = Position;
                    ahead[axis] += Facing * (Size.x * 0.5f + 4 * Tuning.Pixel);
                    char tile = game.Map.At(ahead);
                    bool blocked = tile == '#' || tile == '^' ||
                        game.Map.At(ahead + Gravity.Vector() * (Size.y * 0.5f + 6 * Tuning.Pixel)) != '#';
                    if (!blocked || !chase)
                    {
                        if (blocked) Facing = -Facing;
                        float speed = Kind == BodyKind.Inverter ? Tuning.InverterSpeed : 75 * Tuning.Pixel;
                        desired = Facing * speed;
                    }
                    acceleration = Kind == BodyKind.Inverter ? 700 : 900;
                }
                Velocity[axis] = Mathf.MoveTowards(Velocity[axis], desired, acceleration * Tuning.Pixel * step);
            }
            Move(game, step);
        }
    }

    internal sealed class PlayerBody : Body
    {
        private float coyote;
        private float jumpBuffer;
        private float dashCooldown;
        private float attackCooldown;
        public float DashTime { get; private set; }
        public float AttackTime { get; private set; }
        public float Invulnerability;
        public float Blink;

        public PlayerBody(Vector2 position, GravityDirection gravity, int health) : base(BodyKind.Player, position, gravity)
        {
            Health = Mathf.Min(MaxHealth, health);
        }

        public Rect AttackBounds
        {
            get
            {
                const float padding = 8 * Tuning.Pixel;
                Vector2 size = VerticalGravity ? new Vector2(Tuning.AttackRange, Size.y + padding) :
                    new Vector2(Size.x + padding, Tuning.AttackRange);
                Vector2 center = Position;
                int axis = VerticalGravity ? 0 : 1;
                center[axis] += Facing * (Size[axis] + Tuning.AttackRange) * 0.5f;
                return new Rect(center - size * 0.5f, size);
            }
        }

        public void Tick(GameState game, Controls input, float step)
        {
            TickTimers(step);
            Invulnerability = Mathf.Max(0, Invulnerability - step);
            Blink = Mathf.Max(0, Blink - step);
            dashCooldown = Mathf.Max(0, dashCooldown - step);
            attackCooldown = Mathf.Max(0, attackCooldown - step);
            AttackTime = Mathf.Max(0, AttackTime - step);

            Vector2 down = Gravity.Vector();
            int axis = VerticalGravity ? 0 : 1;
            int downAxis = 1 - axis;
            float direction = input.Move[axis];
            coyote = Grounded ? 0.1f : coyote - step;
            jumpBuffer = input.Has(ActionButton.Jump) ? 0.12f : jumpBuffer - step;
            if (DashTime > 0)
            {
                DashTime -= step;
                if (Random.value < 0.6f) game.Burst(Position, Palette.Dash, 1, 30);
            }
            else
            {
                float acceleration = (Grounded ? 2600 : 1500) * Tuning.Pixel;
                Velocity[axis] = Mathf.MoveTowards(Velocity[axis], direction * Tuning.MoveSpeed, acceleration * step);
            }
            if (direction != 0) Facing = direction > 0 ? 1 : -1;

            if (jumpBuffer > 0 && coyote > 0)
            {
                Velocity[downAxis] = -down[downAxis] * Tuning.JumpSpeed;
                jumpBuffer = coyote = 0;
            }
            if (!input.JumpHeld && Vector2.Dot(Velocity, down) < -Tuning.JumpCut && DashTime <= 0)
                Velocity[downAxis] = -down[downAxis] * Tuning.JumpCut;

            if (input.Has(ActionButton.Dash) && dashCooldown <= 0)
            {
                Vector2 aim = input.Move;
                if (aim == Vector2.zero) aim[axis] = Facing;
                Velocity = aim.normalized * Tuning.DashSpeed;
                DashTime = 0.14f;
                dashCooldown = 0.55f;
                Invulnerability = Mathf.Max(Invulnerability, DashTime);
            }
            if (input.Has(ActionButton.Attack) && attackCooldown <= 0)
            {
                attackCooldown = 0.28f;
                AttackTime = 0.12f;
                game.Attack();
            }
            Move(game, step, DashTime <= 0);
        }
    }
}
