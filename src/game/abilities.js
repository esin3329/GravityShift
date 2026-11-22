// 플레이 스타일(능력). Unity 이식 시 공통 인터페이스 IGravityAbility 를 구현하는 컴포넌트.
(function (GS) {
  'use strict';
  const C = GS.Config;

  GS.StyleColors = { psychic: '#c78bff', engineer: '#6fffb0' };

  // 🟪 사이킥: 대상 하나의 중력을 직접 조작
  class Psychic {
    constructor() { this.name = '사이킥'; this.reset(); }

    reset() {
      if (this.grabbed) this.grabbed.held = false;
      this.grabbed = null;
      this.mana = C.psychic.mana;
    }

    update(game, dt, input) {
      const P = C.psychic, m = input.mouse, player = game.player;
      const color = GS.StyleColors.psychic;
      if (!this.grabbed) this.mana = Math.min(P.mana, this.mana + P.regen * dt);

      // 좌클릭: 대상 중력 반전
      if (input.mousePressed(0)) {
        const t = game.pickEntity(m);
        if (t && !t.manipulable) game.floatText(t.pos.x, t.pos.y - 22, '무효', '#9a9aa8');
        else if (t && this.mana < P.flipCost) game.floatText(player.pos.x, player.pos.y - 24, '정신력 부족', color);
        else if (t) {
          t.gravOverride = GS.opposite(t.gravity);
          t.overrideTimer = P.flipTime;
          this.mana -= P.flipCost;
          if (t.isEnemy) t.stun = Math.max(t.stun, 0.3);
          game.spawnBurst(t.pos.x, t.pos.y, color, 12, 160);
        }
      }

      // 우클릭 홀드: 잡기 → 놓으면 던지기
      if (input.mousePressed(2) && !this.grabbed) {
        const t = game.pickEntity(m);
        if (t && t.manipulable && this.mana > 5) { this.grabbed = t; t.held = true; }
        else if (t) game.floatText(t.pos.x, t.pos.y - 22, '무효', '#9a9aa8');
      }
      if (this.grabbed) {
        const g = this.grabbed;
        if (!g.alive || !input.mouseDown(2) || this.mana <= 0) {
          g.held = false;
          this.grabbed = null;
        } else {
          let v = m.sub(g.pos).scale(P.grabPull);
          if (v.magnitude > P.grabMaxSpeed) v = v.normalized.scale(P.grabMaxSpeed);
          g.vel = v;
          this.mana = Math.max(0, this.mana - P.grabDrain * dt);
          if (g.isEnemy) g.stun = Math.max(g.stun, 0.35);
        }
      }

      // F: 자신의 중력 반전 (토글)
      if (input.pressed('KeyF')) {
        if (player.gravOverride) player.gravOverride = null;
        else if (this.mana >= P.selfFlipCost) {
          player.gravOverride = GS.opposite(player.gravity);
          player.overrideTimer = Infinity;
          this.mana -= P.selfFlipCost;
          game.spawnBurst(player.pos.x, player.pos.y, color, 12, 160);
        } else game.floatText(player.pos.x, player.pos.y - 24, '정신력 부족', color);
      }
    }

    zoneAt() { return null; }
  }

  // 🟩 엔지니어: 구역 단위로 중력을 바꾸는 발판 설치
  class Engineer {
    constructor() { this.name = '엔지니어'; this.reset(); }

    reset() { this.zones = []; this.dirIndex = 0; this.cd = 0; }
    get dir() { return GS.Dir[GS.DirOrder[this.dirIndex]]; }

    // 발판은 중력 방향 축을 따라 벽에 닿을 때까지 뻗는 빔(폭 beamWidth 칸)을 만든다
    zoneRectAt(p, dir, map) {
      const T = C.TILE, bw = C.engineer.beamWidth, half = Math.floor(bw / 2);
      const cx = Math.floor(p.x / T), cy = Math.floor(p.y / T);
      if (map.isSolid(cx, cy)) return null;
      if (dir.y !== 0) {
        let top = cy, bottom = cy;
        while (!map.isSolid(cx, top - 1)) top--;
        while (!map.isSolid(cx, bottom + 1)) bottom++;
        return { x: (cx - half) * T, y: top * T, w: bw * T, h: (bottom - top + 1) * T };
      }
      let left = cx, right = cx;
      while (!map.isSolid(left - 1, cy)) left--;
      while (!map.isSolid(right + 1, cy)) right++;
      return { x: left * T, y: (cy - half) * T, w: (right - left + 1) * T, h: bw * T };
    }

    update(game, dt, input) {
      const E = C.engineer, m = input.mouse;
      this.cd = Math.max(0, this.cd - dt);

      if (input.wheel !== 0) this.dirIndex = (this.dirIndex + (input.wheel > 0 ? 1 : -1) + 4) % 4;
      if (input.pressed('KeyQ')) this.dirIndex = (this.dirIndex + 1) % 4;

      const rect = this.zoneRectAt(m, this.dir, game.map);
      if (input.mousePressed(0) && this.cd <= 0 && rect) {
        const z = Object.assign(rect, { dir: this.dir, born: game.time });
        this.zones.push(z);
        if (this.zones.length > E.maxZones) this.zones.shift();
        this.cd = E.cooldown;
        game.spawnBurst(z.x + z.w / 2, z.y + z.h / 2, GS.StyleColors.engineer, 14, 200);
      }
      if (input.mousePressed(2)) {
        for (let i = this.zones.length - 1; i >= 0; i--) {
          if (GS.Rect.contains(this.zones[i], m)) { this.zones.splice(i, 1); break; }
        }
      }
      if (input.pressed('KeyF') && this.zones.length) {
        this.zones.forEach((z) => { z.dir = GS.opposite(z.dir); });
        game.addShake(4);
      }
    }

    // 나중에 설치한 발판이 우선
    zoneAt(p) {
      for (let i = this.zones.length - 1; i >= 0; i--) if (GS.Rect.contains(this.zones[i], p)) return this.zones[i].dir;
      return null;
    }
  }

  GS.Psychic = Psychic;
  GS.Engineer = Engineer;
})(window.GS = window.GS || {});
