// 렌더링 전용. 게임 로직은 여기서 상태를 바꾸지 않는다.
// Unity 이식 시 SpriteRenderer / UI(Canvas) / ParticleSystem 으로 대체되므로 이 파일은 옮기지 않는다.
(function (GS) {
  'use strict';
  const C = GS.Config, T = C.TILE;
  const FONT = '"Pretendard","Malgun Gothic","Apple SD Gothic Neo",sans-serif';

  function arrow(ctx, x, y, dir, size, color) {
    ctx.save();
    ctx.translate(x, y);
    ctx.rotate(Math.atan2(dir.y, dir.x));
    ctx.fillStyle = color;
    ctx.beginPath();
    ctx.moveTo(size, 0);
    ctx.lineTo(-size * 0.6, size * 0.75);
    ctx.lineTo(-size * 0.6, -size * 0.75);
    ctx.closePath();
    ctx.fill();
    ctx.restore();
  }

  function roundRect(ctx, x, y, w, h, r) {
    ctx.beginPath();
    ctx.moveTo(x + r, y);
    ctx.arcTo(x + w, y, x + w, y + h, r);
    ctx.arcTo(x + w, y + h, x, y + h, r);
    ctx.arcTo(x, y + h, x, y, r);
    ctx.arcTo(x, y, x + w, y, r);
    ctx.closePath();
  }

  class Renderer {
    constructor(canvas) {
      this.ctx = canvas.getContext('2d');
      this.W = canvas.width;
      this.H = canvas.height;
      this.stars = Array.from({ length: 90 }, () => ({
        x: Math.random() * this.W, y: Math.random() * this.H, r: Math.random() * 1.4 + 0.3, a: Math.random() * 0.6 + 0.2,
      }));
    }

    draw(game) {
      const ctx = this.ctx, th = game.room.theme;
      ctx.save();
      if (game.shakeAmt > 0) ctx.translate((Math.random() - 0.5) * game.shakeAmt, (Math.random() - 0.5) * game.shakeAmt);

      ctx.fillStyle = th.bg;
      ctx.fillRect(-20, -20, this.W + 40, this.H + 40);
      if (th.stars) {
        for (const s of this.stars) {
          ctx.globalAlpha = s.a * (0.7 + 0.3 * Math.sin(game.time * 2 + s.x));
          ctx.fillStyle = '#ffffff';
          ctx.fillRect(s.x, s.y, s.r, s.r);
        }
        ctx.globalAlpha = 1;
      }
      ctx.strokeStyle = th.grid;
      ctx.lineWidth = 1;
      ctx.beginPath();
      for (let x = 0; x <= this.W; x += T) { ctx.moveTo(x + 0.5, 0); ctx.lineTo(x + 0.5, this.H); }
      for (let y = 0; y <= this.H; y += T) { ctx.moveTo(0, y + 0.5); ctx.lineTo(this.W, y + 0.5); }
      ctx.stroke();

      this.drawZones(game);
      this.drawTiles(game);
      for (const e of game.entities) if (e.kind === 'box') this.drawBox(e, game);
      for (const e of game.entities) if (e.isEnemy) this.drawEnemy(e, game);
      if (game.player.alive) this.drawPlayer(game.player, game);
      this.drawAbilityOverlay(game);
      this.drawParticles(game);
      ctx.restore();

      this.drawHUD(game);
      this.drawBanner(game);
    }

    // ── 맵 ──
    drawTiles(game) {
      const ctx = this.ctx, map = game.map, th = game.room.theme;
      for (let y = 0; y < map.rows; y++) {
        for (let x = 0; x < map.cols; x++) {
          const ch = map.get(x, y);
          if (ch === '#') {
            ctx.fillStyle = th.wall;
            ctx.fillRect(x * T, y * T, T, T);
            ctx.fillStyle = th.wallTop;
            if (!map.isSolid(x, y - 1)) ctx.fillRect(x * T, y * T, T, 3);
            if (!map.isSolid(x, y + 1)) ctx.fillRect(x * T, (y + 1) * T - 3, T, 3);
            if (!map.isSolid(x - 1, y)) ctx.fillRect(x * T, y * T, 3, T);
            if (!map.isSolid(x + 1, y)) ctx.fillRect((x + 1) * T - 3, y * T, 3, T);
          } else if (ch === '^') this.drawSpike(map, x, y);
          else if (ch === 'D') this.drawDoor(game, x, y);
        }
      }
    }

    drawSpike(map, x, y) {
      const ctx = this.ctx;
      // 붙어 있는 벽의 반대 방향으로 가시가 솟는다
      let angle = 0;
      if (map.isSolid(x, y + 1)) angle = 0;
      else if (map.isSolid(x, y - 1)) angle = Math.PI;
      else if (map.isSolid(x - 1, y)) angle = Math.PI / 2;
      else if (map.isSolid(x + 1, y)) angle = -Math.PI / 2;
      ctx.save();
      ctx.translate(x * T + T / 2, y * T + T / 2);
      ctx.rotate(angle);
      ctx.fillStyle = '#ff4d6d';
      for (let i = 0; i < 3; i++) {
        const bx = -T / 2 + (i * T) / 3;
        ctx.beginPath();
        ctx.moveTo(bx + 1, T / 2);
        ctx.lineTo(bx + T / 6, -T / 2 + 10);
        ctx.lineTo(bx + T / 3 - 1, T / 2);
        ctx.fill();
      }
      ctx.fillStyle = '#a3243d';
      ctx.fillRect(-T / 2, T / 2 - 4, T, 4);
      ctx.restore();
    }

    drawDoor(game, x, y) {
      const ctx = this.ctx, th = game.room.theme;
      if (game.doorOpen) {
        ctx.globalAlpha = 0.45 + 0.25 * Math.sin(game.time * 5);
        ctx.fillStyle = th.accent;
        ctx.fillRect(x * T + 2, y * T + 2, T - 4, T - 4);
        ctx.globalAlpha = 1;
        ctx.strokeStyle = th.accent;
      } else {
        ctx.fillStyle = '#1b1f27';
        ctx.fillRect(x * T + 2, y * T + 2, T - 4, T - 4);
        ctx.strokeStyle = '#4a5160';
        ctx.beginPath();
        for (let i = 1; i < 4; i++) { ctx.moveTo(x * T + (i * T) / 4, y * T + 4); ctx.lineTo(x * T + (i * T) / 4, (y + 1) * T - 4); }
        ctx.stroke();
      }
      ctx.lineWidth = 2;
      ctx.strokeRect(x * T + 2, y * T + 2, T - 4, T - 4);
      ctx.lineWidth = 1;
    }

    drawZones(game) {
      if (game.style !== 'engineer') return;
      const ctx = this.ctx, color = GS.StyleColors.engineer;
      for (const z of game.abilities.engineer.zones) {
        ctx.fillStyle = 'rgba(111,255,176,0.10)';
        ctx.fillRect(z.x, z.y, z.w, z.h);
        ctx.strokeStyle = color;
        ctx.lineWidth = 2;
        ctx.strokeRect(z.x + 1, z.y + 1, z.w - 2, z.h - 2);
        ctx.lineWidth = 1;
        // 중력 방향으로 흐르는 화살표
        ctx.save();
        ctx.beginPath();
        ctx.rect(z.x, z.y, z.w, z.h);
        ctx.clip();
        ctx.globalAlpha = 0.35;
        const off = (game.time * 50) % T;
        for (let i = -1; i <= z.w / T; i++) {
          for (let j = -1; j <= z.h / T; j++) {
            const ax = z.x + i * T + T / 2 + z.dir.x * off;
            const ay = z.y + j * T + T / 2 + z.dir.y * off;
            arrow(ctx, ax, ay, z.dir, 6, color);
          }
        }
        ctx.restore();
      }
    }

    // ── 엔티티 ──
    drawOverride(e, game) {
      if (!e.gravOverride) return;
      const ctx = this.ctx, color = GS.StyleColors.psychic;
      const tip = e.pos.add(e.gravity.scale(e.h / 2 + 10));
      arrow(ctx, tip.x, tip.y, e.gravity, 6, color);
      if (e.overrideTimer !== Infinity) {
        ctx.strokeStyle = color;
        ctx.lineWidth = 2;
        ctx.beginPath();
        ctx.arc(e.pos.x, e.pos.y, e.w * 0.8, -Math.PI / 2, -Math.PI / 2 + (Math.PI * 2 * e.overrideTimer) / C.psychic.flipTime);
        ctx.stroke();
        ctx.lineWidth = 1;
      }
    }

    drawHeld(e) {
      if (!e.held) return;
      const ctx = this.ctx;
      ctx.shadowColor = GS.StyleColors.psychic;
      ctx.shadowBlur = 16;
    }

    drawBox(e, game) {
      const ctx = this.ctx;
      ctx.save();
      this.drawHeld(e);
      const r = e.rect;
      ctx.fillStyle = '#9c6a35';
      ctx.fillRect(r.x, r.y, r.w, r.h);
      ctx.shadowBlur = 0;
      ctx.strokeStyle = '#5e3d1a';
      ctx.lineWidth = 2;
      ctx.strokeRect(r.x + 1, r.y + 1, r.w - 2, r.h - 2);
      ctx.beginPath();
      ctx.moveTo(r.x + 3, r.y + 3); ctx.lineTo(r.x + r.w - 3, r.y + r.h - 3);
      ctx.moveTo(r.x + r.w - 3, r.y + 3); ctx.lineTo(r.x + 3, r.y + r.h - 3);
      ctx.stroke();
      ctx.restore();
      this.drawOverride(e, game);
    }

    drawEnemy(e, game) {
      const ctx = this.ctx, a = e.visualAngle;
      ctx.save();
      ctx.translate(e.pos.x, e.pos.y);
      ctx.rotate(a);
      this.drawHeld(e);
      const body = e.flash > 0 ? '#ffffff' : e.anchor ? '#6d5ce8' : '#ff6b5c';
      ctx.fillStyle = body;
      roundRect(ctx, -12, -12, 24, 22, 9);
      ctx.fill();
      ctx.shadowBlur = 0;
      // 발
      ctx.fillStyle = e.anchor ? '#3b2f99' : '#b8402f';
      ctx.fillRect(-9, 9, 6, 3);
      ctx.fillRect(3, 9, 6, 3);
      if (e.anchor) {
        // 앵커 표식: 고정 고리
        ctx.strokeStyle = '#c9c2ff';
        ctx.lineWidth = 2;
        ctx.beginPath();
        ctx.arc(0, 3, 4, 0, Math.PI * 2);
        ctx.moveTo(0, -1); ctx.lineTo(0, -6);
        ctx.stroke();
        ctx.lineWidth = 1;
      }
      // 눈: 플레이어를 바라본다
      const d = game.player.pos.sub(e.pos).normalized;
      const lx = d.x * Math.cos(a) + d.y * Math.sin(a);
      const ly = -d.x * Math.sin(a) + d.y * Math.cos(a);
      for (const ex of [-5, 5]) {
        ctx.fillStyle = '#ffffff';
        ctx.beginPath(); ctx.arc(ex, -4, 3.5, 0, Math.PI * 2); ctx.fill();
        ctx.fillStyle = '#1a1a22';
        ctx.beginPath(); ctx.arc(ex + lx * 1.6, -4 + ly * 1.6, 1.8, 0, Math.PI * 2); ctx.fill();
      }
      if (e.stun > 0 && !e.held) {
        ctx.fillStyle = '#ffd25f';
        ctx.font = `bold 10px ${FONT}`;
        ctx.textAlign = 'center';
        ctx.fillText('★', Math.sin(game.time * 12) * 6, -16);
      }
      ctx.restore();

      // 체력
      for (let i = 0; i < e.maxHp; i++) {
        ctx.fillStyle = i < e.hp ? '#ff9a8a' : 'rgba(255,255,255,0.15)';
        ctx.fillRect(e.pos.x - e.maxHp * 3 + i * 6, e.pos.y - e.h / 2 - 9, 4, 3);
      }
      this.drawOverride(e, game);
    }

    drawPlayer(p, game) {
      const ctx = this.ctx, a = p.visualAngle;
      if (p.blink > 0 && Math.floor(game.time * 20) % 2 === 0) return;
      const styleColor = GS.StyleColors[game.style];

      // 공격 이펙트
      if (p.attackTimer > 0) {
        const r = p.attackRect();
        ctx.fillStyle = 'rgba(255,255,255,0.35)';
        roundRect(ctx, r.x, r.y, r.w, r.h, 6);
        ctx.fill();
      }

      // 로컬 좌표계에서 바라보는 방향
      const axisVec = p.moveAxis === 'x' ? { x: 1, y: 0 } : { x: 0, y: 1 };
      const lf = Math.sign(Math.cos(a) * axisVec.x + Math.sin(a) * axisVec.y) * p.facing || p.facing;

      ctx.save();
      ctx.translate(p.pos.x, p.pos.y);
      ctx.rotate(a);
      if (p.dashTimer > 0) { ctx.shadowColor = '#9fe8ff'; ctx.shadowBlur = 14; }
      ctx.fillStyle = '#eaf6ff';
      roundRect(ctx, -11, -13, 22, 24, 6);
      ctx.fill();
      ctx.shadowBlur = 0;
      ctx.fillStyle = styleColor;
      ctx.fillRect(lf > 0 ? 0 : -10, -7, 10, 5); // 바이저
      ctx.fillStyle = '#5a6a80';
      ctx.fillRect(-8, 10, 6, 3);
      ctx.fillRect(2, 10, 6, 3);
      ctx.restore();
      this.drawOverride(p, game);
    }

    drawAbilityOverlay(game) {
      const ctx = this.ctx, m = game.input.mouse, p = game.player;
      if (game.style === 'psychic') {
        const color = GS.StyleColors.psychic, psy = game.abilities.psychic;
        const hover = game.pickEntity(m);
        if (hover && hover !== psy.grabbed) {
          const r = hover.rect;
          ctx.setLineDash([4, 3]);
          ctx.strokeStyle = hover.manipulable ? color : '#777';
          ctx.strokeRect(r.x - 4, r.y - 4, r.w + 8, r.h + 8);
          ctx.setLineDash([]);
        }
        if (psy.grabbed && p.alive) {
          const g = psy.grabbed;
          ctx.strokeStyle = color;
          ctx.globalAlpha = 0.6;
          ctx.lineWidth = 2;
          ctx.beginPath();
          ctx.moveTo(p.pos.x, p.pos.y);
          const mx = (p.pos.x + g.pos.x) / 2 + Math.sin(game.time * 20) * 6;
          const my = (p.pos.y + g.pos.y) / 2 + Math.cos(game.time * 17) * 6;
          ctx.quadraticCurveTo(mx, my, g.pos.x, g.pos.y);
          ctx.stroke();
          ctx.globalAlpha = 1;
          ctx.lineWidth = 1;
        }
        ctx.strokeStyle = color;
        ctx.beginPath();
        ctx.arc(m.x, m.y, 7, 0, Math.PI * 2);
        ctx.moveTo(m.x - 11, m.y); ctx.lineTo(m.x - 4, m.y);
        ctx.moveTo(m.x + 4, m.y); ctx.lineTo(m.x + 11, m.y);
        ctx.stroke();
      } else {
        const eng = game.abilities.engineer, color = GS.StyleColors.engineer;
        const z = eng.zoneRectAt(m, eng.dir, game.map);
        ctx.globalAlpha = 0.55;
        if (z) {
          ctx.setLineDash([6, 4]);
          ctx.strokeStyle = color;
          ctx.strokeRect(z.x + 1, z.y + 1, z.w - 2, z.h - 2);
          ctx.setLineDash([]);
        }
        arrow(ctx, m.x, m.y, eng.dir, 12, z ? color : '#777');
        ctx.globalAlpha = 1;
      }
    }

    drawParticles(game) {
      const ctx = this.ctx;
      for (const p of game.particles) {
        ctx.globalAlpha = Math.min(1, p.life * 2.5);
        ctx.fillStyle = p.color;
        ctx.fillRect(p.x - p.size / 2, p.y - p.size / 2, p.size, p.size);
      }
      ctx.globalAlpha = 1;
      ctx.textAlign = 'center';
      ctx.font = `bold 13px ${FONT}`;
      for (const t of game.texts) {
        ctx.globalAlpha = Math.min(1, t.life * 2);
        ctx.fillStyle = t.color;
        ctx.fillText(t.text, t.x, t.y);
      }
      ctx.globalAlpha = 1;
    }

    // ── UI ──
    drawHUD(game) {
      const ctx = this.ctx, W = this.W, H = this.H, p = game.player, th = game.room.theme;
      ctx.fillStyle = 'rgba(0,0,0,0.45)';
      ctx.fillRect(0, 0, W, 32);
      ctx.fillRect(0, H - 32, W, 32);
      ctx.textBaseline = 'middle';

      // 체력
      for (let i = 0; i < p.maxHp; i++) {
        ctx.fillStyle = i < p.hp ? '#ff5c7a' : '#3a2a33';
        roundRect(ctx, 12 + i * 18, 9, 14, 14, 3);
        ctx.fill();
      }

      // 스타일 + 리소스
      const sx = 12 + p.maxHp * 18 + 10;
      const color = GS.StyleColors[game.style];
      ctx.font = `bold 14px ${FONT}`;
      ctx.textAlign = 'left';
      ctx.fillStyle = color;
      ctx.fillText(game.ability.name, sx, 16);
      const bx = sx + 64;
      if (game.style === 'psychic') {
        const ratio = game.abilities.psychic.mana / C.psychic.mana;
        ctx.fillStyle = 'rgba(255,255,255,0.12)';
        ctx.fillRect(bx, 12, 120, 8);
        ctx.fillStyle = color;
        ctx.fillRect(bx, 12, 120 * ratio, 8);
      } else {
        const eng = game.abilities.engineer;
        for (let i = 0; i < C.engineer.maxZones; i++) {
          ctx.fillStyle = i < C.engineer.maxZones - eng.zones.length ? color : 'rgba(255,255,255,0.15)';
          ctx.fillRect(bx + i * 16, 9, 12, 14);
        }
        ctx.fillStyle = '#cfd6e0';
        ctx.font = `12px ${FONT}`;
        ctx.fillText('방향', bx + 56, 16);
        arrow(ctx, bx + 92, 16, eng.dir, 7, color);
      }

      // 방 이름 + 중력
      ctx.textAlign = 'center';
      ctx.font = `bold 15px ${FONT}`;
      ctx.fillStyle = '#e8eef6';
      ctx.fillText(game.room.name, W / 2 - 14, 16);
      const nameW = ctx.measureText(game.room.name).width;
      const warn = game.room.rotateEvery && game.rotateTimer < 1.5;
      arrow(ctx, W / 2 - 14 + nameW / 2 + 18, 16, game.roomGravity, 8, warn && Math.floor(game.time * 8) % 2 ? '#ff4d6d' : th.accent);
      if (game.room.rotateEvery) {
        ctx.font = `12px ${FONT}`;
        ctx.textAlign = 'left';
        ctx.fillStyle = warn ? '#ff4d6d' : '#9aa6b8';
        ctx.fillText(`회전 ${game.rotateTimer.toFixed(1)}s`, W / 2 - 14 + nameW / 2 + 32, 16);
      }

      // 진행 상태
      ctx.textAlign = 'right';
      ctx.font = `bold 13px ${FONT}`;
      const left = game.entities.filter((e) => e.isEnemy).length;
      ctx.fillStyle = game.doorOpen ? th.accent : '#e8eef6';
      const status = game.doorOpen ? '출구 개방 ▶' : `남은 적 ${left}`;
      ctx.fillText(status, W - 12, 16);
      ctx.font = `11px ${FONT}`;
      ctx.fillStyle = '#7d8798';
      ctx.fillText(`시드 ${game.seed}`, W - 24 - ctx.measureText(status).width * 1.2, 16);

      // 조작 안내
      ctx.textAlign = 'center';
      ctx.font = `11px ${FONT}`;
      ctx.fillStyle = 'rgba(220,228,240,0.75)';
      ctx.fillText('A/D 이동 (좌우 중력일 땐 W/S) · Space 점프 · Shift 대시 · J 공격 · Tab 스타일 전환 · R 방 재시작 · N 새 런', W / 2, H - 23);
      ctx.fillStyle = color;
      ctx.fillText(
        game.style === 'psychic'
          ? '좌클릭 대상 중력 반전 · 우클릭 홀드 잡기, 놓으면 던지기 · F 내 중력 반전'
          : '좌클릭 중력 발판 설치 · 휠/Q 방향 회전 · 우클릭 발판 제거 · F 모든 발판 반전',
        W / 2, H - 9
      );
      ctx.textBaseline = 'alphabetic';
    }

    drawBanner(game) {
      const b = game.banner;
      if (!b) return;
      const ctx = this.ctx;
      ctx.globalAlpha = Math.min(1, b.t * 1.5);
      ctx.fillStyle = 'rgba(0,0,0,0.5)';
      ctx.fillRect(0, this.H / 2 - 50, this.W, 90);
      ctx.textAlign = 'center';
      ctx.fillStyle = '#ffffff';
      ctx.font = `bold 32px ${FONT}`;
      ctx.fillText(b.title, this.W / 2, this.H / 2 - 4);
      ctx.fillStyle = game.room.theme.accent;
      ctx.font = `15px ${FONT}`;
      ctx.fillText(b.sub, this.W / 2, this.H / 2 + 26);
      ctx.globalAlpha = 1;
    }
  }

  GS.Renderer = Renderer;
})(window.GS = window.GS || {});
