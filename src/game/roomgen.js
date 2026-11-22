// 절차적 방 생성. 순수 함수(입력: RNG + 파라미터, 출력: 맵 문자열 배열)라 C#으로 그대로 옮길 수 있다.
//
// 과정: 1) 중력 ↓ 기준으로 지형 배치  2) 플레이어→출구 도달 가능 검사(BFS)
//       3) 적/상자 배치  4) 실패하면 재시도  5) 중력 ↑ 구역이면 상하 반전, 랜덤 좌우 반전
(function (GS) {
  'use strict';
  const COLS = GS.Config.COLS, ROWS = GS.Config.ROWS;
  const FLOOR = ROWS - 2;       // 바닥 위 첫 줄
  const SPAWN_X = 2;
  const DOOR_X = COLS - 4;
  const MAX_ATTEMPTS = 60;

  // 점프 성능 (Config 의 점프 640 / 중력 1800 ≈ 3.5칸 높이, 약 5칸 거리)
  const JUMP_UP = 3, JUMP_DX_UP = 4, JUMP_DX = 5, DROP_DY = 6;

  const isSolid = (g, x, y) => x < 0 || y < 0 || x >= COLS || y >= ROWS || g[y][x] === '#';
  const isBlock = (g, x, y) => isSolid(g, x, y) || g[y][x] === '^';
  const standable = (g, x, y) => !isBlock(g, x, y) && isSolid(g, x, y + 1);
  const isNearSpawnOrDoor = (x) => x <= SPAWN_X + 2 || x >= DOOR_X - 2;

  function makeGrid() {
    const g = [];
    for (let y = 0; y < ROWS; y++) {
      g.push([]);
      for (let x = 0; x < COLS; x++) g[y].push(x === 0 || y === 0 || x === COLS - 1 || y === ROWS - 1 ? '#' : '.');
    }
    return g;
  }

  function rowIsEmpty(g, y, x0, x1) {
    for (let x = x0; x <= x1; x++) if (x < 1 || x > COLS - 2 || g[y][x] !== '.') return false;
    return true;
  }

  // ── 1. 지형 ──
  function layout(rng, p) {
    const g = makeGrid();

    // 바닥 둔덕
    for (let i = rng.int(0, 2); i > 0; i--) {
      const w = rng.int(2, 5), h = rng.int(1, 2), x = rng.int(5, DOOR_X - 3 - w);
      for (let yy = FLOOR - h + 1; yy <= FLOOR; yy++) for (let xx = x; xx < x + w; xx++) g[yy][xx] = '#';
    }

    // 바닥 가시 구덩이 (지면 위에 가시)
    for (let i = rng.int(p.pitsMin, p.pitsMax); i > 0; i--) {
      const w = rng.int(2, 4), x = rng.int(5, DOOR_X - 3 - w);
      for (let xx = x; xx < x + w; xx++) {
        let y = FLOOR;
        while (isSolid(g, xx, y)) y--;
        g[y][xx] = '^';
      }
    }

    // 발판 3단 (세로 간격 3칸 = 점프로 오를 수 있는 높이)
    for (const row of [13, 10, 7]) {
      const want = rng.int(1, 3);
      for (let placed = 0, tries = 0; placed < want && tries < 25; tries++) {
        const w = rng.int(3, 7), x = rng.int(2, COLS - 3 - w);
        if (row === 13 && (isNearSpawnOrDoor(x) || isNearSpawnOrDoor(x + w - 1))) continue;
        // 주변 여백: 다른 발판과 붙거나 위아래로 겹치지 않게
        if (!rowIsEmpty(g, row, x - 2, x + w + 1) || !rowIsEmpty(g, row - 1, x - 1, x + w) || !rowIsEmpty(g, row + 1, x - 1, x + w)) continue;
        for (let xx = x; xx < x + w; xx++) g[row][xx] = '#';
        placed++;
      }
    }

    // 천장 가시 띠 (적을 뒤집어 떨어뜨릴 목표)
    for (let i = rng.int(1, 2); i > 0; i--) {
      const w = rng.int(4, 9), x = rng.int(2, COLS - 3 - w);
      for (let xx = x; xx < x + w; xx++) if (g[1][xx] === '.') g[1][xx] = '^';
    }

    // 벽 가시 (좌우 중력이 오는 구역용)
    if (p.wallSpikes) {
      for (const x of [1, COLS - 2]) {
        if (!rng.chance(0.7)) continue;
        const y0 = rng.int(3, 7), h = rng.int(3, 5);
        for (let y = y0; y < y0 + h; y++) if (g[y][x] === '.') g[y][x] = '^';
      }
    }

    g[FLOOR][DOOR_X] = 'D';
    g[FLOOR - 1][DOOR_X] = 'D';
    return g;
  }

  // ── 2. 도달 가능 검사 ──
  // "서 있을 수 있는 칸"을 노드로 보고 걷기 / 낙하 / 점프로 이동하는 BFS.
  // 실제 물리보다 약간 보수적으로 근사한다 (대시, 중력 조작은 계산에 넣지 않음).
  function reachable(g, sx, sy, gx, gy) {
    const key = (x, y) => y * COLS + x;
    const seen = new Set([key(sx, sy)]);
    const queue = [[sx, sy]];
    const push = (x, y) => {
      const k = key(x, y);
      if (!seen.has(k)) { seen.add(k); queue.push([x, y]); }
    };
    const clearRow = (y, a, b) => {
      for (let x = Math.min(a, b); x <= Math.max(a, b); x++) if (isBlock(g, x, y)) return false;
      return true;
    };
    const clearCol = (x, a, b) => {
      for (let y = Math.min(a, b); y <= Math.max(a, b); y++) if (isBlock(g, x, y)) return false;
      return true;
    };

    while (queue.length) {
      const [x, y] = queue.shift();
      if (x === gx && y === gy) return true;

      // 걷기, 또는 가장자리에서 떨어지기 (낙하 중 좌우 2칸까지 조종)
      for (const dx of [-1, 1]) {
        const nx = x + dx;
        if (isBlock(g, nx, y)) continue;
        if (standable(g, nx, y)) { push(nx, y); continue; }
        for (let d = -2; d <= 2; d++) {
          const fx = nx + d;
          if (!clearRow(y, nx, fx)) continue;
          let fy = y;
          while (fy < ROWS - 2 && !isBlock(g, fx, fy + 1)) fy++;
          if (standable(g, fx, fy)) push(fx, fy);
        }
      }

      // 점프: 출발 칸 위로 솟아 최고점 줄을 가로질러 목표 칸으로 내려간다
      for (let dy = -JUMP_UP; dy <= DROP_DY; dy++) {
        for (let dx = -JUMP_DX; dx <= JUMP_DX; dx++) {
          if (!dx && !dy) continue;
          if (dy < 0 && Math.abs(dx) > JUMP_DX_UP) continue;
          const tx = x + dx, ty = y + dy;
          if (tx < 1 || tx > COLS - 2 || ty < 1 || ty > ROWS - 2 || !standable(g, tx, ty)) continue;
          const peak = Math.min(y, ty) - 1;
          if (peak < 1) continue;
          if (clearCol(x, peak, y) && clearRow(peak, x, tx) && clearCol(tx, peak, ty)) push(tx, ty);
        }
      }
    }
    return false;
  }

  // ── 3. 적 / 상자 배치 ──
  function populate(rng, g, p) {
    const stands = [];
    for (let y = 2; y <= FLOOR; y++) {
      for (let x = 1; x <= COLS - 2; x++) {
        if (g[y][x] !== '.' || !standable(g, x, y)) continue;
        if (Math.abs(x - SPAWN_X) < 8) continue;                    // 시작 지점 근처 금지
        if (Math.abs(x - DOOR_X) <= 1 && y >= FLOOR - 1) continue;  // 출구 앞 금지
        stands.push([x, y]);
      }
    }
    rng.shuffle(stands);

    const used = [];
    const take = (filter, minDist) => {
      const c = stands.find(([x, y]) => filter(x, y) && used.every(([ux, uy]) => Math.abs(ux - x) + Math.abs(uy - y) >= minDist));
      if (c) used.push(c);
      return c;
    };

    const out = [];
    for (let i = 0; i < p.anchors; i++) {
      const c = take((x, y) => y >= FLOOR - 2, 3); // 앵커는 근접 공격이 닿는 바닥 근처에만
      if (!c) return null;
      out.push([c[0], c[1], 'A']);
    }
    for (let i = 0; i < p.enemies; i++) {
      const c = take(() => true, 3);
      if (!c) return null;
      out.push([c[0], c[1], 'E']);
    }
    for (let i = 0; i < p.boxes; i++) {
      const c = take(() => true, 2);
      if (c) out.push([c[0], c[1], 'B']);
    }
    return out;
  }

  function generate(rng, p) {
    for (let attempt = 0; attempt < MAX_ATTEMPTS; attempt++) {
      const g = layout(rng, p);
      if (!reachable(g, SPAWN_X, FLOOR, DOOR_X, FLOOR)) continue;
      const spawns = populate(rng, g, p);
      if (!spawns) continue;
      g[FLOOR][SPAWN_X] = 'P';
      for (const [x, y, ch] of spawns) g[y][x] = ch;

      let rows = g.map((r) => r.join(''));
      if (p.flipH) rows = rows.map((r) => r.split('').reverse().join(''));
      if (p.flipV) rows = rows.slice().reverse();
      return { map: rows, attempts: attempt + 1 };
    }
    return null;
  }

  // 난이도 d (0부터) → 생성 파라미터
  function paramsFor(rng, d, zone) {
    return {
      enemies: 2 + Math.floor(d * 0.5), // 2,2,3,3,4,4 (+앵커)
      anchors: (d >= 2 ? 1 : 0) + (d >= 5 ? 1 : 0),
      boxes: rng.int(1, 2),
      pitsMin: d >= 1 ? 1 : 0,
      pitsMax: d >= 2 ? 2 : 1,
      wallSpikes: !!zone.rotateEvery,
      flipH: rng.chance(0.5),
      flipV: zone.gravity === 'UP',
    };
  }

  // 런 전체 구성: 구역마다 소개 방 1 + 생성 방 N
  function buildRun(seed) {
    const rng = new GS.RNG(seed);
    const rooms = [];
    GS.Zones.forEach((zone, zi) => {
      const total = 1 + zone.generatedRooms;
      const base = { theme: zone.theme, gravity: zone.gravity, zoneIndex: zi };
      rooms.push(Object.assign({}, base, {
        name: `${zone.label} 1/${total}`,
        subtitle: zone.intro.subtitle,
        map: zone.intro.map,
        rotateEvery: zone.rotateEvery || 0,
      }));
      const subtitles = rng.shuffle(zone.subtitles.slice());
      for (let i = 0; i < zone.generatedRooms; i++) {
        const result = generate(rng, paramsFor(rng, zi * 2 + i, zone));
        rooms.push(Object.assign({}, base, {
          name: `${zone.label} ${i + 2}/${total}`,
          subtitle: subtitles[i % subtitles.length],
          map: result ? result.map : zone.intro.map, // 생성 실패 시 소개 방으로 대체
          rotateEvery: zone.rotateEvery ? Math.max(4, zone.rotateEvery - (i + 1)) : 0,
          generated: !!result,
        }));
      }
    });
    return rooms;
  }

  GS.RoomGen = { generate, reachable, paramsFor, buildRun };
})(window.GS = window.GS || {});
