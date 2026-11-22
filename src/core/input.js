// 입력. Unity 이식 시 Input System(PlayerInput / InputAction)으로 대체.
(function (GS) {
  'use strict';
  const BLOCK = ['Space', 'Tab', 'ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight'];

  class Input {
    constructor(canvas) {
      this.canvas = canvas;
      this.down = new Set();
      this.pressedSet = new Set();
      this.mouse = new GS.Vec2(canvas.width / 2, canvas.height / 2);
      this.mDown = [false, false, false];
      this.mPressed = [false, false, false];
      this.wheel = 0;

      window.addEventListener('keydown', (e) => {
        if (BLOCK.includes(e.code)) e.preventDefault();
        if (!this.down.has(e.code)) this.pressedSet.add(e.code);
        this.down.add(e.code);
      });
      window.addEventListener('keyup', (e) => this.down.delete(e.code));
      window.addEventListener('blur', () => { this.down.clear(); this.mDown = [false, false, false]; });

      canvas.addEventListener('mousemove', (e) => this.updateMouse(e));
      canvas.addEventListener('mousedown', (e) => {
        this.updateMouse(e);
        if (e.button > 2) return;
        this.mDown[e.button] = true;
        this.mPressed[e.button] = true;
        e.preventDefault();
      });
      window.addEventListener('mouseup', (e) => { if (e.button <= 2) this.mDown[e.button] = false; });
      canvas.addEventListener('contextmenu', (e) => e.preventDefault());
      canvas.addEventListener('wheel', (e) => { this.wheel += Math.sign(e.deltaY); e.preventDefault(); }, { passive: false });
    }

    updateMouse(e) {
      const r = this.canvas.getBoundingClientRect();
      this.mouse = new GS.Vec2(
        ((e.clientX - r.left) * this.canvas.width) / r.width,
        ((e.clientY - r.top) * this.canvas.height) / r.height
      );
    }

    key(code) { return this.down.has(code); }
    pressed(code) { return this.pressedSet.has(code); }
    mouseDown(b) { return this.mDown[b]; }
    mousePressed(b) { return this.mPressed[b]; }

    get axisX() {
      return (this.key('KeyD') || this.key('ArrowRight') ? 1 : 0) - (this.key('KeyA') || this.key('ArrowLeft') ? 1 : 0);
    }
    get axisY() {
      return (this.key('KeyS') || this.key('ArrowDown') ? 1 : 0) - (this.key('KeyW') || this.key('ArrowUp') ? 1 : 0);
    }

    // 고정 업데이트 1틱이 끝날 때마다 호출
    endFrame() {
      this.pressedSet.clear();
      this.mPressed = [false, false, false];
      this.wheel = 0;
    }
  }

  GS.Input = Input;
})(window.GS = window.GS || {});
