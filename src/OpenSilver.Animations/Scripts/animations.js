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

    const ORIGIN_COLOR = 'rgba(255, 145, 0, 0.95)';
    const MEMBER_COLOR = 'rgba(233, 30, 99, 0.85)';

    function supports(property, value) {
        try { return CSS.supports(property, value); } catch { return false; }
    }

    const supportsLinear = supports('animation-timing-function', 'linear(0, 1)');
    const supportsGeometry = supports('rx', '1px') && supports('cx', '1px') && supports('x', '1px');

    // Running layout animations, per element.
    const states = new WeakMap();

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

    function animateGeometry(element, flags, fromW, fromH, toW, toH, options, curve) {
        if (!supportsGeometry) return null;
        const shape = element.firstElementChild;
        if (!shape) return null;

        const dw = toW - fromW;
        const dh = toH - fromH;
        const attr = (name) => parseFloat(shape.getAttribute(name)) || 0;

        if (flags & FLAG_RECTANGLE) {
            const w = attr('width'), h = attr('height');
            return shape.animate(lerpKeyframes(curve, (p) => ({
                width: px(Math.max(0, w - dw * (1 - p))),
                height: px(Math.max(0, h - dh * (1 - p))),
            })), options);
        }

        if (flags & FLAG_ELLIPSE) {
            const rx = attr('rx'), ry = attr('ry'), cx = attr('cx'), cy = attr('cy');
            return shape.animate(lerpKeyframes(curve, (p) => ({
                rx: px(Math.max(0, rx - dw / 2 * (1 - p))),
                ry: px(Math.max(0, ry - dh / 2 * (1 - p))),
                cx: px(cx - dw / 2 * (1 - p)),
                cy: px(cy - dh / 2 * (1 - p)),
            })), options);
        }

        return null;
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

        const state = { fromX, fromY, fromW, fromH, toW: nw, toH: nh, move: null, size: null, geometry: null };
        const duration = Math.max(1, timing.d);

        if (Math.abs(fromX) >= 0.5 || Math.abs(fromY) >= 0.5) {
            const curve = curveOf(timing, false);
            state.moveCurve = curve;
            state.move = element.animate(
                lerpKeyframes(curve, (p) => ({ translate: px(fromX * (1 - p)) + ' ' + px(fromY * (1 - p)) })),
                { duration, delay, fill: 'backwards', easing: curve.easing, composite: 'add' });
        }

        if (Math.abs(fromW - nw) >= 0.5 || Math.abs(fromH - nh) >= 0.5) {
            const curve = curveOf(timing, true);
            const clip = (flags & FLAG_CLIP_OWN_BOX) !== 0;
            state.sizeCurve = curve;
            const options = { duration, delay, fill: 'backwards', easing: curve.easing };
            state.size = element.animate(
                lerpKeyframes(curve, (p) => {
                    const frame = { width: px(fromW + (nw - fromW) * p), height: px(fromH + (nh - fromH) * p) };
                    if (clip) frame.clipPath = 'inset(0px)';
                    return frame;
                }),
                options);
            if (flags & (FLAG_RECTANGLE | FLAG_ELLIPSE)) {
                state.geometry = animateGeometry(element, flags, fromW, fromH, nw, nh, options, curve);
            }
        }

        if (state.move || state.size) {
            states.set(element, state);
            trackEnd(element, state);
        }
    }

    function outline(element, duration, delay, isOrigin) {
        const color = isOrigin ? ORIGIN_COLOR : MEMBER_COLOR;
        const width = isOrigin ? '3px' : '2px';
        const visible = { outline: width + ' solid ' + color, outlineOffset: '-' + width };
        const hidden = { outline: width + ' solid transparent', outlineOffset: '-' + width };
        element.animate(
            [Object.assign({ offset: 0 }, visible), Object.assign({ offset: 0.85 }, visible), Object.assign({ offset: 1 }, hidden)],
            { duration: Math.max(700, duration + 300), delay: delay });
    }

    function run(batch) {
        if (batch.c) {
            for (const id of batch.c) {
                const element = document.getElementById(id);
                const state = element && states.get(element);
                if (state) cancelState(element, state);
            }
        }

        if (batch.m) {
            for (const m of batch.m) {
                const element = document.getElementById(m[0]);
                if (!element) continue;
                animateBox(element, batch.t[m[1]], m[2], m[3], m[4], m[5], m[6], m[7], m[8], m[9]);
            }
        }

        if (batch.o) {
            for (const o of batch.o) {
                const element = document.getElementById(o[0]);
                if (element) outline(element, o[1], o[2], o[3] === 1);
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

    globalThis.osAnimations = Object.freeze({ init, run });
})();
