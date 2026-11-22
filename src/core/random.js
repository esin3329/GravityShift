// 시드 기반 난수 (mulberry32). 같은 시드 = 같은 런.
// Unity 이식 시 System.Random(seed) 또는 이 알고리즘을 C#으로 그대로 옮긴다 (Unity.Random 은 전역 상태라 비권장).
(function (GS) {
  'use strict';

  class RNG {
    constructor(seed) {
      this.seed = seed >>> 0;
      this.state = this.seed | 0;
    }
    next() {
      this.state = (this.state + 0x6d2b79f5) | 0;
      let t = this.state;
      t = Math.imul(t ^ (t >>> 15), t | 1);
      t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
      return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
    }
    int(a, b) { return a + Math.floor(this.next() * (b - a + 1)); } // a 이상 b 이하
    chance(p) { return this.next() < p; }
    pick(arr) { return arr[this.int(0, arr.length - 1)]; }
    shuffle(arr) {
      for (let i = arr.length - 1; i > 0; i--) {
        const j = this.int(0, i);
        [arr[i], arr[j]] = [arr[j], arr[i]];
      }
      return arr;
    }
  }

  RNG.randomSeed = () => Math.floor(Math.random() * 1e9);
  GS.RNG = RNG;
})(window.GS = window.GS || {});
