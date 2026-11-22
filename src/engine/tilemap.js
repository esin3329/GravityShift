// 타일맵. 문자: # 벽, ^ 가시, D 출구, P 플레이어, E 워커, A 앵커, B 상자
// Unity 이식 시 Tilemap + TilemapCollider2D, 스폰은 프리팹 배치로 대체.
(function (GS) {
  'use strict';
  const C = GS.Config;

  class Tilemap {
    constructor(rows) {
      this.cols = C.COLS;
      this.rows = C.ROWS;
      this.tiles = new Array(this.cols * this.rows);
      this.spawns = { player: null, enemies: [], anchors: [], boxes: [] };

      for (let y = 0; y < this.rows; y++) {
        const line = (rows[y] || '').padEnd(this.cols, '.');
        for (let x = 0; x < this.cols; x++) {
          let ch = line[x];
          const border = x === 0 || y === 0 || x === this.cols - 1 || y === this.rows - 1;
          if (border) ch = '#';
          const center = new GS.Vec2((x + 0.5) * C.TILE, (y + 0.5) * C.TILE);
          if (ch === 'P') { this.spawns.player = center; ch = '.'; }
          else if (ch === 'E') { this.spawns.enemies.push(center); ch = '.'; }
          else if (ch === 'A') { this.spawns.anchors.push(center); ch = '.'; }
          else if (ch === 'B') { this.spawns.boxes.push(center); ch = '.'; }
          this.tiles[y * this.cols + x] = ch;
        }
      }
      if (!this.spawns.player) this.spawns.player = new GS.Vec2(C.TILE * 2, C.TILE * 2);
    }

    get(cx, cy) {
      if (cx < 0 || cy < 0 || cx >= this.cols || cy >= this.rows) return '#';
      return this.tiles[cy * this.cols + cx];
    }
    isSolid(cx, cy) { return this.get(cx, cy) === '#'; }
    tileAtPoint(p) { return this.get(Math.floor(p.x / C.TILE), Math.floor(p.y / C.TILE)); }

    rectHasTile(r, ch, inset = 0) {
      const T = C.TILE;
      const x0 = Math.floor((r.x + inset) / T), x1 = Math.floor((r.x + r.w - inset - 0.01) / T);
      const y0 = Math.floor((r.y + inset) / T), y1 = Math.floor((r.y + r.h - inset - 0.01) / T);
      for (let cy = y0; cy <= y1; cy++)
        for (let cx = x0; cx <= x1; cx++) if (this.get(cx, cy) === ch) return true;
      return false;
    }
  }

  GS.Tilemap = Tilemap;
})(window.GS = window.GS || {});
