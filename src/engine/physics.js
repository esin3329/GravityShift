// 축 분리 AABB 타일 충돌. Unity 이식 시 Rigidbody2D + BoxCollider2D 로 대체하고,
// 중력은 rb.gravityScale = 0 으로 끈 뒤 엔티티가 직접 AddForce 하는 방식을 유지한다.
(function (GS) {
  'use strict';

  GS.Physics = {
    move(e, game, dt) {
      e.grounded = false;
      this.moveAxis(e, game, 'x', e.vel.x * dt);
      this.moveAxis(e, game, 'y', e.vel.y * dt);
    },

    moveAxis(e, game, axis, amt) {
      if (amt === 0) return;
      const T = GS.Config.TILE, map = game.map;
      e.pos[axis] += amt;

      const l = e.pos.x - e.w / 2, r = e.pos.x + e.w / 2;
      const t = e.pos.y - e.h / 2, b = e.pos.y + e.h / 2;
      const x0 = Math.floor(l / T), x1 = Math.floor((r - 0.01) / T);
      const y0 = Math.floor(t / T), y1 = Math.floor((b - 0.01) / T);

      let hit = false, target = e.pos[axis];
      for (let cy = y0; cy <= y1; cy++) {
        for (let cx = x0; cx <= x1; cx++) {
          if (!map.isSolid(cx, cy)) continue;
          hit = true;
          if (axis === 'x') target = amt > 0 ? Math.min(target, cx * T - e.w / 2) : Math.max(target, (cx + 1) * T + e.w / 2);
          else target = amt > 0 ? Math.min(target, cy * T - e.h / 2) : Math.max(target, (cy + 1) * T + e.h / 2);
        }
      }
      if (!hit) return;

      e.pos[axis] = target;
      const speed = Math.abs(e.vel[axis]);
      if (e.gravity[axis] !== 0 && Math.sign(amt) === Math.sign(e.gravity[axis])) e.grounded = true;
      e.vel[axis] = e.bounce > 0 && speed > 200 ? -e.vel[axis] * e.bounce : 0;
      e.onImpact(speed, game);
    },
  };
})(window.GS = window.GS || {});
