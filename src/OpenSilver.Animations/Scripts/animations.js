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

    function animateBox(element, timing, delay, dx, dy, ow, oh, nw, nh, flags, fromGhost) {
        let fromX = dx, fromY = dy, fromW = ow, fromH = oh;

        // An item shown again in a new place goes on from where its old element was on screen.
        const ghost = fromGhost && ghosts.get(fromGhost);
        if (ghost) {
            fromX += ghost.x;
            fromY += ghost.y;
            removeGhost(fromGhost);
        }

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
        startAtNextFrame(animation.fade);
        effects.set(element, { animation: startAtNextFrame(animation), curve });
        animation.addEventListener('finish', () => {
            const running = effects.get(element);
            if (running && running.animation === animation) effects.delete(element);
        });
    }

    // Cancels the animation of an effect, with its fade when it fades on a curve of its own.
    function cancelEffect(animation) {
        if (animation.fade) animation.fade.cancel();
        animation.cancel();
    }

    // The curve of the opacity of an effect that bounces or overshoots, which fades without it.
    function fadeCurveOf(timing) {
        return timing.fe ? { easing: timing.fe, baked: null } : null;
    }

    // Whether a look changes nothing but the opacity.
    function onlyFades(look) {
        return look[1] === 0 && look[2] === 0 && look[3] === 1 && look[4] === 0 && look[5] === 0 &&
               look[6] === 0 && !look[7] && !look[8];
    }

    // Wipes or fades in an element that appears, with the size curve, so that the revealed part
    // keeps up with the elements it pushes.
    function reveal(element, timing, delay, edge) {
        const from = takeGhost(element);
        release(element);
        const previous = effects.get(element);
        if (previous) cancelEffect(previous.animation);

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
    // their top left corner. With a fadeCurve, the opacity plays on that curve, in a second
    // animation kept in the fade property of the first.
    function animateLook(target, look, curve, shownAt, base, options, raise, fadeCurve) {
        const o = look[0], s = look[3], r = look[4], b = look[5], w = look[6];
        const style = getComputedStyle(target);
        const opacity = base.opacity !== undefined ? base.opacity : ownOpacity(target);
        const filter = style.filter && style.filter !== 'none' ? style.filter + ' ' : '';
        const width = parseFloat(target.style.width) || target.offsetWidth || 0;
        const height = parseFloat(target.style.height) || target.offsetHeight || 0;
        const cx = width / 2, cy = height / 2;
        const x = look[1] + (look[7] || 0) * width, y = look[2] + (look[8] || 0) * height;
        const moves = x !== 0 || y !== 0 || s !== 1 || r !== 0;
        const fades = o !== 1 || base.opacity !== undefined;
        const fadeOnItsOwn = fadeCurve && o !== 1;
        let z = null;
        if (raise) {
            const own = parseInt(style.zIndex, 10);
            z = String(isNaN(own) || own < 1 ? 1 : own);
        }

        const opacityAt = (p) => opacity * (o + (1 - o) * shownAt(p));
        const make = (p) => {
            const v = shownAt(p), h = 1 - v;
            const frame = {};
            if (fades && !fadeOnItsOwn) frame.opacity = opacityAt(p);
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

        const animation = target.animate(sampledKeyframes(curve, make, r !== 0 ? 12 : 1), options);
        if (fadeOnItsOwn) {
            animation.fade = target.animate(lerpKeyframes(fadeCurve, (p) => ({ opacity: opacityAt(p) })),
                Object.assign({}, options, { easing: fadeCurve.easing }));
        }
        return animation;
    }

    // Plays the effect of an element that appears. If it disappeared just before and its ghost is
    // still there, it goes on from what the ghost showed.
    function appear(element, timing, delay, look, raise) {
        const from = takeGhost(element);
        release(element);
        const previous = effects.get(element);
        if (previous) cancelEffect(previous.animation);

        // An effect that only fades plays on the curve of the fade.
        const fadeCurve = fadeCurveOf(timing);
        const curve = fadeCurve && onlyFades(look) ? fadeCurve : curveOf(timing, false);
        const options = { duration: Math.max(1, timing.d), delay, fill: 'backwards', easing: curve.easing };
        const animation = animateLook(element, look, curve, (p) => from + (1 - from) * p, { x: 0, y: 0 }, options, raise,
            curve === fadeCurve ? null : fadeCurve);
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
    const FADE_LOOK = [0, 0, 0, 1, 0, 0, 0, 0, 0];
    const VIEW_MARGIN = 100;

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
            cancelEffect(running.animation);
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

    // Copies the containers of a list that is refreshed, those near the screen, before their
    // content is cleared. Reads where they all are before copying any.
    function ghostInView(ids) {
        const width = window.innerWidth, height = window.innerHeight;
        const near = [];
        for (const id of ids) {
            const element = document.getElementById(id);
            if (!element) continue;
            const r = element.getBoundingClientRect();
            if (r.bottom > -VIEW_MARGIN && r.top < height + VIEW_MARGIN && r.right > -VIEW_MARGIN && r.left < width + VIEW_MARGIN) {
                near.push(id);
            }
        }
        for (const id of near) ghost(id);
    }

    function removeGhost(id, entry) {
        const ghost = ghosts.get(id);
        if (!ghost || (entry && ghost !== entry)) return;
        ghosts.delete(id);
        clearTimeout(ghost.timer);
        if (ghost.animation) cancelEffect(ghost.animation);
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

        const fadeCurve = sizeCurve ? null : fadeCurveOf(timing);
        const curve = fadeCurve && onlyFades(look) ? fadeCurve : curveOf(timing, sizeCurve);
        const from = ghost.from;
        const options = { duration: Math.max(1, timing.d), delay, fill: 'both', easing: curve.easing };
        const animation = animateLook(ghost.clone, look, curve, (p) => from * (1 - p), { x: ghost.x, y: ghost.y, opacity: ghost.opacity }, options, false,
            curve === fadeCurve ? null : fadeCurve);
        startAtNextFrame(animation.fade);
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
                animateBox(element, batch.t[m[1]], m[2], m[3], m[4], m[5], m[6], m[7], m[8], m[9], m[10]);
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
                if (element) appear(element, batch.t[e[1]], e[2], e.slice(3, 12), e[12] === 1);
            }
        }

        // After the effects, so that the move adds to the move of their zoom or slide.
        if (batch.k) {
            for (const k of batch.k) {
                const element = document.getElementById(k[0]);
                if (element) animateBox(element, batch.t[k[1]], k[2], k[3], k[4], 0, 0, 0, 0, 0);
            }
        }

        if (batch.g) {
            for (const g of batch.g) vanish(g[0], batch.t[g[1]], g[2], g.slice(3, 12), g[12] === 1);
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

    // Hover and press reactions, per element: { hover, press, enabled, hovered, levels, nudge }.
    // hover and press are what the element does: { k: kind, t: timing, b: bounciness, bo: 1 when
    // bouncy, dist, a: tilt, z: zoom }. levels has one entry for the hover and one for the press:
    // { from, to, animation, curve }, where 0 is the element at rest and 1 is the reaction. They
    // play on top of everything else, like moves, so that the element keeps its place.
    const reactions = new WeakMap();
    const HOVER = 0, PRESS = 1;
    const KIND_NUDGE = 1, KIND_ZOOM = 2, KIND_LIFT = 3;

    // The elements whose reaction shows or plays, the elements pressed until the pointer is
    // released, and the elements the pointer left that it is still over without their reaction.
    const reacting = new Set();
    const pressed = new Set();
    const lingering = new Set();

    let reactionSettings = { on: true, speed: 1, defaultMs: 250, respect: true, simulate: false };
    let prefersReducedMotion = false;

    // The last positions of the mouse or pen, to know where it goes, and how fast, when it comes
    // over an element.
    const trail = [];

    function reactionsAllowed() {
        const s = reactionSettings;
        return s.on && !(s.respect && (prefersReducedMotion || s.simulate));
    }

    function reactionMs(spec) {
        const d = spec.t.d || (spec.k === KIND_NUDGE ? 400 : reactionSettings.defaultMs * (spec.bo ? 2 : 1));
        return Math.max(1, d / reactionSettings.speed);
    }

    // The point the element turns and zooms around, from its transform origin: its center.
    function pivotOf(element) {
        const origin = (getComputedStyle(element).transformOrigin || '').split(' ');
        return {
            x: (element.offsetWidth || 0) / 2 - (parseFloat(origin[0]) || 0),
            y: (element.offsetHeight || 0) / 2 - (parseFloat(origin[1]) || 0),
        };
    }

    function levelFrame(spec, pivot, v) {
        if (spec.k === KIND_LIFT) return { translate: '0px ' + px(-spec.dist * v) };
        const s = 1 + (spec.z - 1) * v;
        return { translate: px((1 - s) * pivot.x) + ' ' + px((1 - s) * pivot.y), scale: String(Math.round(s * 10000) / 10000) };
    }

    function levelOf(level) {
        if (!level) return 0;
        if (!level.animation) return level.to;
        return level.from + (level.to - level.from) * progressOf(level.animation, level.curve);
    }

    function isAtRest(r) {
        return !r.levels[HOVER] && !r.levels[PRESS] && !(r.nudge && r.nudge.playState === 'running');
    }

    // Plays the hover or the press from where it is to the target level.
    function setLevel(element, r, slot, target) {
        const spec = slot === HOVER ? r.hover : r.press;
        const level = r.levels[slot];
        if (level ? level.to === target : target === 0) return;

        const from = levelOf(level);
        if (level) level.animation.cancel();
        r.levels[slot] = null;
        if (!spec || spec.k === KIND_NUDGE || (target === 0 && Math.abs(from) < 0.001)) {
            if (isAtRest(r)) reacting.delete(element);
            return;
        }

        const curve = curveOf(spec.t, false);
        const pivot = pivotOf(element);
        const animation = element.animate(
            lerpKeyframes(curve, (p) => levelFrame(spec, pivot, from + (target - from) * p)),
            { duration: reactionMs(spec), easing: curve.easing, fill: target > 0 ? 'forwards' : 'none', composite: 'add' });
        const entry = { from, to: target, animation, curve };
        r.levels[slot] = entry;
        reacting.add(element);
        // Otherwise the browser removes a hover that is held once a press on the same
        // properties ends, although they add up.
        if (target > 0 && animation.persist) animation.persist();
        if (target === 0) {
            animation.addEventListener('finish', () => {
                if (r.levels[slot] !== entry) return;
                r.levels[slot] = null;
                if (isAtRest(r)) reacting.delete(element);
            });
        }
    }

    // Brings the element back at rest at once.
    function stopReaction(element, r) {
        for (const slot of [HOVER, PRESS]) {
            if (r.levels[slot]) r.levels[slot].animation.cancel();
            r.levels[slot] = null;
        }
        if (r.nudge) r.nudge.cancel();
        r.nudge = null;
        reacting.delete(element);
        pressed.delete(element);
        lingering.delete(element);
    }

    function trackPointer(e) {
        if (e.pointerType === 'touch') return;
        trail.push({ x: e.clientX, y: e.clientY, t: e.timeStamp });
        if (trail.length > 8) trail.shift();
        if (lingering.size > 0) {
            for (const element of lingering) {
                const r = reactions.get(element);
                if (!r || !element.isConnected || !isOverRestBox(element, r, e)) {
                    lingering.delete(element);
                    if (r) setLevel(element, r, HOVER, 0);
                }
            }
        }
    }

    // The pointer left the window.
    function onPointerOut(e) {
        if (e.relatedTarget || lingering.size === 0) return;
        for (const element of lingering) {
            const r = reactions.get(element);
            if (r) setLevel(element, r, HOVER, 0);
        }
        lingering.clear();
    }

    // How fast the pointer comes, in pixels per millisecond, from where it was 20 to 150
    // milliseconds before the event.
    function pointerVelocity(e) {
        for (let i = trail.length - 1; i >= 0; i--) {
            const p = trail[i], dt = e.timeStamp - p.t;
            if (dt > 150) break;
            if (dt >= 20) return { x: (e.clientX - p.x) / dt, y: (e.clientY - p.y) / dt };
        }
        return null;
    }

    // Whether the pointer is over where the element is without its hover, so that an element
    // that rises or shrinks away from the pointer doesn't come back under it, again and again.
    function isOverRestBox(element, r, e) {
        const rect = element.getBoundingClientRect();
        let left = rect.left, top = rect.top, right = rect.right, bottom = rect.bottom;
        const spec = r.hover, v = levelOf(r.levels[HOVER]);
        if (spec && v !== 0) {
            if (spec.k === KIND_LIFT) {
                top += spec.dist * v;
                bottom += spec.dist * v;
            } else if (spec.k === KIND_ZOOM) {
                const s = 1 + (spec.z - 1) * v;
                const cx = (left + right) / 2, cy = (top + bottom) / 2, hw = (right - left) / 2 / s, hh = (bottom - top) / 2 / s;
                left = cx - hw; right = cx + hw; top = cy - hh; bottom = cy + hh;
            }
        }
        return e.clientX >= left && e.clientX < right && e.clientY >= top && e.clientY < bottom;
    }

    // Pushes the element in the direction the pointer comes from, turns it like a lever around
    // its center, from where the pointer comes in, and springs it back. The faster the pointer,
    // the more it moves.
    function nudge(element, r, e) {
        const spec = r.hover;
        if ((r.nudge && r.nudge.playState === 'running') || e.buttons) return;
        const v = pointerVelocity(e);
        const speed = v ? Math.hypot(v.x, v.y) : 0;
        if (speed < 0.05) return;

        const k = Math.min(speed, 1), dirX = v.x / speed, dirY = v.y / speed;
        const rect = element.getBoundingClientRect();
        let ex = e.clientX - (rect.left + rect.right) / 2, ey = e.clientY - (rect.top + rect.bottom) / 2;
        const length = Math.hypot(ex, ey);
        ex = length > 0.001 ? ex / length : 0;
        ey = length > 0.001 ? ey / length : 0;
        const angle = (ex * dirY - ey * dirX) * spec.a * k;
        const dx = dirX * k * spec.dist, dy = dirY * k * spec.dist;
        const pivot = pivotOf(element);
        const back = 1.6 * spec.b;

        // Out in 30% of the time, then back, past the place where it rests, and back to it.
        const make = (t) => {
            let f;
            if (t < 0.3) {
                const u = t / 0.3;
                f = u * (2 - u);
            } else {
                const u = 1 - (t - 0.3) / 0.7;
                f = u * u * u - u * back * Math.sin(Math.PI * u);
            }
            const deg = angle * f, rad = deg * Math.PI / 180, cos = Math.cos(rad), sin = Math.sin(rad);
            return {
                translate: px(dx * f + pivot.x - (cos * pivot.x - sin * pivot.y)) + ' ' +
                           px(dy * f + pivot.y - (sin * pivot.x + cos * pivot.y)),
                rotate: (Math.round(deg * 100) / 100) + 'deg',
            };
        };

        const frames = [];
        for (let i = 0; i <= 24; i++) {
            const frame = make(i / 24);
            frame.offset = i / 24;
            frames.push(frame);
        }

        const animation = element.animate(frames, { duration: reactionMs(spec), composite: 'add' });
        r.nudge = animation;
        reacting.add(element);
        animation.addEventListener('finish', () => {
            if (r.nudge === animation && isAtRest(r)) reacting.delete(element);
        });
    }

    function onReactionEnter(e) {
        const element = e.currentTarget;
        const r = reactions.get(element);
        if (!r || e.pointerType === 'touch') return;
        r.hovered = true;
        lingering.delete(element);
        if (!r.hover || !r.enabled || !reactionsAllowed()) return;
        if (r.hover.k === KIND_NUDGE) nudge(element, r, e);
        else setLevel(element, r, HOVER, 1);
    }

    function onReactionLeave(e) {
        const element = e.currentTarget;
        const r = reactions.get(element);
        if (!r || e.pointerType === 'touch') return;
        r.hovered = false;
        if (r.levels[HOVER] && r.levels[HOVER].to === 1 && isOverRestBox(element, r, e)) lingering.add(element);
        else setLevel(element, r, HOVER, 0);
    }

    // Presses the element under the pointer, and the elements it is in that react when pressed.
    function pressAt(e) {
        if (!reactionsAllowed()) return;
        for (let node = e.target; node && node.nodeType === 1; node = node.parentNode) {
            const r = reactions.get(node);
            if (r && r.press && r.enabled) {
                pressed.add(node);
                setLevel(node, r, PRESS, 1);
            }
        }
    }

    function releasePressed() {
        if (pressed.size === 0) return;
        for (const element of pressed) {
            const r = reactions.get(element);
            if (r) setLevel(element, r, PRESS, 0);
        }
        pressed.clear();
    }

    // Sets what the element does when the pointer is over it, and when it is pressed. Called
    // again when the element is enabled or disabled, and each time it is shown in a page.
    function react(id, hover, press, enabled) {
        for (const element of reacting) {
            if (!element.isConnected) {
                reacting.delete(element);
                pressed.delete(element);
                lingering.delete(element);
            }
        }

        const element = document.getElementById(id);
        if (!element) return;
        let r = reactions.get(element);
        if (!hover && !press) {
            if (r) {
                stopReaction(element, r);
                element.removeEventListener('pointerenter', onReactionEnter);
                element.removeEventListener('pointerleave', onReactionLeave);
                reactions.delete(element);
            }
            return;
        }

        if (!r) {
            r = { hover: null, press: null, enabled: true, hovered: false, levels: [null, null], nudge: null };
            reactions.set(element, r);
            element.addEventListener('pointerenter', onReactionEnter);
            element.addEventListener('pointerleave', onReactionLeave);
        }

        const same = (a, b) => JSON.stringify(a) === JSON.stringify(b);
        if (!same(hover, r.hover)) {
            if (r.levels[HOVER]) r.levels[HOVER].animation.cancel();
            r.levels[HOVER] = null;
            if (r.nudge) r.nudge.cancel();
            r.nudge = null;
            lingering.delete(element);
            r.hover = hover;
        }
        if (!same(press, r.press)) {
            if (r.levels[PRESS]) r.levels[PRESS].animation.cancel();
            r.levels[PRESS] = null;
            r.press = press;
            if (!press) pressed.delete(element);
        }
        if (isAtRest(r)) reacting.delete(element);

        r.enabled = !!enabled;
        const on = r.enabled && reactionsAllowed();
        setLevel(element, r, HOVER, on && r.hovered ? 1 : 0);
        setLevel(element, r, PRESS, on && pressed.has(element) ? 1 : 0);
        if (!on) pressed.delete(element);
    }

    function reactSettings(on, speed, defaultMs, respect, simulate) {
        reactionSettings = { on: !!on, speed: speed > 0 ? speed : 1, defaultMs: defaultMs > 0 ? defaultMs : 250, respect: !!respect, simulate: !!simulate };
        if (!reactionsAllowed()) stopAllReactions();
    }

    function stopAllReactions() {
        for (const element of [...reacting]) {
            const r = reactions.get(element);
            if (r) stopReaction(element, r);
        }
        reacting.clear();
        pressed.clear();
        lingering.clear();
    }

    // Dragging: a pointer button is held and the pointer moved by more than 4 pixels.
    let dragStarted = null, dragEnded = null, pointerDown = null, dragging = false;

    function onPointerDown(e) {
        if (e.pointerType === 'mouse' && e.button !== 0) return;
        pointerDown = { x: e.clientX, y: e.clientY };
        pressAt(e);
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
        releasePressed();
        if (dragging) {
            dragging = false;
            if (dragEnded) dragEnded();
        }
    }

    function init(onDragStarted, onDragEnded, onReducedMotionOn, onReducedMotionOff) {
        dragStarted = onDragStarted;
        dragEnded = onDragEnded;
        window.addEventListener('pointerdown', onPointerDown, true);
        window.addEventListener('pointermove', trackPointer, true);
        window.addEventListener('pointermove', onPointerMove, true);
        window.addEventListener('pointerup', onPointerUp, true);
        window.addEventListener('pointercancel', onPointerUp, true);
        window.addEventListener('pointerout', onPointerOut, true);

        const query = window.matchMedia ? window.matchMedia('(prefers-reduced-motion: reduce)') : null;
        if (query) {
            const notify = (matches) => {
                prefersReducedMotion = matches;
                if (matches) {
                    stopAllReactions();
                    onReducedMotionOn();
                } else {
                    onReducedMotionOff();
                }
            };
            if (query.addEventListener) query.addEventListener('change', e => notify(e.matches));
            notify(query.matches);
        }
    }

    globalThis.osAnimations = Object.freeze({ init, run, hold, ghost, ghostInView, react, reactSettings });
})();
