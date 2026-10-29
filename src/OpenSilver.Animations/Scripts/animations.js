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
    // still there, it goes on from what the ghost showed. text is [kind, letterMs, cursorMs,
    // blinkFirst] for the letters of a TextBlock.
    function appear(element, timing, delay, look, raise, text) {
        const from = takeGhost(element);
        release(element);
        const previous = effects.get(element);
        if (previous) cancelEffect(previous.animation);
        if (text) {
            appearText(element, timing, delay, text);
            return;
        }

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

    // A copy of an element that nobody can click, without ids. The elements inside it that travel
    // (Magic move) are marked with their id, to hide them once their travel starts.
    function copyOf(element) {
        const clone = element.cloneNode(true);
        copyShadowRoots(element, clone);
        for (const e of clone.querySelectorAll('[data-os-magic-src]')) {
            e.setAttribute('data-os-magic', e.id);
            e.removeAttribute('data-os-magic-src');
        }
        clone.removeAttribute('data-os-magic-src');
        clone.removeAttribute('id');
        renameSvgIds(clone);
        for (const e of clone.querySelectorAll('[id]')) {
            if (!e.ownerSVGElement) e.removeAttribute('id');
        }
        removeTextOverlays(clone);
        clone.classList.remove('uielement-collapsed', 'uielement-hidden');
        clone.setAttribute('data-os-foreign', '');
        clone.style.pointerEvents = 'none';
        copyFormState(element, clone);
        return clone;
    }

    // cloneNode leaves out shadow roots, like the one of an HtmlPresenter with UseShadowDom. It must
    // run before the copy is changed: until then, its elements are in the same order as the original's.
    function copyShadowRoots(from, to) {
        if (from.shadowRoot) copyShadowRoot(from.shadowRoot, to);
        const originals = from.querySelectorAll('*');
        let copies = null;
        for (let i = 0; i < originals.length; i++) {
            const root = originals[i].shadowRoot;
            if (!root) continue;
            copies = copies || to.querySelectorAll('*');
            copyShadowRoot(root, copies[i]);
        }
    }

    // A shadow root with media stays empty, like media in the copy (see MEDIA).
    function copyShadowRoot(root, host) {
        if (!host || host.shadowRoot || root.querySelector(MEDIA)) return;
        let shadow;
        try { shadow = host.attachShadow({ mode: 'open' }); } catch { return; }
        for (const node of root.childNodes) {
            const copy = node.cloneNode(true);
            shadow.appendChild(copy);
            if (node.nodeType === 1) copyShadowRoots(node, copy);
        }
    }

    // A shape paints with a gradient it refers to by id, and the browser paints nothing with the
    // original's once that one is hidden or gone: the copy keeps its own, under new ids.
    let copiedIds = 0;
    const URL_REFERENCE = /url\(\s*(['"]?)#([^'")]+)\1\s*\)/g;

    function renameSvgIds(copy) {
        const inside = copy.querySelectorAll('svg [id]');
        if (inside.length === 0) return;
        const suffix = '-os-copy-' + (++copiedIds);
        const names = new Set();
        for (const e of inside) {
            names.add(e.id);
            e.id += suffix;
        }
        const rename = (match, quote, id) => (names.has(id) ? 'url(#' + id + suffix + ')' : match);
        for (const e of copy.querySelectorAll('svg *, [style*="url("]')) {
            for (const a of e.attributes) {
                if (a.value.includes('url(')) {
                    a.value = a.value.replace(URL_REFERENCE, rename);
                } else if ((a.name === 'href' || a.name === 'xlink:href') && a.value[0] === '#' && names.has(a.value.slice(1))) {
                    a.value += suffix;
                }
            }
        }
    }

    // Copies an element that disappears, before the browser hides or removes it, at the same place
    // among its siblings, so that it stacks the same way. The copy starts from where the element is
    // on screen, so that an element that disappears while it appears or moves doesn't jump. It
    // stays until a batch plays its effect or removes it, for at most 1.5 seconds.
    function ghost(id) {
        const element = document.getElementById(id);
        const parent = element && element.parentNode;
        if (!parent) return;

        // It disappears while it travels to its place: the layer stops where it is, and is its copy.
        const travel = travels.get(element);
        if (travel) {
            removeGhost(id);
            const layer = freezeTravel(element, travel);
            const entry = { clone: layer, from: 1, x: 0, y: 0, opacity: 1, media: null, animation: null, curve: null, timer: 0 };
            entry.timer = setTimeout(() => removeGhost(id, entry), 1500);
            ghosts.set(id, entry);
            return;
        }

        if (holds.has(element) || element.matches(MEDIA)) return;
        removeGhost(id);
        endText(element);

        let from = 1;
        const running = effects.get(element);
        if (running) {
            from = Math.max(0, Math.min(1, progressOf(running.animation, running.curve)));
            cancelEffect(running.animation);
            effects.delete(element);
        }
        const opacity = ownOpacity(element);

        const clone = copyOf(element);
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

        if (x !== 0 || y !== 0) clone.style.translate = px(x) + ' ' + px(y);
        if (from < 1) clone.style.opacity = String(opacity * from);
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
    function vanish(id, timing, delay, look, sizeCurve, text) {
        const ghost = ghosts.get(id);
        if (!ghost) return;
        clearTimeout(ghost.timer);
        if (ghost.media) {
            timing = QUICK_FADE;
            look = FADE_LOOK;
            sizeCurve = false;
        } else if (text) {
            vanishText(id, ghost, timing, delay, text);
            return;
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

    // Magic move. The copies of the elements with an id inside a container that disappears, per
    // id, until an element with their id appears, for at most 1.5 seconds: { clone, rect, opacity,
    // look, element, timer }, rect being where the element was on screen, and look what lookOf
    // says of it. The element stays marked, so that the ghost of its container hides it once it
    // travels. And the travels in flight, per element they go to: { p, layer, container, from,
    // fromText, fromContent, oldLook, oldOpacity, newOpacity, animations, hide, timer, group },
    // group being the travels of its batch it waits with, before it leaves.
    const captures = new Map();
    const travels = new WeakMap();
    const TRAVEL_SIZE_WAIT = 300;
    // The two copies of a travel add up instead of covering each other, where the browser can: the
    // same pixel in both stays as it is, so a color changes like a color animation, and a text that
    // fades into itself doesn't look bolder halfway.
    const PLUS_LIGHTER = typeof CSS !== 'undefined' && CSS.supports && CSS.supports('mix-blend-mode', 'plus-lighter');
    const TRAVEL_BEHIND = 1;
    const TRAVEL_FROM_COPY = 1, TRAVEL_OLD_TEXT = 2, TRAVEL_NEW_TEXT = 4;
    const LAYER_STYLE = 'position:absolute;left:0;top:0;margin:0;padding:0;border:0;box-sizing:border-box;' +
                        'overflow:visible;pointer-events:none;z-index:0;';
    // What a copy inherits where its element is, and loses in the layer.
    const INHERITED = ['color', 'fontFamily', 'fontSize', 'fontStyle', 'fontWeight', 'fontStretch', 'lineHeight',
                       'letterSpacing', 'textAlign', 'whiteSpace', 'direction'];
    let travelOverlay = null;

    // Copies the elements with an id inside a container that disappears, with where they are on
    // screen, before the browser hides or removes it. A collapsed container is shown while they
    // are measured, which the browser never paints.
    function capture(containerId, ids) {
        const container = document.getElementById(containerId);
        const collapsed = container && container.classList.contains('uielement-collapsed');
        if (collapsed) container.classList.remove('uielement-collapsed');
        try {
            for (const id of ids) captureOne(id);
        } finally {
            if (collapsed) container.classList.add('uielement-collapsed');
        }
    }

    function captureOne(id) {
        const element = document.getElementById(id);
        if (!element) return;
        removeCapture(id);

        let clone, rect, look, opacity = 1;
        const travel = travels.get(element);
        if (travel) {
            clone = freezeTravel(element, travel);
            rect = clone.getBoundingClientRect();
            look = lookOf(clone);
            clone.remove();
        } else {
            endText(element);
            rect = element.getBoundingClientRect();
            look = lookOf(element);
            opacity = ownOpacity(element);
            clone = copyOf(element);
            keepInherited(element, clone);
            replaceMedia(element, clone);
        }

        element.setAttribute('data-os-magic-src', '');
        const entry = { clone, rect, opacity, look, element, timer: 0 };
        entry.timer = setTimeout(() => removeCapture(id, entry), 1500);
        captures.set(id, entry);
    }

    function removeCapture(id, entry) {
        const c = captures.get(id);
        if (!c || (entry && c !== entry)) return;
        captures.delete(id);
        clearTimeout(c.timer);
        c.element.removeAttribute('data-os-magic-src');
    }

    function keepInherited(element, copy) {
        const style = getComputedStyle(element);
        for (const name of INHERITED) {
            if (!copy.style[name]) copy.style[name] = style[name];
        }
    }

    // Above the page, out of every container that cuts off its content.
    function travelOverlayOf() {
        if (!travelOverlay || !travelOverlay.isConnected) {
            travelOverlay = document.createElement('div');
            travelOverlay.setAttribute('data-os-foreign', '');
            travelOverlay.style.cssText = 'position:fixed;left:0;top:0;width:0;height:0;overflow:visible;pointer-events:none;z-index:2147483000;';
            document.body.appendChild(travelOverlay);
        }
        return travelOverlay;
    }

    // A box on screen, in a container: in the overlay, the screen itself; otherwise, the padding
    // box of the container, without its scale on screen.
    function boxIn(container, rect, containerRect) {
        if (container === travelOverlay) return { x: rect.left, y: rect.top, w: rect.width, h: rect.height };
        const c = containerRect || container.getBoundingClientRect();
        const kx = container.offsetWidth > 0 && c.width > 0 ? c.width / container.offsetWidth : 1;
        const ky = container.offsetHeight > 0 && c.height > 0 ? c.height / container.offsetHeight : 1;
        return {
            x: (rect.left - c.left) / kx - container.clientLeft,
            y: (rect.top - c.top) / ky - container.clientTop,
            w: rect.width / kx,
            h: rect.height / ky,
        };
    }

    // Stops a travel where it is, because the element it goes to disappears again: the layer
    // stays, as the copy of that element, to travel on from there or to disappear.
    function freezeTravel(element, travel) {
        travels.delete(element);
        stopWaiting(travel);
        const layer = travel.layer;
        const rect = layer.getBoundingClientRect();
        for (const animation of travel.animations) {
            try { animation.commitStyles(); } catch { }
            animation.cancel();
        }
        travel.hide.cancel();

        layer.style.translate = '';
        placeLayer(layer, boxIn(travel.container, rect));
        return layer;
    }

    function endTravel(element, travel) {
        if (travels.get(element) === travel) travels.delete(element);
        stopWaiting(travel);
        travel.layer.remove();
        travel.hide.cancel();
    }

    // A copy fills the layer, or, for a text, keeps its own size and is zoomed with its letters
    // (textFrames), so that they don't wrap differently on the way.
    function fitCopy(copy, ownSize) {
        copy.style.left = '0px';
        copy.style.top = '0px';
        copy.style.translate = '';
        copy.style.margin = '0px';
        if (PLUS_LIGHTER) copy.style.mixBlendMode = 'plus-lighter';
        if (ownSize) {
            copy.style.width = px(ownSize.w);
            copy.style.height = px(ownSize.h);
            copy.style.transformOrigin = '0px 0px';
        } else {
            copy.style.width = '100%';
            copy.style.height = '100%';
        }
    }

    // Before the batch moves anything: where each element that travels goes, and the copy it
    // travels from, taken out of the ghosts so that the rest of the batch leaves it alone.
    function prepareTravels(list, timings) {
        const prepared = [];
        for (const v of list) {
            const fromCopy = (v[5] & TRAVEL_FROM_COPY) !== 0;
            const source = fromCopy ? captures.get(v[0]) : ghosts.get(v[0]);
            if (!source) continue;
            if (fromCopy) {
                removeCapture(v[0]);
            } else {
                ghosts.delete(v[0]);
                clearTimeout(source.timer);
                if (source.animation) cancelEffect(source.animation);
            }

            const to = document.getElementById(v[1]);
            if (!to) {
                if (!fromCopy) source.clone.remove();
                continue;
            }

            const previous = travels.get(to);
            if (previous) endTravel(to, previous);
            const ancestor = v[3] === TRAVEL_BEHIND ? document.getElementById(v[4]) : null;
            prepared.push({
                v, timing: timings[v[2]], source, fromCopy, to, end: to.getBoundingClientRect(), endLook: lookOf(to),
                ancestor, ancestorEnd: ancestor ? ancestor.getBoundingClientRect() : null,
            });
        }
        return prepared;
    }

    // After the batch: for each element that travels, a layer with a copy of the old element
    // under a copy of the new one moves and changes size from where the old one is on screen to
    // the new one's place, while the copies cross-fade. The new elements stay hidden until their
    // layer arrives. When one of them has no size yet, like an image that is still loading, they
    // all wait for it, for at most TRAVEL_SIZE_WAIT, with the old copies where they were, so
    // that they leave together.
    function startTravels(trips) {
        const started = [];
        for (const p of trips) {
            const travel = startTravel(p);
            if (travel) started.push(travel);
        }
        if (started.every((travel) => hasSize(travel.p.end))) {
            for (const travel of started) fly(travel, travel.p.end, travel.p.ancestor ? travel.p.ancestorEnd : null, travel.p.endLook);
            return;
        }

        const group = { travels: new Set(started), observer: null, timer: 0 };
        const leave = (timedOut) => {
            const waiting = [...group.travels];
            if (!timedOut && !waiting.every((travel) => hasSize(travel.p.to.getBoundingClientRect()))) return;
            endWait(group);
            for (const travel of waiting) {
                travel.group = null;
                const end = travel.p.to.getBoundingClientRect();
                if (hasSize(end)) fly(travel, end, null, lookOf(travel.p.to));
                else endTravel(travel.p.to, travel);
            }
        };
        group.observer = new ResizeObserver(() => leave(false));
        for (const travel of started) {
            travel.group = group;
            group.observer.observe(travel.p.to);
        }
        group.timer = setTimeout(() => leave(true), TRAVEL_SIZE_WAIT);
    }

    function hasSize(rect) {
        return rect.width > 0 && rect.height > 0;
    }

    function endWait(group) {
        group.observer.disconnect();
        clearTimeout(group.timer);
    }

    // The layer of an element that travels, with the copy of the old element where it is.
    function startTravel(p) {
        const { v, source, fromCopy, to } = p;
        const oldCopy = source.clone;
        if (!fromCopy && !oldCopy.isConnected) return null;

        const start = fromCopy ? source.rect : oldCopy.getBoundingClientRect();
        const oldOpacity = fromCopy ? source.opacity : ownOpacity(oldCopy);
        const oldLook = fromCopy ? source.look : lookOf(oldCopy);
        if (!fromCopy) {
            keepInherited(oldCopy, oldCopy);
            oldCopy.remove();
        }
        const newOpacity = ownOpacity(to);

        const ancestor = p.ancestor && p.ancestor.isConnected ? p.ancestor : null;
        const container = ancestor || travelOverlayOf();
        const from = boxIn(container, start);
        const fromText = oldLook.text ? boxIn(container, oldLook.text) : null;
        const fromContent = oldLook.content ? boxIn(container, oldLook.content) : null;

        const layer = document.createElement('div');
        layer.setAttribute('data-os-foreign', '');
        layer.setAttribute('data-os-layer', '');
        layer.style.cssText = LAYER_STYLE;
        if (PLUS_LIGHTER) layer.style.isolation = 'isolate';
        placeLayer(layer, from);
        if ((v[5] & TRAVEL_OLD_TEXT) !== 0) {
            fitCopy(oldCopy, oldLook);
            oldCopy.style.scale = (from.w / oldLook.w) + ' ' + (from.h / oldLook.h);
        } else {
            fitCopy(oldCopy, null);
        }
        layer.appendChild(oldCopy);
        if (ancestor) ancestor.insertBefore(layer, ancestor.firstChild);
        else container.appendChild(layer);

        // Its copy in the ghost of the container it was in, which may be fading out.
        if (fromCopy) {
            for (const e of document.querySelectorAll('[data-os-magic="' + v[0] + '"]')) e.style.visibility = 'hidden';
        }

        // Hidden before its start too: it starts at the next frame, and the one painted before would show it.
        const hide = startAtNextFrame(to.animate([{ opacity: 0 }, { opacity: 0 }], { duration: 1, fill: 'both' }));
        const travel = { p, layer, container, from, fromText, fromContent, oldLook, oldOpacity, newOpacity, animations: [], hide, timer: 0, group: null };
        travels.set(to, travel);
        return travel;
    }

    // A travel that stops before it leaves no longer holds back the others of its batch.
    function stopWaiting(travel) {
        clearTimeout(travel.timer);
        const group = travel.group;
        if (!group) return;
        travel.group = null;
        group.travels.delete(travel);
        if (group.travels.size === 0) endWait(group);
    }

    function placeLayer(layer, box) {
        layer.style.left = px(box.x);
        layer.style.top = px(box.y);
        layer.style.width = px(box.w);
        layer.style.height = px(box.h);
    }

    // What a travel needs to know of an element, besides where it is on screen: its own size,
    // without its scale on screen, its corners and borders, for a text, where its letters are on
    // screen and how they are aligned, and for a box, where what is inside it is on screen, with
    // the size of its largest letters. A stopped layer looks like the copy on top in it.
    function lookOf(element) {
        const layer = element.hasAttribute('data-os-layer');
        const box = layer && element.lastElementChild ? element.lastElementChild : element;
        const style = getComputedStyle(box);
        const isText = !layer && element.classList.contains('opensilver-textblock');
        const content = layer || isText ? null : contentOf(element);
        const rect = element.getBoundingClientRect();
        return {
            w: element.offsetWidth || rect.width || 1,
            h: element.offsetHeight || rect.height || 1,
            radii: [style.borderTopLeftRadius, style.borderTopRightRadius, style.borderBottomRightRadius, style.borderBottomLeftRadius]
                .map((r) => parseFloat(r) || 0),
            borders: [style.borderTopWidth, style.borderRightWidth, style.borderBottomWidth, style.borderLeftWidth]
                .map((b) => parseFloat(b) || 0),
            text: isText ? textRectOf(element) : null,
            align: isText ? alignOf(style.textAlign) : 0,
            content: content ? content.rect : null,
            font: content ? content.font : 0,
        };
    }

    const MAX_MEASURED = 50;

    // What shows inside a box: its letters, pictures and shapes, rather than the panels that hold
    // them, which often fill the box, and the shapes of its frame. Its elements otherwise.
    function contentOf(element) {
        let left = Infinity, top = Infinity, right = -Infinity, bottom = -Infinity;
        const add = (r) => {
            if (!r || !hasSize(r)) return;
            left = Math.min(left, r.left);
            top = Math.min(top, r.top);
            right = Math.max(right, r.right);
            bottom = Math.max(bottom, r.bottom);
        };
        const texts = element.querySelectorAll('.opensilver-textblock');
        for (let i = 0; i < texts.length && i < MAX_MEASURED; i++) add(textRectOf(texts[i]));
        const box = element.getBoundingClientRect();
        const media = element.querySelectorAll('img, video, canvas, svg');
        for (let i = 0; i < media.length && i < MAX_MEASURED; i++) {
            const r = media[i].getBoundingClientRect();
            if (!isPlainShape(media[i]) || !fills(r, box, FRAME_GAP)) add(r);
        }
        if (left === Infinity) {
            for (const child of element.children) {
                if (child.classList.contains('opensilver-uielement')) add(child.getBoundingClientRect());
            }
        }
        if (left === Infinity) return null;
        // The fonts are on the runs, not on their TextBlock.
        let font = 0;
        const runs = element.querySelectorAll('.opensilver-inline');
        for (let i = 0; i < runs.length && i < MAX_MEASURED; i++) {
            font = Math.max(font, parseFloat(getComputedStyle(runs[i]).fontSize) || 0);
        }
        return { rect: new DOMRect(left, top, right - left, bottom - top), font };
    }

    function textRectOf(element) {
        const range = document.createRange();
        range.selectNodeContents(element);
        const rect = range.getBoundingClientRect();
        return hasSize(rect) ? rect : null;
    }

    // Where a line of text is held: 0 at its left, 0.5 in its middle, 1 at its right.
    function alignOf(textAlign) {
        if (textAlign === 'center' || textAlign === '-webkit-center') return 0.5;
        if (textAlign === 'right' || textAlign === 'end' || textAlign === '-webkit-right') return 1;
        return 0;
    }

    // The corners a box shows: the browser makes them all smaller when two of them don't fit
    // along a side, like the 999 of a round button.
    function cornersOf(look) {
        const [tl, tr, br, bl] = look.radii;
        const f = Math.min(1, look.w / (tl + tr), look.w / (bl + br), look.h / (tl + bl), look.h / (tr + br));
        return look.radii.map((r) => r * f);
    }

    // How much what is inside a box zooms, without letters to tell: as much as its side that
    // changes the least, so that it grows or shrinks the same way in both directions, and not at
    // all when it gets wider and shorter.
    function zoomOf(rx, ry) {
        if (rx >= 1 && ry >= 1) return Math.min(rx, ry);
        if (rx <= 1 && ry <= 1) return Math.max(rx, ry);
        return 1;
    }

    function centered(inner, innerSize, outer, outerSize) {
        return Math.abs(inner + innerSize / 2 - (outer + outerSize / 2)) < 1;
    }

    // Where what is inside a box is held along one axis, from 0 at the start of the box to 1 at
    // its end: when it is centered in both boxes, on the part of the box that moves the least,
    // like the middle of a badge that grows or the top of a card that opens below a field. At
    // the start of the box otherwise.
    function anchorOf(c0, cw0, b0, bw0, c1, cw1, b1, bw1) {
        if (!centered(c0, cw0, b0, bw0) || !centered(c1, cw1, b1, bw1)) return 0;
        const start = Math.abs(b1 - b0), end = Math.abs(b1 + bw1 - b0 - bw0);
        if (Math.abs(b1 + bw1 / 2 - b0 - bw0 / 2) <= Math.min(start, end) + 0.5) return 0.5;
        return start <= end ? 0 : 1;
    }

    // The frame of a box: the panels and the plain shapes that fill it, within FRAME_GAP of its
    // sides, like the Border in the template of a control. In a copy, they change their size with
    // the box, while what is inside them travels as one picture. A panel that holds anything else
    // than elements, like a text or an image, is part of the picture.
    const FRAME_GAP = 4;
    const WRAPPER_STYLE = 'position:absolute;left:0;top:0;margin:0;padding:0;border:0;pointer-events:none;';
    const CLIP_STYLE = 'position:absolute;left:0;top:0;right:0;bottom:0;margin:0;padding:0;border:0;' +
                       'overflow:hidden;border-radius:inherit;pointer-events:none;';

    function fills(r, box, gap) {
        const insets = [r.left - box.left, r.top - box.top, box.right - r.right, box.bottom - r.bottom];
        return insets.every((d) => d > -1 && d <= gap);
    }

    function isPlainShape(e) {
        if (e.tagName !== 'svg') return false;
        let shapes = 0;
        for (const child of e.children) {
            if (child.tagName === 'rect' || child.tagName === 'ellipse') shapes++;
            else if (child.tagName !== 'defs') return false;
        }
        return shapes > 0;
    }

    function isPanel(e) {
        if (e.tagName !== 'DIV' || e.classList.contains('opensilver-textblock')) return false;
        for (const node of e.childNodes) {
            if (node.nodeType === 3 && node.data.trim() !== '') return false;
            if (node.nodeType === 1 && !node.classList.contains('opensilver-uielement')) return false;
        }
        return true;
    }

    function isFrame(e, box, gap) {
        if (!isPanel(e) && !(supportsGeometry && isPlainShape(e))) return false;
        const style = getComputedStyle(e);
        if (style.position !== 'absolute' || style.transform !== 'none' || style.translate !== 'none' ||
            style.scale !== 'none' || style.rotate !== 'none') return false;
        return fills(e.getBoundingClientRect(), box, gap);
    }

    // The frame of the copy of a box, measured while the copy is at its own size, and the boxes
    // that hold the rest: { frame, holders }. Each holder is { element, x, y, w, h, runs }: where
    // its padding box is in the copy's, its size, and its elements that aren't frame, in runs of
    // siblings. A copy that is scaled on screen has no frame. A stopped layer has neither.
    function frameOf(copy, look, k) {
        if (copy.hasAttribute('data-os-layer')) return null;
        const box = copy.getBoundingClientRect();
        const s = copy.offsetWidth > 0 ? box.width / copy.offsetWidth : 1;
        const framed = Math.abs(k - 1) < 0.01;
        const parts = { frame: [], holders: [] };
        const visit = (holder) => {
            let run = null;
            for (const child of holder.element.children) {
                if (!child.classList.contains('opensilver-uielement')) {
                    run = null;
                } else if (framed && isFrame(child, box, FRAME_GAP * s)) {
                    run = null;
                    parts.frame.push(stretchOf(child));
                    if (child.tagName === 'DIV') {
                        const r = child.getBoundingClientRect();
                        visit({
                            element: child,
                            x: (r.left - box.left) / s + child.clientLeft - copy.clientLeft,
                            y: (r.top - box.top) / s + child.clientTop - copy.clientTop,
                            w: child.clientWidth, h: child.clientHeight, runs: [],
                        });
                    }
                } else {
                    if (!run) holder.runs.push(run = []);
                    run.push(child);
                }
            }
            if (holder.runs.length > 0) parts.holders.push(holder);
        };
        visit({
            element: copy, x: 0, y: 0, runs: [],
            w: look.w - look.borders[1] - look.borders[3], h: look.h - look.borders[0] - look.borders[2],
        });
        return parts;
    }

    // How a part of the frame follows the size of its parent: what its width and height lack to
    // fill it, and the same for its shape in the SVG.
    function stretchOf(e) {
        const style = getComputedStyle(e);
        const w = parseFloat(style.width) || 0, h = parseFloat(style.height) || 0;
        const entry = { e, dw: e.parentElement.clientWidth - w, dh: e.parentElement.clientHeight - h, shapes: [] };
        if (e.tagName === 'svg') {
            for (const shape of e.children) {
                const attr = (name) => parseFloat(shape.getAttribute(name)) || 0;
                if (shape.tagName === 'rect') {
                    entry.shapes.push({ shape, width: w - attr('width'), height: h - attr('height') });
                } else if (shape.tagName === 'ellipse') {
                    entry.shapes.push({ shape, cx: attr('cx') - w / 2, cy: attr('cy') - h / 2, rx: w / 2 - attr('rx'), ry: h / 2 - attr('ry') });
                }
            }
        }
        return entry;
    }

    function stretch(entry) {
        const less = (part, d) => 'calc(' + part + ' - ' + px(d) + ')';
        entry.e.style.width = less('100%', entry.dw);
        entry.e.style.height = less('100%', entry.dh);
        for (const s of entry.shapes) {
            if (s.shape.tagName === 'rect') {
                s.shape.style.width = less('100%', s.width);
                s.shape.style.height = less('100%', s.height);
            } else {
                s.shape.style.cx = less('50%', -s.cx);
                s.shape.style.cy = less('50%', -s.cy);
                s.shape.style.rx = less('50%', s.rx);
                s.shape.style.ry = less('50%', s.ry);
            }
        }
    }

    // What is inside the copy of a box goes, in wrappers of its own, from where the old content
    // is in the old box (way.fromContent in way.from) to where the new content is in the new box,
    // its scale going from z0 to z1 times its own size, held where anchorOf says. own and
    // ownContent are where the copy and its content are at their end, and parts its frame and
    // holders (frameOf). The frame follows the layer's size instead. With clip, what doesn't fit
    // in its holder on the way is cut off, like the content of a card that grows out of a field.
    function zoomContent(copy, parts, look, own, ownContent, k, z0, z1, way, play, clip) {
        if (!parts) return;
        for (const entry of parts.frame) stretch(entry);
        if (parts.holders.length === 0) return;
        const { from, fromContent: c0, end, endContent: c1, b0, b1 } = way;
        const ax = anchorOf(c0.x, c0.w, from.x, from.w, c1.x, c1.w, end.x, end.w);
        const ay = anchorOf(c0.y, c0.h, from.y, from.h, c1.y, c1.h, end.y, end.h);
        const lx = (ownContent.x - own.x + ax * ownContent.w) / k - look.borders[3];
        const ly = (ownContent.y - own.y + ay * ownContent.h) / k - look.borders[0];
        const x0 = c0.x - from.x + ax * c0.w, x1 = c1.x - end.x + ax * c1.w;
        const y0 = c0.y - from.y + ay * c0.h, y1 = c1.y - end.y + ay * c1.h;
        const s0 = k * z0, s1 = k * z1;
        // Around the copy's padding box, which moves with the borders.
        const frame = (q) => {
            const s = s0 + (s1 - s0) * q;
            const x = x0 + (x1 - x0) * q - (b0[3] + (b1[3] - b0[3]) * q) - s * lx;
            const y = y0 + (y1 - y0) * q - (b0[0] + (b1[0] - b0[0]) * q) - s * ly;
            return { translate: px(x) + ' ' + px(y), scale: String(s) };
        };
        const first = frame(0), last = frame(1);
        const moves = first.translate !== '0px 0px' || last.translate !== '0px 0px' || Math.abs(s0 - 1) >= 1e-3 || Math.abs(s1 - 1) >= 1e-3;
        if (!moves && !clip) return;
        for (const holder of parts.holders) {
            for (const run of holder.runs) {
                const wrapper = document.createElement('div');
                wrapper.setAttribute('data-os-foreign', '');
                wrapper.style.cssText = WRAPPER_STYLE;
                wrapper.style.width = px(holder.w);
                wrapper.style.height = px(holder.h);
                wrapper.style.transformOrigin = px(-holder.x) + ' ' + px(-holder.y);
                let outer = wrapper;
                if (clip) {
                    outer = document.createElement('div');
                    outer.setAttribute('data-os-foreign', '');
                    outer.style.cssText = CLIP_STYLE;
                    outer.appendChild(wrapper);
                }
                holder.element.insertBefore(outer, run[0]);
                for (const child of run) wrapper.appendChild(child);
                if (moves) play(wrapper, frame);
            }
        }
    }

    // The way of a copy of a text, so that its letters go from where the old text's letters are
    // (fromText, in a box at from) to where the new text's letters are (endText, in a box at end):
    // own and ownText are where the copy and its letters are at their end. The same text at both
    // ends also stretches a little, so that its two copies cover each other exactly.
    function textFrames(look, own, ownText, from, fromText, end, endText, same) {
        const kx = own.w / look.w, ky = own.h / look.h;
        const lx = (ownText.x - own.x) / kx, ly = (ownText.y - own.y) / ky;
        const lw = Math.max(1e-3, ownText.w / kx), lh = Math.max(1e-3, ownText.h / ky);
        const ox0 = fromText.x - from.x, oy0 = fromText.y - from.y, ox1 = endText.x - end.x, oy1 = endText.y - end.y;
        const stretch = same && Math.abs((fromText.w * endText.h) / (fromText.h * endText.w) - 1) < 0.15;
        const a = look.align;
        return (q) => {
            const tw = fromText.w + (endText.w - fromText.w) * q, th = fromText.h + (endText.h - fromText.h) * q;
            const sy = th / lh, sx = stretch ? tw / lw : sy;
            const x = ox0 + (ox1 - ox0) * q + a * tw - sx * (lx + a * lw);
            const y = oy0 + (oy1 - oy0) * q - sy * ly;
            return { translate: px(x) + ' ' + px(y), scale: sx + ' ' + sy };
        };
    }

    function lerpPx(a, b, q) {
        return a.map((v, i) => px(v + (b[i] - v) * q)).join(' ');
    }

    function differ(a, b) {
        return a.some((v, i) => Math.abs(v - b[i]) > 0.01);
    }

    function fly(travel, endRect, ancestorRect, endLook) {
        const { v, timing, to } = travel.p;
        const { layer, container, from, oldLook, oldOpacity, newOpacity } = travel;
        const oldCopy = layer.firstChild;
        const oldText = (v[5] & TRAVEL_OLD_TEXT) !== 0, newText = (v[5] & TRAVEL_NEW_TEXT) !== 0;
        const k0 = from.w / oldLook.w;
        const oldParts = oldText ? null : frameOf(oldCopy, oldLook, k0);
        const newCopy = copyOf(to);
        keepInherited(to, newCopy);
        replaceMedia(to, newCopy);

        const end = boxIn(container, endRect, ancestorRect);
        placeLayer(layer, end);
        fitCopy(newCopy, newText ? endLook : null);
        layer.appendChild(newCopy);
        const k1 = end.w / endLook.w;
        const newParts = newText ? null : frameOf(newCopy, endLook, k1);

        const d = Math.max(1, timing.d);
        const moveCurve = curveOf(timing, false), sizeCurve = curveOf(timing, true);
        const sizeOptions = { duration: d, fill: 'both', easing: sizeCurve.easing };
        const fade = { duration: d, fill: 'both' };
        const dx = from.x - end.x, dy = from.y - end.y;
        const animations = [
            layer.animate(
                lerpKeyframes(moveCurve, (q) => ({ translate: px(dx * (1 - q)) + ' ' + px(dy * (1 - q)) })),
                { duration: d, fill: 'both', easing: moveCurve.easing }),
            layer.animate(
                lerpKeyframes(sizeCurve, (q) => ({ width: px(from.w + (end.w - from.w) * q), height: px(from.h + (end.h - from.h) * q) })),
                sizeOptions),
        ];
        const play = (element, make) => animations.push(element.animate(lerpKeyframes(sizeCurve, make), sizeOptions));

        // A text travels by its letters.
        const fromText = travel.fromText || from;
        const endText = endLook.text ? boxIn(container, endLook.text, ancestorRect) : end;
        const same = oldText && newText && oldCopy.textContent === newCopy.textContent;
        if (oldText) play(oldCopy, textFrames(oldLook, from, fromText, from, fromText, end, endText, same));
        if (newText) play(newCopy, textFrames(endLook, end, endText, from, fromText, end, endText, same));

        // A box changes its width and height with the layer, with its frame, and its corners and
        // borders go from the old ones to the new ones in both copies, so that they keep the same
        // shape. What is inside it goes from where it is in the old box to where it is in the new
        // one, and zooms as much as its largest letters change size.
        const r0 = cornersOf(oldLook).map((r) => r * k0), r1 = cornersOf(endLook).map((r) => r * k1);
        const b0 = oldLook.borders.map((b) => b * k0), b1 = endLook.borders.map((b) => b * k1);
        const shape = differ(r0, r1) || differ(b0, b1)
            ? (q) => ({ borderRadius: lerpPx(r0, r1, q), borderWidth: lerpPx(b0, b1, q) })
            : null;
        const fromContent = travel.fromContent || from;
        const endContent = endLook.content ? boxIn(container, endLook.content, ancestorRect) : end;
        let z = oldLook.font > 0 && endLook.font > 0
            ? (endLook.font * k1) / (oldLook.font * k0)
            : zoomOf(endContent.w / fromContent.w, endContent.h / fromContent.h);
        if (!(z > 0 && isFinite(z))) z = 1;
        const way = { from, fromContent, end, endContent, b0, b1 };
        if (!oldText) {
            if (shape) play(oldCopy, shape);
            const clip = fromContent.w * z > end.w + 1 || fromContent.h * z > end.h + 1;
            zoomContent(oldCopy, oldParts, oldLook, from, fromContent, k0, 1, z, way, play, clip);
        }
        if (!newText) {
            if (shape) play(newCopy, shape);
            const clip = endContent.w / z > from.w + 1 || endContent.h / z > from.h + 1;
            zoomContent(newCopy, newParts, endLook, end, endContent, k1, 1 / z, 1, way, play, clip);
        }

        // Two different texts take turns, so that their letters don't mix on the way. Otherwise
        // the copies add up, or, where the browser can't, the new one comes over the old one.
        if (oldText && newText && !same) {
            animations.push(oldCopy.animate([{ opacity: oldOpacity }, { opacity: 0, offset: 0.6 }, { opacity: 0 }], fade));
            animations.push(newCopy.animate([{ opacity: 0 }, { opacity: 0, offset: 0.4 }, { opacity: newOpacity }], fade));
        } else if (PLUS_LIGHTER) {
            play(oldCopy, (q) => ({ opacity: oldOpacity * (1 - q) }));
            play(newCopy, (q) => ({ opacity: newOpacity * q }));
        } else {
            animations.push(oldCopy.animate([{ opacity: oldOpacity }, { opacity: oldOpacity, offset: 0.5 }, { opacity: 0 }], fade));
            animations.push(newCopy.animate([{ opacity: 0 }, { opacity: newOpacity, offset: 0.5 }, { opacity: newOpacity }], fade));
        }
        for (const animation of animations) startAtNextFrame(animation);
        travel.animations = animations;
        animations[0].addEventListener('finish', () => endTravel(to, travel));
        // In case it never finishes, like in a tab in the background.
        travel.timer = setTimeout(() => endTravel(to, travel), d + 1000);
    }

    // The text effects of TextBlocks, per element: { overlay, visibility, overflow, observer,
    // timer, raf, animations, held, shown, value }. The real text stays in the element, hidden, and
    // is measured as usual: the effect plays in an overlay on top of it, with the same font and
    // alignment. held is true while a change waits for its layout pass, shown returns the text the
    // overlay shows, and value is the number a CountUp shows.
    const texts = new WeakMap();

    const TEXT_RELEASE = 0, TEXT_ROLL = 1, TEXT_CROSS_FADE = 2, TEXT_SCRAMBLE = 3, TEXT_RETYPE = 4, TEXT_COUNT_UP = 5;
    const LETTERS_TYPE = 1, LETTERS_FADE = 2, LETTERS_ERASE = 3;

    // Out of the flow: the TextBlock measures its text by copying its content, overlay included.
    const OVERLAY_STYLE = 'position:absolute;left:0;top:0;right:0;bottom:0;box-sizing:border-box;padding:inherit;' +
                          'background:inherit;visibility:visible;pointer-events:none;';
    // A second text in the overlay, on top of the first, laid out the same way.
    const TOP_LAYER_STYLE = 'position:absolute;left:0;top:0;right:0;bottom:0;box-sizing:border-box;padding:inherit;';

    const segmenter = typeof Intl !== 'undefined' && Intl.Segmenter ? new Intl.Segmenter(undefined, { granularity: 'grapheme' }) : null;

    // The letters of a text, an emoji or a letter with its accents being one letter.
    function lettersOf(text) {
        return segmenter ? Array.from(segmenter.segment(text), (s) => s.segment) : Array.from(text);
    }

    function isForeign(node) {
        return node.nodeType === 1 && node.hasAttribute('data-os-foreign');
    }

    // Whether a change of the element's content comes from its text effect.
    function isOwnChange(st, element, record) {
        if (st.overlay.contains(record.target)) return true;
        if (record.target !== element || record.type !== 'childList') return false;
        for (const node of record.addedNodes) if (node !== st.overlay) return false;
        for (const node of record.removedNodes) if (node !== st.overlay) return false;
        return true;
    }

    function startText(element) {
        endText(element);
        const overlay = document.createElement('div');
        overlay.setAttribute('data-os-foreign', '');
        overlay.setAttribute('data-os-text', '');
        overlay.style.cssText = OVERLAY_STYLE;
        // Decorations don't reach the content of an element out of the flow.
        const decoration = getComputedStyle(element).textDecorationLine;
        if (decoration && decoration !== 'none') overlay.style.textDecoration = getComputedStyle(element).textDecoration;
        element.appendChild(overlay);

        const st = {
            overlay, visibility: element.style.visibility, overflow: element.style.overflow, observer: null,
            timer: 0, raf: 0, animations: [], held: false, shown: null, value: null,
        };
        overlay.setAttribute('data-os-visibility', st.visibility);
        overlay.setAttribute('data-os-overflow', st.overflow);
        element.style.visibility = 'hidden';
        element.style.overflow = 'visible';

        // The real text shows when the app changes it without a text effect, like in its inlines.
        st.observer = new MutationObserver((records) => {
            if (records.some((r) => !isOwnChange(st, element, r))) endText(element, st);
        });
        st.observer.observe(element, { childList: true, characterData: true, subtree: true });
        texts.set(element, st);
        return st;
    }

    function endText(element, st) {
        const current = texts.get(element);
        if (!current || (st && current !== st)) return;
        texts.delete(element);
        current.observer.disconnect();
        clearTimeout(current.timer);
        if (current.raf) cancelAnimationFrame(current.raf);
        for (const animation of current.animations) animation.cancel();
        current.overlay.remove();
        element.style.visibility = current.visibility;
        element.style.overflow = current.overflow;
    }

    // Ends the text effect after duration, counted from the next frame, like its animations.
    // Returns the clock, whose currentTime tells how far the effect went.
    function endTextAfter(element, st, duration) {
        const clock = startAtNextFrame(st.overlay.animate([], { duration: Math.max(1, duration) }));
        st.animations.push(clock);
        clock.addEventListener('finish', () => finishText(element, st));
        clearTimeout(st.timer);
        st.timer = setTimeout(() => endText(element, st), duration + 1000);
        return clock;
    }

    // An empty copy of the first run of the TextBlock, to write a text in its font.
    function plainLayer(element, text) {
        let run = null;
        for (const child of element.children) {
            if (isForeign(child)) continue;
            run = child.matches('.opensilver-inline') ? child : child.querySelector('.opensilver-inline');
            if (run) break;
        }
        const layer = run ? run.cloneNode(false) : document.createElement('span');
        layer.removeAttribute('id');
        if (text !== undefined) layer.textContent = text;
        return layer;
    }

    // A copy of the real content of the TextBlock.
    function contentCopy(element) {
        const fragment = document.createDocumentFragment();
        for (const child of element.childNodes) {
            if (isForeign(child)) continue;
            const copy = child.cloneNode(true);
            if (copy.nodeType === 1) {
                copy.removeAttribute('id');
                for (const e of copy.querySelectorAll('[id]')) e.removeAttribute('id');
            }
            fragment.appendChild(copy);
        }
        return fragment;
    }

    function realText(element) {
        let text = '';
        for (const child of element.childNodes) {
            if (!isForeign(child)) text += child.textContent;
        }
        return text;
    }

    // Puts each letter of the text inside root in a span of its own, and returns the spans in order.
    function splitLetters(root) {
        const nodes = [];
        const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT);
        for (let node = walker.nextNode(); node; node = walker.nextNode()) nodes.push(node);
        const letters = [];
        for (const node of nodes) {
            const fragment = document.createDocumentFragment();
            for (const letter of lettersOf(node.data)) {
                const span = document.createElement('span');
                span.textContent = letter;
                fragment.appendChild(span);
                letters.push(span);
            }
            node.replaceWith(fragment);
        }
        return letters;
    }

    // The copies of an element made for a ghost show its real text, without its text effect.
    function removeTextOverlays(root) {
        for (const overlay of root.querySelectorAll('[data-os-text]')) {
            const owner = overlay.parentNode;
            owner.style.visibility = overlay.getAttribute('data-os-visibility') || '';
            owner.style.overflow = overlay.getAttribute('data-os-overflow') || '';
            overlay.remove();
        }
    }

    // Keeps showing the old text of a TextBlock whose text just changed, until its layout pass
    // plays the change, for at most a second. A text effect that is still playing goes on, and the
    // change starts from where it is then.
    function textHold(id, oldText) {
        const element = document.getElementById(id);
        if (!element) return;
        const running = texts.get(element);
        if (running && running.shown) {
            // The new text was just written in the same script: its record must not end the effect.
            running.observer.takeRecords();
            running.held = true;
            clearTimeout(running.timer);
            running.timer = setTimeout(() => endText(element, running), 1000);
            return;
        }
        const st = startText(element);
        st.overlay.appendChild(plainLayer(element, oldText));
        st.held = true;
        st.shown = () => oldText;
        st.timer = setTimeout(() => endText(element, st), 1000);
    }

    // An effect that holds a change keeps its last frame until the change plays.
    function finishText(element, st) {
        if (!st.held) endText(element, st);
    }

    function textStop(id) {
        const element = document.getElementById(id);
        if (element) endText(element);
    }

    // Plays the change of a TextBlock's text, from the old text that its hold shows.
    function playText(element, w, timing) {
        const held = texts.get(element);
        if (w[1] === TEXT_RELEASE || !held || !held.held) {
            endText(element);
            return;
        }

        const from = held.shown(), value = held.value;
        const st = startText(element);
        switch (w[1]) {
            case TEXT_ROLL: rollText(element, st, timing, w[3], w[4], from); break;
            case TEXT_SCRAMBLE: scrambleText(element, st, timing, from, w[3]); break;
            case TEXT_RETYPE: retypeText(element, st, w[3], from, w[5], w[6], w[7]); break;
            case TEXT_COUNT_UP: countText(element, st, timing, value !== null ? value : w[3], w[4], w[5], w[6], w[7], w[8], w[9]); break;
            default: crossFadeText(element, st, timing, from); break;
        }
    }

    function crossFadeText(element, st, timing, from) {
        const old = plainLayer(element, from);
        const box = document.createElement('div');
        box.style.cssText = TOP_LAYER_STYLE;
        box.appendChild(contentCopy(element));
        st.overlay.append(old, box);
        const text = realText(element);
        st.shown = () => text;

        const curve = curveOf(timing, false);
        const options = { duration: Math.max(1, timing.d), easing: curve.easing };
        st.animations.push(
            startAtNextFrame(old.animate(lerpKeyframes(curve, (p) => ({ opacity: 1 - p })), Object.assign({ fill: 'both' }, options))),
            startAtNextFrame(box.animate(lerpKeyframes(curve, (p) => ({ opacity: p })), Object.assign({ fill: 'backwards' }, options))));
        endTextAfter(element, st, timing.d);
    }

    // The characters that change roll, each in a column that clips them and goes from the width
    // of the old character to the width of the new one. direction is 1 when the new characters
    // come from below, -1 from above.
    function rollText(element, st, timing, direction, columns, from) {
        const layer = plainLayer(element);
        const rolls = [];
        let text = '';
        for (const c of columns) {
            if (typeof c === 'string') {
                layer.appendChild(document.createTextNode(c));
                text += c;
                continue;
            }
            const column = document.createElement('span');
            column.style.cssText = 'display:inline-block;position:relative;clip-path:inset(0 -0.5em);';
            const incoming = document.createElement('span');
            incoming.style.display = 'inline-block';
            // A column without a character still has the height of a line, to clip the old one.
            incoming.textContent = c[1] || '\u200B';
            const outgoing = document.createElement('span');
            outgoing.style.cssText = 'position:absolute;left:0;top:0;';
            outgoing.textContent = c[0];
            column.append(incoming, outgoing);
            layer.appendChild(column);
            rolls.push({ column, incoming, outgoing });
            text += c[1];
        }
        st.overlay.appendChild(layer);
        st.shown = () => text;

        // Text on several lines cross-fades instead.
        if (rolls.length === 0 || layer.getClientRects().length > 1) {
            layer.remove();
            crossFadeText(element, st, timing, from);
            return;
        }

        const curve = curveOf(timing, false);
        const options = { duration: Math.max(1, timing.d), easing: curve.easing };
        const widths = rolls.map((r) => [r.outgoing.offsetWidth, r.incoming.offsetWidth]);
        rolls.forEach((r, i) => {
            const [oldWidth, newWidth] = widths[i];
            if (oldWidth !== newWidth) {
                st.animations.push(startAtNextFrame(r.column.animate(lerpKeyframes(curve, (p) => ({ width: px(oldWidth + (newWidth - oldWidth) * p) })),
                    Object.assign({ fill: 'backwards' }, options))));
            }
            st.animations.push(
                startAtNextFrame(r.incoming.animate(lerpKeyframes(curve, (p) => ({ translate: '0 ' + Math.round(direction * 100 * (1 - p)) + '%', opacity: p })),
                    Object.assign({ fill: 'backwards' }, options))),
                startAtNextFrame(r.outgoing.animate(lerpKeyframes(curve, (p) => ({ translate: '0 ' + Math.round(-direction * 100 * p) + '%', opacity: 1 - p })),
                    Object.assign({ fill: 'both' }, options))));
        });
        endTextAfter(element, st, timing.d);
    }

    const DIGITS = '0123456789', UPPER = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', LOWER = 'abcdefghijklmnopqrstuvwxyz';

    // A random character of the same kind: a digit for a digit, a letter of the same case for a
    // letter. Spaces and punctuation stay.
    function randomLike(letter) {
        const set = letter >= '0' && letter <= '9' ? DIGITS
            : letter !== letter.toLowerCase() ? UPPER
            : letter !== letter.toUpperCase() ? LOWER
            : null;
        return set ? set[Math.floor(Math.random() * set.length)] : letter;
    }

    // Shows random characters that settle on the new text from left to right, changing 20 times a
    // second. The letters that are the same in both texts stay.
    function scrambleText(element, st, timing, from, to) {
        const layer = plainLayer(element, from);
        st.overlay.appendChild(layer);
        const old = lettersOf(from), target = lettersOf(to);
        const n = target.length, d = Math.max(1, timing.d);
        let text = from, start = 0, tick = -1;
        st.shown = () => text;
        const step = (now) => {
            if (texts.get(element) !== st) return;
            if (!start) start = now;
            const t = now - start;
            if (t >= d) {
                text = to;
                layer.textContent = to;
                finishText(element, st);
                return;
            }
            if (Math.floor(t / 50) !== tick) {
                tick = Math.floor(t / 50);
                let s = '';
                for (let i = 0; i < n; i++) {
                    s += old[i] === target[i] || t >= d * (0.3 + 0.7 * (i + 1) / n) ? target[i] : randomLike(target[i]);
                }
                text = s;
                layer.textContent = s;
            }
            st.raf = requestAnimationFrame(step);
        };
        st.raf = requestAnimationFrame(step);
    }

    // Erases the old text letter by letter down to what it shares with the new one, then types the
    // new one, step k at blinkFirst + k * letterMs. The old text is erased in its own layout, and
    // the new one typed in its final layout. With a cursor (cursorMs above 0), the cursor blinks
    // for blinkFirst first, follows the letters, then blinks twice and fades away.
    function retypeText(element, st, letterMs, from, to, cursorMs, blinkFirst) {
        const a = lettersOf(from), b = lettersOf(to);
        let shared = 0;
        while (shared < a.length && shared < b.length && a[shared] === b[shared]) shared++;
        const erase = a.length - shared, steps = erase + b.length - shared;
        const ms = Math.max(1, letterMs);
        const old = plainLayer(element, from);
        const box = document.createElement('div');
        box.style.cssText = TOP_LAYER_STYLE;
        const next = plainLayer(element, to);
        box.appendChild(next);
        st.overlay.append(old, box);
        const oldLetters = splitLetters(old), newLetters = splitLetters(next);

        const hide = (node, at) => st.animations.push(startAtNextFrame(node.animate([{ opacity: 0 }, { opacity: 0 }], { duration: 1, delay: at, fill: 'forwards' })));
        const hideUntil = (node, at) => st.animations.push(startAtNextFrame(node.animate([{ opacity: 0 }, { opacity: 0 }], { duration: at, fill: 'backwards' })));
        const swap = Math.max(1, blinkFirst + erase * ms);
        for (let i = shared; i < a.length; i++) hide(oldLetters[i], blinkFirst + (a.length - i) * ms);
        hide(old, swap);
        hideUntil(box, swap);
        for (let j = shared; j < b.length; j++) hideUntil(newLetters[j], swap + (j - shared + 1) * ms);

        let end = swap + (b.length - shared) * ms;
        const first = oldLetters[0] || newLetters[0];
        if (cursorMs > 0 && first) {
            const positions = [];
            for (let k = 0; k <= steps; k++) {
                const count = k <= erase ? a.length - k : shared + k - erase;
                const letters = k <= erase ? oldLetters : newLetters;
                positions.push(count > 0 ? after(letters[count - 1]) : before(first));
            }
            end = playCursor(st.animations, makeCursor(st.overlay, first), positions, 0, blinkFirst, ms, cursorMs / 5, true);
        }

        const clock = endTextAfter(element, st, end);
        st.shown = () => {
            const k = Math.max(0, Math.min(steps, Math.floor(((clock.currentTime || 0) - blinkFirst) / ms)));
            return k <= erase ? a.slice(0, a.length - k).join('') : b.slice(0, shared + k - erase).join('');
        };
    }

    function formatNumber(v, decimals, group, separator) {
        const fixed = Math.abs(v).toFixed(decimals);
        const dot = fixed.indexOf('.');
        let integer = dot < 0 ? fixed : fixed.slice(0, dot);
        if (group) integer = integer.replace(/\B(?=(\d{3})+(?!\d))/g, () => group);
        const negative = v < 0 && Number(fixed) !== 0;
        return (negative ? '-' : '') + integer + (dot < 0 ? '' : separator + fixed.slice(dot + 1));
    }

    // Counts from one number to the other, slowing down at the end, in the format of the new text.
    function countText(element, st, timing, from, to, decimals, prefix, suffix, group, separator) {
        const layer = plainLayer(element);
        const write = (v) => { layer.textContent = prefix + formatNumber(v, decimals, group, separator) + suffix; };
        write(from);
        st.overlay.appendChild(layer);
        st.shown = () => layer.textContent;
        st.value = from;
        const d = Math.max(1, timing.d);
        let start = 0;
        const step = (now) => {
            if (texts.get(element) !== st) return;
            if (!start) start = now;
            const p = Math.min(1, (now - start) / d);
            st.value = from + (to - from) * (1 - Math.pow(1 - p, 3));
            write(st.value);
            if (p >= 1) {
                finishText(element, st);
                return;
            }
            st.raf = requestAnimationFrame(step);
        };
        st.raf = requestAnimationFrame(step);
    }

    // A hidden cursor, a tenth of the letter's font size wide and in its color. parent must be the
    // letters' offsetParent: the cursor's left and top, like their offsetLeft and offsetTop, are
    // from its padding edge.
    function makeCursor(parent, letter) {
        const style = getComputedStyle(letter);
        const cursor = document.createElement('span');
        cursor.style.cssText = 'position:absolute;left:0;top:0;opacity:0;';
        cursor.style.width = px(Math.max(1, parseFloat(style.fontSize) * 0.1));
        cursor.style.background = style.color;
        parent.appendChild(cursor);
        return cursor;
    }

    // Where a cursor goes before or after a letter: [x, y, height].
    function before(letter) { return [letter.offsetLeft, letter.offsetTop, letter.offsetHeight]; }
    function after(letter) { return [letter.offsetLeft + letter.offsetWidth, letter.offsetTop, letter.offsetHeight]; }

    // Plays a cursor that blinks at the first position for blinkFirst, goes to the next position
    // every letterMs, then, if fadeAway, blinks twice and fades away. It is on, or off, for
    // blinkMs in a blink, and shows nothing during its delay. Returns how long it plays, delay
    // not included.
    function playCursor(animations, cursor, positions, delay, blinkFirst, letterMs, blinkMs, fadeAway) {
        const active = blinkFirst + (positions.length - 1) * letterMs;
        const total = Math.max(1, active + (fadeAway ? 5 * blinkMs : 0));
        const at = (ms) => Math.min(1, ms / total);
        const moves = positions.map((p, i) => ({ offset: i === 0 ? 0 : at(blinkFirst + i * letterMs), translate: px(p[0]) + ' ' + px(p[1]), height: px(p[2]), easing: 'steps(1, end)' }));
        const last = positions[positions.length - 1];
        moves.push({ offset: 1, translate: px(last[0]) + ' ' + px(last[1]), height: px(last[2]) });

        const blinks = [];
        let on = true;
        for (let t = 0; blinkMs > 0 && t < blinkFirst; t += blinkMs, on = !on) blinks.push({ offset: at(t), opacity: on ? 1 : 0, easing: 'steps(1, end)' });
        blinks.push({ offset: at(blinkFirst), opacity: 1, easing: 'steps(1, end)' });
        if (fadeAway) {
            blinks.push(
                { offset: at(active + blinkMs), opacity: 0, easing: 'steps(1, end)' },
                { offset: at(active + 2 * blinkMs), opacity: 1, easing: 'steps(1, end)' },
                { offset: at(active + 3 * blinkMs), opacity: 0, easing: 'steps(1, end)' },
                { offset: at(active + 4 * blinkMs), opacity: 1, easing: 'ease-in' },
                { offset: 1, opacity: 0 });
        } else {
            blinks.push({ offset: 1, opacity: 1 });
        }
        animations.push(
            startAtNextFrame(cursor.animate(moves, { duration: total, delay })),
            startAtNextFrame(cursor.animate(blinks, { duration: total, delay })));
        return total;
    }

    // Types the letters of a TextBlock that appears one after the other, or fades them in.
    function appearText(element, timing, delay, text) {
        const [kind, letterMs, cursorMs, blinkFirst] = text;
        const st = startText(element);
        st.overlay.appendChild(contentCopy(element));
        const letters = splitLetters(st.overlay);
        const n = letters.length;
        if (n === 0) {
            endText(element, st);
            return;
        }

        let end;
        if (kind === LETTERS_FADE) {
            const d = Math.max(1, timing.d), each = d / 2, stagger = n > 1 ? (d - each) / (n - 1) : 0;
            const curve = curveOf(timing, false);
            letters.forEach((letter, i) => st.animations.push(startAtNextFrame(letter.animate(lerpKeyframes(curve, (p) => ({ opacity: p })),
                { duration: each, delay: delay + i * stagger, fill: 'backwards', easing: curve.easing }))));
            end = delay + d;
        } else {
            letters.forEach((letter, i) => st.animations.push(startAtNextFrame(letter.animate([{ opacity: 0 }, { opacity: 0 }],
                { duration: delay + blinkFirst + (i + 1) * letterMs, fill: 'backwards' }))));
            end = delay + blinkFirst + n * letterMs;
            if (cursorMs > 0) {
                const positions = [before(letters[0])].concat(letters.map(after));
                end = delay + playCursor(st.animations, makeCursor(st.overlay, letters[0]), positions, delay, blinkFirst, letterMs, cursorMs / 5, true);
            }
        }
        endTextAfter(element, st, end);
    }

    // Erases the letters of the ghost of a TextBlock from the last one, or fades them out from the
    // first one, then removes the ghost.
    function vanishText(id, ghost, timing, delay, text) {
        const [kind, letterMs, cursorMs, blinkFirst] = text;
        const letters = splitLetters(ghost.clone);
        const n = letters.length;
        let duration;
        if (kind === LETTERS_FADE) {
            const d = Math.max(1, timing.d), each = d / 2, stagger = n > 1 ? (d - each) / (n - 1) : 0;
            const curve = curveOf(timing, false);
            letters.forEach((letter, i) => startAtNextFrame(letter.animate(lerpKeyframes(curve, (p) => ({ opacity: 1 - p })),
                { duration: each, delay: delay + i * stagger, fill: 'forwards', easing: curve.easing })));
            duration = d;
        } else {
            letters.forEach((letter, i) => startAtNextFrame(letter.animate([{ opacity: 0 }, { opacity: 0 }],
                { duration: 1, delay: delay + blinkFirst + (n - i) * letterMs, fill: 'forwards' })));
            duration = Math.max(1, blinkFirst + n * letterMs);
            if (cursorMs > 0 && n > 0) {
                const positions = [];
                for (let k = 0; k < n; k++) positions.push(after(letters[n - 1 - k]));
                positions.push(before(letters[0]));
                playCursor([], makeCursor(ghost.clone, letters[n - 1]), positions, delay, blinkFirst, letterMs, cursorMs / 5, false);
            }
        }

        // Tells how far it went, if the element appears again before the end.
        ghost.animation = startAtNextFrame(ghost.clone.animate([], { duration, delay }));
        ghost.curve = null;
        ghost.animation.addEventListener('finish', () => removeGhost(id, ghost));
        ghost.timer = setTimeout(() => removeGhost(id, ghost), delay + duration + 1000);
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

        const trips = batch.v && batch.v.length > 0 ? prepareTravels(batch.v, batch.t) : null;

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
                if (element) appear(element, batch.t[e[1]], e[2], e.slice(3, 12), e[12] === 1, e.length > 13 ? e.slice(13, 17) : null);
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
            for (const g of batch.g) vanish(g[0], batch.t[g[1]], g[2], g.slice(3, 12), g[12] === 1, g.length > 13 ? g.slice(13, 17) : null);
        }

        if (batch.x) {
            for (const id of batch.x) {
                removeGhost(id);
                removeCapture(id);
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

        if (batch.w) {
            for (const w of batch.w) {
                const element = document.getElementById(w[0]);
                if (element) playText(element, w, batch.t[w[2]]);
            }
        }

        if (trips) startTravels(trips);
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

    let consoleCommand = null;

    // The functions for people, in the browser console: each one changes the setting of
    // AnimationSettings with the same name, and says what it did, or without a value, what it is.
    function command(name) {
        return (value) => { consoleCommand(name, value === undefined ? '' : String(value)); };
    }

    function init(onDragStarted, onDragEnded, onReducedMotionOn, onReducedMotionOff, onConsoleCommand) {
        dragStarted = onDragStarted;
        dragEnded = onDragEnded;
        consoleCommand = onConsoleCommand;
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

    globalThis.osAnimations = Object.freeze({
        init, run, hold, ghost, ghostInView, capture, react, reactSettings, textHold, textStop,
        slowMotion: command('slowMotion'),
        showWhatAnimates: command('showWhatAnimates'),
        logForDebugging: command('logForDebugging'),
        simulateReducedMotion: command('simulateReducedMotion'),
        isEnabled: command('isEnabled'),
    });
})();
