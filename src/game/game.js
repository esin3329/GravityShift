// 게임 상태와 규칙. Unity 이식 시 GameManager(싱글턴) + RoomManager 로 나눈다.
(function (GS) {
  'use strict';
  const C = GS.Config, Rect = GS.Rect;

  class Game {
    constructor(canvas) {
      this.canvas = canvas;
      this.input = new GS.Input(canvas);
      this.abilities = { psychic: new GS.Psychic(), engineer: new GS.Engineer() };
      this.style = 'psychic';
      this.time = 0;
      this.particles = [];
      this.texts = [];
      this.shakeAmt = 0;
      this.banner = null;
      this.deadTimer = 0;
      this.victoryTimer = 0;
      const urlSeed = new URLSearchParams(location.search).get('seed');
      this.startRun(urlSeed !== null && urlSeed !== '' && !isNaN(urlSeed) ? Number(urlSeed) : undefined);
    }

    get ability() { return this.abilities[this.style]; }

    // 새 런: 시드로 방 목록 전체를 미리 생성한다
    startRun(seed) {
      this.seed = seed === undefined ? GS.RNG.randomSeed() : seed >>> 0;
      this.run = GS.RoomGen.buildRun(this.seed);
      this.loadRoom(0);
    }

    loadRoom(index, hp) {
      const R = this.run[index];
      this.roomIndex = index;
      this.room = R;
      this.deadTimer = 0;
      this.victoryTimer = 0;
      this.map = new GS.Tilemap(R.map);
      this.rotIndex = GS.DirOrder.indexOf(R.gravity);
      this.roomGravity = GS.Dir[R.gravity];
      this.rotateTimer = R.rotateEvery || 0;

      const s = this.map.spawns;
      this.player = new GS.Player(s.player.x, s.player.y);
      if (hp !== undefined) this.player.hp = Math.min(this.player.maxHp, hp);
      this.roomEntryHp = this.player.hp;

      this.entities = [];
      s.enemies.forEach((p) => this.entities.push(new GS.Enemy(p.x, p.y, false)));
      s.anchors.forEach((p) => this.entities.push(new GS.Enemy(p.x, p.y, true)));
      s.boxes.forEach((p) => this.entities.push(new GS.Box(p.x, p.y)));
      for (const e of [this.player, ...this.entities]) {
        e.gravity = this.roomGravity;
        e.visualAngle = GS.gravAngle(this.roomGravity);
        if (e.isEnemy) e.stun = 2; // 방 입장 직후 유예 시간
      }

      this.abilities.psychic.reset();
      this.abilities.engineer.reset();
      this.doorOpen = false;
      this.particles = [];
      this.texts = [];
      this.banner = { title: R.name, sub: R.subtitle, t: 2.6 };
    }

    nextRoom() {
      if (this.roomIndex + 1 < this.run.length) {
        this.loadRoom(this.roomIndex + 1, this.player.hp + 1);
      } else {
        this.victoryTimer = 4;
        this.banner = { title: '프로토타입 클리어!', sub: '처음부터 다시 시작합니다', t: 4 };
      }
    }

    onPlayerDeath() {
      this.deadTimer = 2.4;
      this.banner = { title: '런 실패', sub: '처음부터 다시 시작합니다', t: 2.4 };
    }

    switchStyle() {
      this.ability.reset();
      this.style = this.style === 'psychic' ? 'engineer' : 'psychic';
      this.player.gravOverride = null;
      this.floatText(this.player.pos.x, this.player.pos.y - 26, this.ability.name, GS.StyleColors[this.style]);
    }

    zoneGravityAt(p) { return this.ability.zoneAt(p); }

    pickEntity(p) {
      let best = null, bd = Infinity;
      for (const e of this.entities) {
        if (!e.alive || !Rect.contains(e.rect, p, 8)) continue;
        const d = e.pos.sub(p).magnitude;
        if (d < bd) { bd = d; best = e; }
      }
      return best;
    }

    update(dt) {
      const input = this.input;
      this.time += dt;
      this.shakeAmt = Math.max(0, this.shakeAmt - dt * 40);
      this.updateParticles(dt);
      if (this.banner) { this.banner.t -= dt; if (this.banner.t <= 0) this.banner = null; }

      if (this.deadTimer > 0) { this.deadTimer -= dt; if (this.deadTimer <= 0) this.startRun(); return; }
      if (this.victoryTimer > 0) { this.victoryTimer -= dt; if (this.victoryTimer <= 0) this.startRun(); return; }

      if (input.pressed('Tab')) this.switchStyle();
      if (input.pressed('KeyR')) { this.loadRoom(this.roomIndex, this.roomEntryHp); return; }
      if (input.pressed('KeyN')) { this.startRun(); return; }

      if (this.room.rotateEvery) {
        this.rotateTimer -= dt;
        if (this.rotateTimer <= 0) {
          this.rotIndex = (this.rotIndex + 1) % 4;
          this.roomGravity = GS.Dir[GS.DirOrder[this.rotIndex]];
          this.rotateTimer = this.room.rotateEvery;
          this.addShake(8);
        }
      }

      this.ability.update(this, dt, input);
      this.player.update(this, dt, input);
      for (const e of this.entities) if (e.alive) e.update(this, dt);
      this.resolveInteractions();
      this.entities = this.entities.filter((e) => e.alive);

      if (!this.doorOpen && !this.entities.some((e) => e.isEnemy)) {
        this.doorOpen = true;
        this.floatText(this.player.pos.x, this.player.pos.y - 26, '출구 개방!', this.room.theme.accent);
      }
      if (this.doorOpen && this.player.alive && this.map.rectHasTile(this.player.rect, 'D', 2)) this.nextRoom();
    }

    resolveInteractions() {
      const p = this.player;
      if (!p.alive) return;

      for (const e of this.entities) {
        if (!e.alive) continue;
        if (e.isEnemy && !e.held && e.stun <= 0 && p.invuln <= 0 && Rect.overlaps(p.rect, e.rect)) {
          p.hurt(1, this, e.pos);
          // 때린 적도 튕겨나가서 구석에 몰린 연속 피격을 막는다
          e.vel = e.pos.sub(p.pos).normalized.scale(220).add(e.gravity.neg().scale(200));
          e.stun = 0.7;
        }
        if (e.damageable && this.map.rectHasTile(e.rect, '^', 4)) e.damage(99, this);
      }
      if (this.map.rectHasTile(p.rect, '^', 4)) p.hurt(1, this, p.pos.add(p.gravity.scale(20)));

      // 빠르게 날아가는 물체가 적과 부딪히면 피해
      for (const a of this.entities) {
        if (!a.alive || a.hitCd > 0) continue;
        const speed = a.vel.magnitude;
        if (speed < C.impact.collide) continue;
        for (const b of this.entities) {
          if (b === a || !b.alive || !b.damageable || !Rect.overlaps(a.rect, b.rect)) continue;
          b.damage(1 + Math.floor((speed - C.impact.collide) / C.impact.perDamage), this);
          b.vel = b.vel.add(a.vel.scale(0.6));
          if (b.isEnemy) b.stun = 0.5;
          a.vel = a.vel.scale(-0.3);
          a.hitCd = b.hitCd = 0.25;
          this.spawnBurst((a.pos.x + b.pos.x) / 2, (a.pos.y + b.pos.y) / 2, '#ffd25f', 12, 220);
          this.addShake(5);
          break;
        }
      }
    }

    playerAttack(p) {
      const r = p.attackRect();
      const axisVec = p.moveAxis === 'x' ? new GS.Vec2(p.facing, 0) : new GS.Vec2(0, p.facing);
      let hit = false;
      for (const e of this.entities) {
        if (!e.alive || !Rect.overlaps(r, e.rect)) continue;
        e.vel = axisVec.scale(C.player.attackKnock).add(e.gravity.neg().scale(C.player.attackLift));
        e.hitCd = 0.12;
        if (e.isEnemy) { e.damage(1, this); e.stun = 0.45; }
        this.spawnBurst(e.pos.x, e.pos.y, '#ffffff', 8, 180);
        hit = true;
      }
      if (hit) this.addShake(3);
    }

    // ── 연출 ──
    addShake(n) { this.shakeAmt = Math.max(this.shakeAmt, n); }

    spawnBurst(x, y, color, n, speed = 180) {
      for (let i = 0; i < n; i++) {
        const a = Math.random() * Math.PI * 2, s = speed * (0.3 + Math.random() * 0.7);
        this.particles.push({ x, y, vx: Math.cos(a) * s, vy: Math.sin(a) * s, life: 0.3 + Math.random() * 0.4, color, size: 2 + Math.random() * 3 });
      }
    }

    floatText(x, y, text, color) { this.texts.push({ x, y, text, color, life: 1.1 }); }

    updateParticles(dt) {
      for (const p of this.particles) { p.life -= dt; p.x += p.vx * dt; p.y += p.vy * dt; p.vx *= 0.92; p.vy *= 0.92; }
      this.particles = this.particles.filter((p) => p.life > 0);
      for (const t of this.texts) { t.life -= dt; t.y -= 30 * dt; }
      this.texts = this.texts.filter((t) => t.life > 0);
    }
  }

  GS.Game = Game;
})(window.GS = window.GS || {});
