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

    // Running effects, wipes and fades of elements that appear, per element: { animation, curve }.
    const effects = new WeakMap();

    // Copies of elements that disappear, per id of the element, until their effect ends:
    // { clone, from, x, y, opacity, animation, curve, timer }, where from is how much of the
    // element showed (1 unless it was appearing).
    const ghosts = new Map();

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

    // Like lerpKeyframes, with at least the given number of steps, for values that don't change
    // linearly with the progress, like the move that keeps a rotating element on its center.
    function sampledKeyframes(curve, make, steps) {
        if (curve.baked || steps <= 1) return lerpKeyframes(curve, make);
        const frames = [];
        for (let i = 0; i <= steps; i++) {
            const frame = make(i / steps);
            frame.offset = i / steps;
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
        unhold(element);
        holds.set(element, element.animate([{ opacity: 0 }, { opacity: 0 }], { duration: 1000 }));
    }

    function unhold(element) {
        const animation = holds.get(element);
        if (animation) {
            animation.cancel();
            holds.delete(element);
        }
    }

    // Shows an element that was held, and removes the ghost it had if it disappeared just before.
    function release(element) {
        unhold(element);
        removeGhost(element.id);
    }

    function ownOpacity(element) {
        const opacity = parseFloat(getComputedStyle(element).opacity);
        return isNaN(opacity) ? 1 : opacity;
    }

    function track(element, animation, curve) {
        effects.set(element, { animation: startAtNextFrame(animation), curve });
        animation.addEventListener('finish', () => {
            const running = effects.get(element);
            if (running && running.animation === animation) effects.delete(element);
        });
    }

    // Wipes or fades in an element that appears, with the size curve, so that the revealed part
    // keeps up with the elements it pushes.
    function reveal(element, timing, delay, edge) {
        const from = takeGhost(element);
        release(element);
        const previous = effects.get(element);
        if (previous) previous.animation.cancel();

        const curve = curveOf(timing, true);
        const options = { duration: Math.max(1, timing.d), delay, fill: 'backwards', easing: curve.easing };
        const shown = (p) => from + (1 - from) * p;
        let animation;
        if (edge === FADE) {
            const target = ownOpacity(element);
            animation = element.animate(lerpKeyframes(curve, (p) => ({ opacity: target * shown(p) })), options);
        } else {
            animation = element.animate(lerpKeyframes(curve, (p) => ({ clipPath: wipeAt(edge, shown(p)) })), options);
        }

        track(element, animation, curve);
    }

    // Animates an element, or a ghost, between a look and the element as it is. shownAt gives, for
    // a progress from 0 to 1, how much of the element shows: 0 is the look, 1 is the element.
    // Zoom and rotation turn around the center, which takes a move, because elements turn around
    // their top left corner.
    function animateLook(target, look, curve, shownAt, base, options, raise) {
        const o = look[0], x = look[1], y = look[2], s = look[3], r = look[4], b = look[5], w = look[6];
        const style = getComputedStyle(target);
        const opacity = base.opacity !== undefined ? base.opacity : ownOpacity(target);
        const filter = style.filter && style.filter !== 'none' ? style.filter + ' ' : '';
        const width = parseFloat(target.style.width) || target.offsetWidth || 0;
        const height = parseFloat(target.style.height) || target.offsetHeight || 0;
        const cx = width / 2, cy = height / 2;
        const moves = x !== 0 || y !== 0 || s !== 1 || r !== 0;
        let z = null;
        if (raise) {
            const own = parseInt(style.zIndex, 10);
            z = String(isNaN(own) || own < 1 ? 1 : own);
        }

        const make = (p) => {
            const v = shownAt(p), h = 1 - v;
            const frame = {};
            if (o !== 1 || base.opacity !== undefined) frame.opacity = opacity * (o + (1 - o) * v);
            if (moves) {
                const scale = s + (1 - s) * v, angle = r * h;
                const rad = angle * Math.PI / 180, cos = Math.cos(rad), sin = Math.sin(rad);
                frame.translate = px(base.x + x * h + cx - scale * (cos * cx - sin * cy)) + ' ' +
                                  px(base.y + y * h + cy - scale * (sin * cx + cos * cy));
                if (s !== 1) frame.scale = String(Math.round(scale * 10000) / 10000);
                if (r !== 0) frame.rotate = (Math.round(angle * 100) / 100) + 'deg';
            }
            if (b > 0) frame.filter = filter + 'blur(' + px(b * h) + ')';
            if (w > 0) frame.clipPath = wipeAt(w, v);
            if (z !== null) frame.zIndex = z;
            return frame;
        };

        return target.animate(sampledKeyframes(curve, make, r !== 0 ? 12 : 1), options);
    }

    // Plays the effect of an element that appears. If it disappeared just before and its ghost is
    // still there, it goes on from what the ghost showed.
    function appear(element, timing, delay, look, raise) {
        const from = takeGhost(element);
        release(element);
        const previous = effects.get(element);
        if (previous) previous.animation.cancel();

        const curve = curveOf(timing, false);
        const options = { duration: Math.max(1, timing.d), delay, fill: 'backwards', easing: curve.easing };
        const animation = animateLook(element, look, curve, (p) => from + (1 - from) * p, { x: 0, y: 0 }, options, raise);
        track(element, animation, curve);
    }

    function copyFormState(from, to) {
        const canvases = from.getElementsByTagName('canvas');
        if (canvases.length > 0) {
            const copies = to.getElementsByTagName('canvas');
            for (let i = 0; i < canvases.length && i < copies.length; i++) {
                try { copies[i].getContext('2d').drawImage(canvases[i], 0, 0); } catch { }
            }
        }
        const inputs = from.querySelectorAll('input, textarea');
        if (inputs.length > 0) {
            const copies = to.querySelectorAll('input, textarea');
            for (let i = 0; i < inputs.length && i < copies.length; i++) copies[i].value = inputs[i].value;
        }
    }

    // In a copy, media and frames would load again, and could play sound. They become an empty
    // box of the same size, and the copy fades out quickly instead of playing its effect.
    const MEDIA = 'video, audio, iframe, object, embed';
    const QUICK_FADE = { d: 150, e: 'linear' };
    const FADE_LOOK = [0, 0, 0, 1, 0, 0, 0];

    function replaceMedia(from, to) {
        const originals = from.querySelectorAll(MEDIA);
        if (originals.length === 0) return false;
        const copies = to.querySelectorAll(MEDIA);
        for (let i = 0; i < originals.length && i < copies.length; i++) {
            const box = document.createElement('div');
            box.style.cssText = copies[i].style.cssText;
            // A collapsed element is already display: none, and measures 0.
            const width = originals[i].offsetWidth, height = originals[i].offsetHeight;
            if (width > 0 || height > 0) {
                box.style.width = px(width);
                box.style.height = px(height);
            }
            copies[i].replaceWith(box);
        }
        return true;
    }

    // Copies an element that disappears, before the browser hides or removes it, at the same place
    // among its siblings, so that it stacks the same way. The copy starts from where the element is
    // on screen, so that an element that disappears while it appears or moves doesn't jump. It
    // stays until a batch plays its effect or removes it, for at most 1.5 seconds.
    function ghost(id) {
        const element = document.getElementById(id);
        const parent = element && element.parentNode;
        if (!parent || holds.has(element) || element.matches(MEDIA)) return;
        removeGhost(id);

        let from = 1;
        const running = effects.get(element);
        if (running) {
            from = Math.max(0, Math.min(1, progressOf(running.animation, running.curve)));
            running.animation.cancel();
            effects.delete(element);
        }
        const opacity = ownOpacity(element);

        const clone = element.cloneNode(true);
        let x = 0, y = 0;
        const state = states.get(element);
        if (state) {
            const pm = progressOf(state.move, state.moveCurve);
            x = state.fromX * (1 - pm);
            y = state.fromY * (1 - pm);
            if (state.size) {
                const ps = progressOf(state.size, state.sizeCurve);
                clone.style.width = px(state.fromW + (state.toW - state.fromW) * ps);
                clone.style.height = px(state.fromH + (state.toH - state.fromH) * ps);
            }
            cancelState(element, state);
        }

        clone.removeAttribute('id');
        for (const e of clone.querySelectorAll('[id]')) e.removeAttribute('id');
        clone.classList.remove('uielement-collapsed', 'uielement-hidden');
        clone.setAttribute('data-os-foreign', '');
        clone.style.pointerEvents = 'none';
        if (x !== 0 || y !== 0) clone.style.translate = px(x) + ' ' + px(y);
        if (from < 1) clone.style.opacity = String(opacity * from);
        copyFormState(element, clone);
        const media = replaceMedia(element, clone);
        parent.insertBefore(clone, element);

        const entry = { clone, from, x, y, opacity, media, animation: null, curve: null, timer: 0 };
        entry.timer = setTimeout(() => removeGhost(id, entry), 1500);
        ghosts.set(id, entry);
    }

    function removeGhost(id, entry) {
        const ghost = ghosts.get(id);
        if (!ghost || (entry && ghost !== entry)) return;
        ghosts.delete(id);
        clearTimeout(ghost.timer);
        if (ghost.animation) ghost.animation.cancel();
        ghost.clone.remove();
    }

    // Removes the ghost of an element that appears again, and returns how much of it showed.
    function takeGhost(element) {
        const ghost = ghosts.get(element.id);
        if (!ghost) return 0;
        const shown = ghost.animation
            ? ghost.from * (1 - Math.max(0, Math.min(1, progressOf(ghost.animation, ghost.curve))))
            : ghost.from;
        removeGhost(element.id);
        return shown;
    }

    // Plays the effect of an element that disappeared on its ghost, then removes the ghost.
    function vanish(id, timing, delay, look, sizeCurve) {
        const ghost = ghosts.get(id);
        if (!ghost) return;
        clearTimeout(ghost.timer);
        if (ghost.media) {
            timing = QUICK_FADE;
            look = FADE_LOOK;
            sizeCurve = false;
        }

        const curve = curveOf(timing, sizeCurve);
        const from = ghost.from;
        const options = { duration: Math.max(1, timing.d), delay, fill: 'both', easing: curve.easing };
        const animation = animateLook(ghost.clone, look, curve, (p) => from * (1 - p), { x: ghost.x, y: ghost.y, opacity: ghost.opacity }, options, false);
        ghost.animation = startAtNextFrame(animation);
        ghost.curve = curve;
        animation.addEventListener('finish', () => removeGhost(id, ghost));

        // In case it never finishes, like in a tab in the background.
        ghost.timer = setTimeout(() => removeGhost(id, ghost), delay + timing.d + 1000);
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

        if (batch.e) {
            for (const e of batch.e) {
                const element = document.getElementById(e[0]);
                if (element) appear(element, batch.t[e[1]], e[2], e.slice(3, 10), e[10] === 1);
            }
        }

        if (batch.g) {
            for (const g of batch.g) vanish(g[0], batch.t[g[1]], g[2], g.slice(3, 10), g[10] === 1);
        }

        if (batch.x) {
            for (const id of batch.x) removeGhost(id);
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

    globalThis.osAnimations = Object.freeze({ init, run, hold, ghost });
})();
