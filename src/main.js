// 진입점: 고정 시간 간격(60Hz) 업데이트 + 가변 렌더링.
// Unity 의 FixedUpdate(로직) / Update(렌더) 분리와 같은 구조.
(function (GS) {
  'use strict';
  window.addEventListener('load', () => {
    const canvas = document.getElementById('game');
    const game = new GS.Game(canvas);
    const renderer = new GS.Renderer(canvas);
    const STEP = 1 / 60;
    let last = performance.now(), acc = 0;

    function frame(now) {
      acc += Math.min(0.1, (now - last) / 1000);
      last = now;
      while (acc >= STEP) {
        game.update(STEP);
        game.input.endFrame();
        acc -= STEP;
      }
      renderer.draw(game);
      requestAnimationFrame(frame);
    }
    requestAnimationFrame(frame);
    window.__game = game; // 디버그용
  });
})(window.GS = window.GS || {});
