// 모든 튜닝 수치. Unity 이식 시 ScriptableObject(GameConfig)로 옮긴다.
// 단위: 픽셀, 초. (Unity에서는 TILE=1 유닛으로 나누어 쓰면 된다)
(function (GS) {
  'use strict';
  GS.Config = {
    TILE: 32,
    COLS: 30,
    ROWS: 17,
    GRAVITY: 1800,
    MAX_FALL: 900,

    player: {
      w: 22, h: 24,
      speed: 250, accel: 2600, airAccel: 1500,
      jump: 640, jumpCut: 260, coyote: 0.1, jumpBuffer: 0.12,
      dashSpeed: 640, dashTime: 0.14, dashCd: 0.55,
      hp: 5, invuln: 1.0,
      attackRange: 34, attackCd: 0.28, attackKnock: 420, attackLift: 300,
    },
    enemy: { w: 24, h: 24, speed: 75, hp: 3, anchorHp: 5, chaseRange: 200 },
    box: { w: 28, h: 28, bounce: 0.25 },

    // 충격 피해: 속도가 기준을 넘으면 1 + (초과량 / perDamage) 피해
    impact: { wall: 480, collide: 380, perDamage: 120 },

    psychic: {
      mana: 100, regen: 18,
      flipCost: 20, flipTime: 5,
      grabDrain: 32, grabPull: 12, grabMaxSpeed: 1000,
      selfFlipCost: 20,
    },
    engineer: { maxZones: 3, beamWidth: 3, cooldown: 0.25 },
  };
})(window.GS = window.GS || {});
