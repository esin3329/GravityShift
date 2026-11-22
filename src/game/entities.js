// 엔티티. Unity 이식 시 각 클래스 = MonoBehaviour, update() = FixedUpdate().
(function (GS) {
  'use strict';
  const C = GS.Config, Vec2 = GS.Vec2, M = GS.Mathf;
  let nextId = 1;

  class Entity {
    constructor(x, y, w, h, kind) {
      this.id = nextId++;
      this.kind = kind;
      this.pos = new Vec2(x, y);
      this.vel = new Vec2();
      this.w = w; this.h = h;
      this.alive = true;
      this.grounded = false;
      this.gravity = GS.Dir.DOWN;
      this.gravOverride = null; // 사이킥 개별 반전
      this.overrideTimer = 0;
      this.manipulable = true;  // false 면 중력 조작 무효 (앵커)
      this.held = false;        // 염동력으로 잡힌 상태
      this.damageable = false;
      this.impactDamage = true; // 낙하/충돌 충격으로 피해를 입는가
      this.hp = 1; this.maxHp = 1;
      this.flash = 0; this.hitCd = 0;
      this.bounce = 0;
      this.visualAngle = 0;
    }

    get rect() { return { x: this.pos.x - this.w / 2, y: this.pos.y - this.h / 2, w: this.w, h: this.h }; }
    get isEnemy() { return false; }

    // 우선순위: 개별 반전 > 발판 구역 > 방 기본 중력
    computeGravity(game) {
      if (this.manipulable) {
        if (this.gravOverride) return this.gravOverride;
        const z = game.zoneGravityAt(this.pos);
        if (z) return z;
      }
      return game.roomGravity;
    }

    tickTimers(dt) {
      this.flash = Math.max(0, this.flash - dt);
      this.hitCd = Math.max(0, this.hitCd - dt);
      if (this.gravOverride && this.overrideTimer !== Infinity) {
        this.overrideTimer -= dt;
        if (this.overrideTimer <= 0) this.gravOverride = null;
      }
    }

    physicsStep(game, dt, gravityScale = 1) {
      this.gravity = this.computeGravity(game);
      const g = this.gravity;
      if (!this.held && gravityScale > 0) {
        this.vel.x += g.x * C.GRAVITY * gravityScale * dt;
        this.vel.y += g.y * C.GRAVITY * gravityScale * dt;
        const along = this.vel.dot(g);
        if (along > C.MAX_FALL) {
          this.vel.x -= g.x * (along - C.MAX_FALL);
          this.vel.y -= g.y * (along - C.MAX_FALL);
        }
      }
      GS.Physics.move(this, game, dt);
      this.visualAngle = M.lerpAngle(this.visualAngle, GS.gravAngle(g), Math.min(1, dt * 14));
    }

    onImpact(speed, game) {
      if (speed <= C.impact.wall) return;
      game.spawnBurst(this.pos.x, this.pos.y, '#ffffff', 6, 140);
      if (speed > 700) game.addShake(5);
      if (this.damageable && this.impactDamage) {
        this.damage(1 + Math.floor((speed - C.impact.wall) / C.impact.perDamage), game);
      }
    }

    damage(n, game) {
      if (!this.alive) return;
      this.hp -= n;
      this.flash = 0.15;
      if (this.hp <= 0) { this.alive = false; this.onDeath(game); }
    }

    onDeath(game) {
      game.spawnBurst(this.pos.x, this.pos.y, '#ff7a5c', 20, 260);
      game.addShake(4);
    }
  }

  class Player extends Entity {
    constructor(x, y) {
      const P = C.player;
      super(x, y, P.w, P.h, 'player');
      this.hp = this.maxHp = P.hp;
      this.damageable = true;
      this.impactDamage = false;
      this.facing = 1;
      this.invuln = 0; this.blink = 0;
      this.coyote = 0; this.jumpBuffer = 0;
      this.dashTimer = 0; this.dashCd = 0;
      this.attackTimer = 0; this.attackCd = 0;
    }

    // 중력이 상하면 x축, 좌우면 y축으로 걷는다
    get moveAxis() { return this.gravity.y !== 0 ? 'x' : 'y'; }

    update(game, dt, input) {
      const P = C.player;
      this.tickTimers(dt);
      this.invuln = Math.max(0, this.invuln - dt);
      this.blink = Math.max(0, this.blink - dt);
      this.dashCd = Math.max(0, this.dashCd - dt);
      this.attackCd = Math.max(0, this.attackCd - dt);
      this.attackTimer = Math.max(0, this.attackTimer - dt);

      const g = this.gravity;
      const axis = this.moveAxis;
      const gAxis = axis === 'x' ? 'y' : 'x';
      const inp = axis === 'x' ? input.axisX : input.axisY;

      this.coyote = this.grounded ? P.coyote : this.coyote - dt;
      this.jumpBuffer = input.pressed('Space') ? P.jumpBuffer : this.jumpBuffer - dt;

      if (this.dashTimer > 0) {
        this.dashTimer -= dt;
        if (Math.random() < 0.6) game.spawnBurst(this.pos.x, this.pos.y, '#9fe8ff', 1, 30);
      } else {
        const accel = this.grounded ? P.accel : P.airAccel;
        this.vel[axis] = M.approach(this.vel[axis], inp * P.speed, accel * dt);
      }
      if (inp !== 0) this.facing = Math.sign(inp);

      // 점프 (코요테 타임 + 입력 버퍼)
      if (this.jumpBuffer > 0 && this.coyote > 0) {
        this.vel[gAxis] = -g[gAxis] * P.jump;
        this.jumpBuffer = 0; this.coyote = 0;
      }
      // 가변 점프: 버튼을 떼면 상승 속도 감소
      if (!input.key('Space') && this.vel.dot(g) < -P.jumpCut && this.dashTimer <= 0) {
        this.vel[gAxis] = -g[gAxis] * P.jumpCut;
      }

      // 대시 (화면 기준 8방향)
      if ((input.pressed('ShiftLeft') || input.pressed('ShiftRight')) && this.dashCd <= 0) {
        let dx = input.axisX, dy = input.axisY;
        if (!dx && !dy) { if (axis === 'x') dx = this.facing; else dy = this.facing; }
        this.vel = new Vec2(dx, dy).normalized.scale(P.dashSpeed);
        this.dashTimer = P.dashTime; this.dashCd = P.dashCd;
        this.invuln = Math.max(this.invuln, P.dashTime);
      }

      if (input.pressed('KeyJ') && this.attackCd <= 0) {
        this.attackCd = P.attackCd;
        this.attackTimer = 0.12;
        game.playerAttack(this);
      }

      this.physicsStep(game, dt, this.dashTimer > 0 ? 0 : 1);
    }

    attackRect() {
      const R = C.player.attackRange;
      if (this.moveAxis === 'x') {
        return { x: this.pos.x + this.facing * (this.w / 2 + R / 2) - R / 2, y: this.pos.y - this.h / 2 - 4, w: R, h: this.h + 8 };
      }
      return { x: this.pos.x - this.w / 2 - 4, y: this.pos.y + this.facing * (this.h / 2 + R / 2) - R / 2, w: this.w + 8, h: R };
    }

    hurt(n, game, fromPos) {
      if (this.invuln > 0 || !this.alive) return;
      this.damage(n, game);
      this.invuln = C.player.invuln;
      this.blink = C.player.invuln;
      const away = this.pos.sub(fromPos).normalized;
      this.vel = away.scale(300).add(this.gravity.neg().scale(350));
      game.addShake(7);
      game.spawnBurst(this.pos.x, this.pos.y, '#ff5c7a', 10, 200);
    }

    onDeath(game) {
      game.spawnBurst(this.pos.x, this.pos.y, '#eaf6ff', 30, 300);
      game.onPlayerDeath();
    }
  }

  class Enemy extends Entity {
    constructor(x, y, anchor) {
      const E = C.enemy;
      super(x, y, E.w, E.h, anchor ? 'anchor' : 'enemy');
      this.anchor = anchor;
      this.hp = this.maxHp = anchor ? E.anchorHp : E.hp;
      this.damageable = true;
      this.manipulable = !anchor;
      this.dir = Math.random() < 0.5 ? -1 : 1;
      this.stun = 0;
    }

    get isEnemy() { return true; }

    blocked(game, d, axis) {
      const g = this.gravity;
      const ahead = this.pos.clone();
      ahead[axis] += d * (this.w / 2 + 4);
      const tAhead = game.map.tileAtPoint(ahead);
      if (tAhead === '#' || tAhead === '^') return true;
      const below = ahead.add(g.scale(this.h / 2 + 6));
      return game.map.tileAtPoint(below) !== '#'; // 낭떠러지
    }

    update(game, dt) {
      this.tickTimers(dt);
      this.stun = Math.max(0, this.stun - dt);
      const axis = this.gravity.y !== 0 ? 'x' : 'y';

      if (this.grounded && this.stun <= 0 && !this.held) {
        const p = game.player;
        const chasing = p.alive && p.pos.sub(this.pos).magnitude < C.enemy.chaseRange;
        if (chasing && Math.abs(p.pos[axis] - this.pos[axis]) > 4) this.dir = Math.sign(p.pos[axis] - this.pos[axis]);
        let want = this.dir;
        if (this.blocked(game, want, axis)) {
          if (chasing) want = 0;
          else { this.dir = -this.dir; want = this.dir; }
        }
        this.vel[axis] = M.approach(this.vel[axis], want * C.enemy.speed, 900 * dt);
      } else if (this.grounded) {
        this.vel[axis] = M.approach(this.vel[axis], 0, 600 * dt);
      }
      this.physicsStep(game, dt);
    }
  }

  class Box extends Entity {
    constructor(x, y) {
      super(x, y, C.box.w, C.box.h, 'box');
      this.bounce = C.box.bounce;
    }
    update(game, dt) {
      this.tickTimers(dt);
      if (this.grounded && !this.held) {
        const axis = this.gravity.y !== 0 ? 'x' : 'y';
        this.vel[axis] = M.approach(this.vel[axis], 0, 700 * dt);
      }
      this.physicsStep(game, dt);
    }
  }

  GS.Entity = Entity;
  GS.Player = Player;
  GS.Enemy = Enemy;
  GS.Box = Box;
})(window.GS = window.GS || {});
