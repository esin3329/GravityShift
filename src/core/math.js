// 수학 유틸. Unity 이식 시 Vector2 / Mathf / Rect 로 대체된다.
(function (GS) {
  'use strict';

  class Vec2 {
    constructor(x = 0, y = 0) { this.x = x; this.y = y; }
    clone() { return new Vec2(this.x, this.y); }
    add(v) { return new Vec2(this.x + v.x, this.y + v.y); }
    sub(v) { return new Vec2(this.x - v.x, this.y - v.y); }
    scale(s) { return new Vec2(this.x * s, this.y * s); }
    neg() { return new Vec2(-this.x, -this.y); }
    dot(v) { return this.x * v.x + this.y * v.y; }
    get magnitude() { return Math.hypot(this.x, this.y); }
    get normalized() {
      const m = this.magnitude;
      return m > 0 ? this.scale(1 / m) : new Vec2();
    }
  }

  // 중력 방향 상수. 엔티티의 gravity 는 항상 이 객체들 중 하나를 가리킨다.
  const Dir = {
    DOWN: Object.freeze(new Vec2(0, 1)),
    LEFT: Object.freeze(new Vec2(-1, 0)),
    UP: Object.freeze(new Vec2(0, -1)),
    RIGHT: Object.freeze(new Vec2(1, 0)),
  };

  GS.Vec2 = Vec2;
  GS.Dir = Dir;
  GS.DirOrder = ['DOWN', 'LEFT', 'UP', 'RIGHT'];
  GS.opposite = (d) =>
    d === Dir.DOWN ? Dir.UP : d === Dir.UP ? Dir.DOWN : d === Dir.LEFT ? Dir.RIGHT : Dir.LEFT;
  // 중력 방향을 "발밑"으로 보는 회전각 (DOWN = 0)
  GS.gravAngle = (g) => Math.atan2(-g.x, g.y);

  GS.Mathf = {
    clamp: (v, a, b) => Math.max(a, Math.min(b, v)),
    approach(cur, target, delta) {
      if (cur < target) return Math.min(cur + delta, target);
      return Math.max(cur - delta, target);
    },
    lerpAngle(a, b, t) {
      const d = ((b - a + Math.PI * 3) % (Math.PI * 2)) - Math.PI;
      return a + d * t;
    },
  };

  // 사각형은 {x, y, w, h} (좌상단 기준)
  GS.Rect = {
    overlaps: (a, b) => a.x < b.x + b.w && a.x + a.w > b.x && a.y < b.y + b.h && a.y + a.h > b.y,
    contains: (r, p, pad = 0) =>
      p.x >= r.x - pad && p.x <= r.x + r.w + pad && p.y >= r.y - pad && p.y <= r.y + r.h + pad,
  };
})(window.GS = window.GS || {});
