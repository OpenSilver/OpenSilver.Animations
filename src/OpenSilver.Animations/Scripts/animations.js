/*===================================================================================
*
*   Copyright (c) Userware (OpenSilver.net)
*
*   This file is part of the OpenSilver.Animations library, which is licensed under
*   the MIT license: https://opensource.org/licenses/MIT
*
\*====================================================================================*/

(function () {
    'use strict';

    if (globalThis.osAnimations) return;

    const FLAG_CLIP_OWN_BOX = 1;
    const FLAG_RECTANGLE = 2;
    const FLAG_ELLIPSE = 4;

    const FADE = 0;
    const REVEAL_FROM_TOP = 1;
    const REVEAL_FROM_LEFT = 2;
    const REVEAL_FROM_BOTTOM = 3;

    const ORIGIN_COLOR = 'rgba(255, 145, 0, 0.95)';
    const MEMBER_COLOR = 'rgba(233, 30, 99, 0.85)';

    function supports(property, value) {
        try { return CSS.supports(property, value); } catch { return false; }
    }

    const supportsLinear = supports('animation-timing-function', 'linear(0, 1)');
    const supportsGeometry = supports('rx', '1px') && supports('cx', '1px') && supports('x', '1px');

    // Running layout animations, per element.
    const states = new WeakMap();

    // Running wipes and fades of elements that appear, per element.
    const reveals = new WeakMap();

    // Elements that just became visible, hidden until their layout pass is done.
    const holds = new WeakMap();

    // Animations made since the last frame. They start at the next frame, not at the time of the
    // frame their batch runs in: a batch can run late in a frame, after a long resize, or in the
    // middle of a long task, and the browser would count that time as already played, so that
    // they would end before showing.
    let starting = null;

    function startAtNextFrame(animation) {
        if (!animation) return animation;
        animation.startTime = performance.now();
        if (!starting) {
            starting = [];
            requestAnimationFrame(() => {
                const animations = starting;
                starting = null;
                const now = performance.now();
                for (const a of animations) {
                    if (a.playState === 'running') a.startTime = now;
                }
            });
        }
        starting.push(animation);
        return animation;
    }

    function sampleAt(samples, p) {
        const n = samples.length - 1;
        const x = Math.max(0, Math.min(1, p)) * n;
        const i = Math.min(n - 1, Math.floor(x));
        return samples[i] + (samples[i + 1] - samples[i]) * (x - i);
    }

    // Returns how to play a timing curve: a CSS easing, or samples to bake into keyframes.
    function curveOf(timing, forSize) {
        if (forSize && timing.se) return { easing: timing.se, baked: null };
        if (timing.s) {
            if (supportsLinear) return { easing: 'linear(' + timing.s.join(', ') + ')', baked: null };
            return { easing: 'linear', baked: timing.s };
        }
        return { easing: timing.e || 'linear', baked: null };
    }

    // The eased progress (0 to 1, or beyond for springs) of a running animation.
    function progressOf(animation, curve) {
        if (!animation) return 1;
        const timing = animation.effect ? animation.effect.getComputedTiming() : null;
        let p;
        if (!timing || timing.progress === null || timing.progress === undefined) {
            p = animation.playState === 'finished' || timing === null || timing.localTime >= timing.endTime ? 1 : 0;
        } else {
            p = timing.progress;
        }
        return curve && curve.baked ? sampleAt(curve.baked, p) : p;
    }

    function lerpKeyframes(curve, make) {
        if (!curve.baked) return [make(0), make(1)];
        const n = curve.baked.length - 1;
        const frames = [];
        for (let i = 0; i <= n; i++) {
            const frame = make(curve.baked[i]);
            frame.offset = i / n;
            frames.push(frame);
        }
        return frames;
    }

    function px(v) {
        return (Math.round(v * 100) / 100) + 'px';
    }

    function cancelState(element, state) {
        if (state.move) state.move.cancel();
        if (state.size) state.size.cancel();
        if (state.geometry) state.geometry.cancel();
        if (states.get(element) === state) states.delete(element);
    }

    function trackEnd(element, state) {
        const animations = [state.move, state.size, state.geometry].filter(a => a);
        let pending = animations.length;
        for (const animation of animations) {
            animation.addEventListener('finish', () => {
                if (--pending === 0 && states.get(element) === state) states.delete(element);
            });
        }
    }

    function sizeKeyframes(state) {
        const { fromW, fromH, toW, toH, clip } = state;
        return lerpKeyframes(state.sizeCurve, (p) => {
            const frame = { width: px(Math.max(0, fromW + (toW - fromW) * p)), height: px(Math.max(0, fromH + (toH - fromH) * p)) };
            if (clip) frame.clipPath = 'inset(0px)';
            return frame;
        });
    }

    // The keyframes of the shape of a Rectangle or an Ellipse, from its attributes at the new size.
    function geometryKeyframes(shape, flags, dw, dh, curve) {
        const attr = (name) => parseFloat(shape.getAttribute(name)) || 0;

        if (flags & FLAG_RECTANGLE) {
            const w = attr('width'), h = attr('height');
            return lerpKeyframes(curve, (p) => ({
                width: px(Math.max(0, w - dw * (1 - p))),
                height: px(Math.max(0, h - dh * (1 - p))),
            }));
        }

        if (flags & FLAG_ELLIPSE) {
            const rx = attr('rx'), ry = attr('ry'), cx = attr('cx'), cy = attr('cy');
            return lerpKeyframes(curve, (p) => ({
                rx: px(Math.max(0, rx - dw / 2 * (1 - p))),
                ry: px(Math.max(0, ry - dh / 2 * (1 - p))),
                cx: px(cx - dw / 2 * (1 - p)),
                cy: px(cy - dh / 2 * (1 - p)),
            }));
        }

        return null;
    }

    function animateGeometry(element, flags, fromW, fromH, toW, toH, options, curve) {
        if (!supportsGeometry) return null;
        const shape = element.firstElementChild;
        if (!shape) return null;

        const frames = geometryKeyframes(shape, flags, toW - fromW, toH - fromH, curve);
        return frames ? shape.animate(frames, options) : null;
    }

    function animateBox(element, timing, delay, dx, dy, ow, oh, nw, nh, flags) {
        let fromX = dx, fromY = dy, fromW = ow, fromH = oh;

        // Continue from where the element is on screen, so that interrupted animations stay fluid.
        const previous = states.get(element);
        if (previous) {
            const pm = progressOf(previous.move, previous.moveCurve);
            fromX += previous.fromX * (1 - pm);
            fromY += previous.fromY * (1 - pm);
            if (previous.size) {
                const ps = progressOf(previous.size, previous.sizeCurve);
                fromW = previous.fromW + (previous.toW - previous.fromW) * ps;
                fromH = previous.fromH + (previous.toH - previous.fromH) * ps;
            }
            cancelState(element, previous);
        }

        const state = { fromX, fromY, fromW, fromH, toW: nw, toH: nh, flags, clip: (flags & FLAG_CLIP_OWN_BOX) !== 0, move: null, size: null, geometry: null };
        const duration = Math.max(1, timing.d);

        if (Math.abs(fromX) >= 0.5 || Math.abs(fromY) >= 0.5) {
            const curve = curveOf(timing, false);
            state.moveCurve = curve;
            state.move = startAtNextFrame(element.animate(
                lerpKeyframes(curve, (p) => ({ translate: px(fromX * (1 - p)) + ' ' + px(fromY * (1 - p)) })),
                { duration, delay, fill: 'backwards', easing: curve.easing, composite: 'add' }));
        }

        if (Math.abs(fromW - nw) >= 0.5 || Math.abs(fromH - nh) >= 0.5) {
            const curve = curveOf(timing, true);
            state.sizeCurve = curve;
            const options = { duration, delay, fill: 'backwards', easing: curve.easing };
            state.size = startAtNextFrame(element.animate(sizeKeyframes(state), options));
            if (flags & (FLAG_RECTANGLE | FLAG_ELLIPSE)) {
                state.geometry = startAtNextFrame(animateGeometry(element, flags, fromW, fromH, nw, nh, options, curve));
            }
        }

        if (state.move || state.size) {
            states.set(element, state);
            trackEnd(element, state);
        }
    }

    // The element got a new size without animating, like the rest of a resize it follows, while
    // its size animates. Both ends of the animation move by the same amount: the element follows
    // the resize at once, and what is left of its change plays on top, like a move, which is
    // added to where the layout puts the element.
    function retarget(element, nw, nh) {
        const state = states.get(element);
        if (!state || !state.size) return;

        const dw = nw - state.toW, dh = nh - state.toH;
        if (Math.abs(dw) < 0.01 && Math.abs(dh) < 0.01) return;

        const effect = state.size.effect;
        if (!effect || !effect.setKeyframes) {
            cancelState(element, state);
            return;
        }

        state.fromW += dw;
        state.fromH += dh;
        state.toW = nw;
        state.toH = nh;
        effect.setKeyframes(sizeKeyframes(state));

        if (state.geometry && state.geometry.effect) {
            const frames = geometryKeyframes(state.geometry.effect.target, state.flags, nw - state.fromW, nh - state.fromH, state.sizeCurve);
            if (frames) state.geometry.effect.setKeyframes(frames);
        }
    }

    // The clip of a wipe from the given edge, p going from 0 (hidden) to 1 (fully shown).
    function wipeAt(edge, p) {
        const hidden = (Math.round((1 - Math.max(0, Math.min(1, p))) * 10000) / 100) + '%';
        switch (edge) {
            case REVEAL_FROM_TOP: return 'inset(0px 0px ' + hidden + ' 0px)';
            case REVEAL_FROM_LEFT: return 'inset(0px ' + hidden + ' 0px 0px)';
            case REVEAL_FROM_BOTTOM: return 'inset(' + hidden + ' 0px 0px 0px)';
            default: return 'inset(0px 0px 0px ' + hidden + ')';
        }
    }

    // Hidden for at most a second, in case no batch ever says what to do with the element.
    function hold(id) {
        const element = document.getElementById(id);
        if (!element) return;
        release(element);
        holds.set(element, element.animate([{ opacity: 0 }, { opacity: 0 }], { duration: 1000 }));
    }

    function release(element) {
        const animation = holds.get(element);
        if (animation) {
            animation.cancel();
            holds.delete(element);
        }
    }

    // Wipes or fades in an element that appears, with the size curve, so that the revealed part
    // keeps up with the elements it pushes.
    function reveal(element, timing, delay, edge) {
        release(element);
        const previous = reveals.get(element);
        if (previous) previous.cancel();

        const curve = curveOf(timing, true);
        const options = { duration: Math.max(1, timing.d), delay, fill: 'backwards', easing: curve.easing };
        let animation;
        if (edge === FADE) {
            const opacity = parseFloat(getComputedStyle(element).opacity);
            const target = isNaN(opacity) ? 1 : opacity;
            animation = element.animate(lerpKeyframes(curve, (p) => ({ opacity: target * p })), options);
        } else {
            animation = element.animate(lerpKeyframes(curve, (p) => ({ clipPath: wipeAt(edge, p) })), options);
        }

        reveals.set(element, startAtNextFrame(animation));
        animation.addEventListener('finish', () => {
            if (reveals.get(element) === animation) reveals.delete(element);
        });
    }

    function outline(element, duration, delay, isOrigin) {
        const color = isOrigin ? ORIGIN_COLOR : MEMBER_COLOR;
        const width = isOrigin ? '3px' : '2px';
        const visible = { outline: width + ' solid ' + color, outlineOffset: '-' + width };
        const hidden = { outline: width + ' solid transparent', outlineOffset: '-' + width };
        startAtNextFrame(element.animate(
            [Object.assign({ offset: 0 }, visible), Object.assign({ offset: 0.85 }, visible), Object.assign({ offset: 1 }, hidden)],
            { duration: Math.max(700, duration + 300), delay: delay }));
    }

    function run(batch) {
        if (batch.c) {
            for (const id of batch.c) {
                const element = document.getElementById(id);
                const state = element && states.get(element);
                if (state) cancelState(element, state);
            }
        }

        if (batch.s) {
            for (const s of batch.s) {
                const element = document.getElementById(s[0]);
                if (element) retarget(element, s[1], s[2]);
            }
        }

        if (batch.m) {
            for (const m of batch.m) {
                const element = document.getElementById(m[0]);
                if (!element) continue;
                animateBox(element, batch.t[m[1]], m[2], m[3], m[4], m[5], m[6], m[7], m[8], m[9]);
            }
        }

        if (batch.a) {
            for (const a of batch.a) {
                const element = document.getElementById(a[0]);
                if (element) reveal(element, batch.t[a[1]], a[2], a[3]);
            }
        }

        if (batch.o) {
            for (const o of batch.o) {
                const element = document.getElementById(o[0]);
                if (element) outline(element, o[1], o[2], o[3] === 1);
            }
        }

        if (batch.r) {
            for (const id of batch.r) {
                const element = document.getElementById(id);
                if (element) release(element);
            }
        }
    }

    // Dragging: a pointer button is held and the pointer moved by more than 4 pixels.
    let dragStarted = null, dragEnded = null, pointerDown = null, dragging = false;

    function onPointerDown(e) {
        if (e.pointerType === 'mouse' && e.button !== 0) return;
        pointerDown = { x: e.clientX, y: e.clientY };
    }

    function onPointerMove(e) {
        if (!pointerDown || dragging) return;
        if (e.buttons === 0) { pointerDown = null; return; }
        if (Math.hypot(e.clientX - pointerDown.x, e.clientY - pointerDown.y) > 4) {
            dragging = true;
            if (dragStarted) dragStarted();
        }
    }

    function onPointerUp() {
        pointerDown = null;
        if (dragging) {
            dragging = false;
            if (dragEnded) dragEnded();
        }
    }

    function init(onDragStarted, onDragEnded, onReducedMotionOn, onReducedMotionOff) {
        dragStarted = onDragStarted;
        dragEnded = onDragEnded;
        window.addEventListener('pointerdown', onPointerDown, true);
        window.addEventListener('pointermove', onPointerMove, true);
        window.addEventListener('pointerup', onPointerUp, true);
        window.addEventListener('pointercancel', onPointerUp, true);

        const query = window.matchMedia ? window.matchMedia('(prefers-reduced-motion: reduce)') : null;
        if (query) {
            const notify = (matches) => { if (matches) onReducedMotionOn(); else onReducedMotionOff(); };
            if (query.addEventListener) query.addEventListener('change', e => notify(e.matches));
            notify(query.matches);
        }
    }

    globalThis.osAnimations = Object.freeze({ init, run, hold });
})();
