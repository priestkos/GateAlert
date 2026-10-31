(() => {
  if (window.__wt) return;

  const easeOutCubic = (t) => 1 - (1 - t) ** 3;
  const easeInOutCubic = (t) =>
    t < 0.5 ? 4 * t * t * t : 1 - (-2 * t + 2) ** 3 / 2;

  function ensureDom() {
    if (document.getElementById('wt-root')) return;

    const style = document.createElement('style');
    style.textContent = `
      #wt-root { position: fixed; inset: 0; pointer-events: none; z-index: 99990; overflow: hidden; }
      #wt-veil {
        position: absolute; inset: 0; background: #0a0a0c;
        opacity: 0; transition: opacity 0.45s ease;
      }
      #wt-veil.wt-veil-visible { opacity: 1; }
      #wt-cursor {
        position: absolute; left: 0; top: 0;
        width: 28px; height: 28px; margin: -2px 0 0 -2px;
        filter: drop-shadow(0 1px 2px rgba(0,0,0,0.65));
        transform: translate(640px, 360px);
        will-change: transform;
      }
      #wt-cursor svg { display: block; }
      #wt-cursor.wt-cursor-down { transform: translate(var(--wt-x), var(--wt-y)) scale(0.88); }
      .wt-ripple {
        position: absolute; width: 14px; height: 14px; margin: -7px 0 0 -7px;
        border-radius: 50%; border: 2px solid rgba(245, 199, 74, 0.85);
        background: rgba(245, 199, 74, 0.2);
        animation: wt-ripple-pop 0.55s ease-out forwards;
      }
      @keyframes wt-ripple-pop {
        0% { transform: scale(0.35); opacity: 1; }
        100% { transform: scale(2.6); opacity: 0; }
      }
      .wt-panel-enter {
        animation: wt-panel-in 0.55s ease-out both;
      }
      @keyframes wt-panel-in {
        from { opacity: 0; transform: translateY(10px); }
        to { opacity: 1; transform: translateY(0); }
      }
      .wt-panel-enter-center {
        animation: wt-panel-in-center 0.55s ease-out both;
      }
      @keyframes wt-panel-in-center {
        from { opacity: 0; transform: translate(-50%, calc(-50% + 10px)); }
        to { opacity: 1; transform: translate(-50%, -50%); }
      }
      .intro-wrap.wt-panel-enter { animation: wt-intro-in 0.7s ease-out both; }
      @keyframes wt-intro-in {
        from { opacity: 0; transform: translateY(14px); }
        to { opacity: 1; transform: translateY(0); }
      }
    `;
    document.head.appendChild(style);

    const root = document.createElement('div');
    root.id = 'wt-root';
    root.innerHTML = `
      <div id="wt-veil"></div>
      <div id="wt-cursor" aria-hidden="true">
        <svg width="28" height="28" viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg">
          <path fill="#ffffff" stroke="#1a1a22" stroke-width="1.1" stroke-linejoin="round"
            d="M4.5 3.2v16.8l4.2-3.4 3.1 5.8 2.2-1.1-3.4-6.2h4.8z"/>
        </svg>
      </div>
    `;
    document.body.appendChild(root);
  }

  function setCursorPos(x, y) {
    const cursor = document.getElementById('wt-cursor');
    if (!cursor) return;
    cursor.style.setProperty('--wt-x', `${x}px`);
    cursor.style.setProperty('--wt-y', `${y}px`);
    cursor.style.transform = `translate(${x}px, ${y}px)`;
  }

  function getCursorPos() {
    const cursor = document.getElementById('wt-cursor');
    if (!cursor) return { x: 640, y: 360 };
    const m = /translate\(([-\d.]+)px,\s*([-\d.]+)px\)/.exec(cursor.style.transform);
    if (m) return { x: parseFloat(m[1]), y: parseFloat(m[2]) };
    return { x: 640, y: 360 };
  }

  function rippleAt(x, y) {
    const root = document.getElementById('wt-root');
    if (!root) return;
    const el = document.createElement('div');
    el.className = 'wt-ripple';
    el.style.left = `${x}px`;
    el.style.top = `${y}px`;
    root.appendChild(el);
    el.addEventListener('animationend', () => el.remove(), { once: true });
  }

  function pressAt(x, y) {
    const target = document.elementFromPoint(x, y);
    if (!target || target.closest('#wt-root')) return null;
    const clickable =
      target.closest('button, label, .slider-track, .slider-knob, input') || target;
    const prev = clickable.style.transition;
    clickable.style.transition = 'transform 0.1s ease, filter 0.1s ease';
    clickable.style.transform = 'scale(0.96)';
    clickable.style.filter = 'brightness(1.08)';
    window.setTimeout(() => {
      clickable.style.transform = '';
      clickable.style.filter = '';
      clickable.style.transition = prev;
    }, 160);
    return clickable;
  }

  function animatePanelEntrance() {
    const imwin = document.querySelector('.imwin');
    if (imwin) {
      if (imwin.classList.contains('pos-center')) imwin.classList.add('wt-panel-enter-center');
      else imwin.classList.add('wt-panel-enter');
    }
    const intro = document.querySelector('.intro-wrap');
    if (intro) intro.classList.add('wt-panel-enter');
  }

  function wait(ms) {
    return new Promise((resolve) => window.setTimeout(resolve, ms));
  }

  function animateValue(start, end, durationMs, easeFn, onFrame) {
    return new Promise((resolve) => {
      const t0 = performance.now();
      function frame(now) {
        const t = Math.min(1, (now - t0) / durationMs);
        const v = start + (end - start) * easeFn(t);
        onFrame(v, t);
        if (t < 1) requestAnimationFrame(frame);
        else resolve();
      }
      requestAnimationFrame(frame);
    });
  }

  window.__wt = {
    ensureDom,
    easeOutCubic,
    easeInOutCubic,
    wait,
    animatePanelEntrance,

    async fadeVeil(toVisible, durationMs = 450) {
      ensureDom();
      const veil = document.getElementById('wt-veil');
      if (!veil) return;
      veil.style.transition = `opacity ${durationMs}ms ease`;
      if (toVisible) veil.classList.add('wt-veil-visible');
      else veil.classList.remove('wt-veil-visible');
      await wait(durationMs + 40);
    },

    async moveCursor(x, y, durationMs = 580) {
      ensureDom();
      const { x: x0, y: y0 } = getCursorPos();
      await animateValue(0, 1, durationMs, easeOutCubic, (t) => {
        setCursorPos(x0 + (x - x0) * t, y0 + (y - y0) * t);
      });
    },

    async performClick(x, y) {
      ensureDom();
      const cursor = document.getElementById('wt-cursor');
      setCursorPos(x, y);
      if (cursor) {
        cursor.classList.add('wt-cursor-down');
        cursor.style.transform = `translate(${x}px, ${y}px) scale(0.88)`;
      }
      rippleAt(x, y);
      pressAt(x, y);
      await wait(120);
      if (cursor) {
        cursor.classList.remove('wt-cursor-down');
        cursor.style.transform = `translate(${x}px, ${y}px)`;
      }
      await wait(80);
    },

    async clickAt(x, y, moveMs = 580) {
      await window.__wt.moveCursor(x, y, moveMs);
      await window.__wt.performClick(x, y);
    },

    async dragCursor(x0, y0, x1, y1, durationMs = 700) {
      ensureDom();
      await window.__wt.moveCursor(x0, y0, 420);
      await window.__wt.performClick(x0, y0);
      await animateValue(0, 1, durationMs, easeInOutCubic, (t) => {
        setCursorPos(x0 + (x1 - x0) * t, y0 + (y1 - y0) * t);
      });
      rippleAt(x1, y1);
      await wait(100);
    },
  };

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', () => {
      ensureDom();
      animatePanelEntrance();
    });
  } else {
    ensureDom();
    animatePanelEntrance();
  }
})();
