const shortcuts = new Map([
    ["v", "Verb"], ["n", "Noun"], ["p", "Proceed"], ["r", "Reset"],
    ["e", "Enter"], ["enter", "Enter"], ["c", "Clear"], ["backspace", "Clear"],
    ["k", "KeyRelease"], ["+", "Plus"], ["=", "Plus"], ["-", "Minus"]
]);
const digits = ["Zero", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine"];

export function initialize(session) {
    const canvas = document.querySelector("#face");
    const context = canvas.getContext("2d", { alpha: false });
    if (!context) throw new Error("Canvas 2D is unavailable.");

    const width = canvas.width;
    const height = canvas.height;
    const buttons = new Map(Array.from(document.querySelectorAll("#panel .key"), button => [button.dataset.key, button]));
    const pressed = new Map();
    let previousFrame;
    let lastDrawing;

    function press(source, button) {
        if (!button || button.disabled || pressed.has(source)) return;
        const alreadyHeld = Array.from(pressed.values()).includes(button);
        pressed.set(source, button);
        if (!alreadyHeld) session.invokeMethod("SetKey", Number(button.dataset.code), true);
    }

    function release(source) {
        const button = pressed.get(source);
        if (!button) return;
        pressed.delete(source);
        if (!Array.from(pressed.values()).includes(button)) {
            session.invokeMethod("SetKey", Number(button.dataset.code), false);
        }
    }

    function releaseAll() {
        pressed.clear();
        session.invokeMethod("ReleaseKeys");
    }

    function resetInput() {
        previousFrame = undefined;
        releaseAll();
    }

    function paint() {
        if (!lastDrawing) return;
        const bounds = canvas.getBoundingClientRect();
        const pixelWidth = Math.round(bounds.width * window.devicePixelRatio);
        const pixelHeight = Math.round(bounds.height * window.devicePixelRatio);
        if (!pixelWidth || !pixelHeight) return;
        if (canvas.width !== pixelWidth || canvas.height !== pixelHeight) {
            canvas.width = pixelWidth;
            canvas.height = pixelHeight;
        }
        context.setTransform(pixelWidth / width, 0, 0, pixelHeight / height, 0, 0);

        const data = lastDrawing;
        for (let at = 0; at < data.length;) {
            const count = data[at++];
            const top = color(data[at++]);
            if (count === 0) {
                context.fillStyle = top;
                context.fillRect(0, 0, width, height);
            } else if (count === 1) {
                context.fillStyle = top;
                context.fillRect(data[at++], data[at++], data[at++], data[at++]);
            } else {
                const bottom = color(data[at++]);
                const y = data[at++];
                const depth = data[at++];
                context.fillStyle = top;
                if (top !== bottom) {
                    const gradient = context.createLinearGradient(0, y, 0, y + depth);
                    gradient.addColorStop(0, top);
                    gradient.addColorStop(1, bottom);
                    context.fillStyle = gradient;
                }
                context.beginPath();
                context.moveTo(data[at++], data[at++]);
                for (let point = 1; point < count; point++) context.lineTo(data[at++], data[at++]);
                context.closePath();
                context.fill();
            }
        }
    }

    function advance(seconds) {
        const drawing = session.invokeMethod("AdvanceFrame", seconds);
        if (!drawing) return;
        lastDrawing = new Float32Array(drawing.buffer, drawing.byteOffset, drawing.byteLength / 4);
        paint();
    }

    function frame(now) {
        if (!document.hidden) {
            const seconds = previousFrame === undefined ? 0 : Math.min((now - previousFrame) / 1000, 0.1);
            previousFrame = now;
            try {
                advance(seconds);
            } catch (error) {
                releaseAll();
                const status = document.querySelector("#status");
                status.hidden = false;
                status.textContent = "The emulation stopped. Reload to start it again.";
                console.error(error);
                return;
            }
        }
        requestAnimationFrame(frame);
    }

    window.addEventListener("keydown", event => {
        if (event.altKey || event.ctrlKey || event.metaKey) return;
        const key = event.key.toLowerCase();
        const focused = event.target.closest?.("#panel .key");
        if (!focused && event.target.closest?.("button, select, input, textarea, a, [contenteditable]")) return;
        const button = focused && (key === "enter" || key === " ")
            ? focused : buttons.get(digits[key] ?? shortcuts.get(key));
        if (!button || button.disabled) return;
        event.preventDefault();
        if (!event.repeat) press(event.code, button);
    });
    window.addEventListener("keyup", event => release(event.code));
    window.addEventListener("blur", releaseAll);
    document.addEventListener("visibilitychange", resetInput);

    for (const button of buttons.values()) {
        button.addEventListener("pointerdown", event => {
            if (event.button !== 0) return;
            event.preventDefault();
            button.setPointerCapture(event.pointerId);
            press(event.pointerId, button);
        });
        for (const event of ["pointerup", "pointercancel", "lostpointercapture"]) {
            button.addEventListener(event, input => release(input.pointerId));
        }
        button.addEventListener("click", event => {
            if (event.detail !== 0) return;
            const source = Symbol();
            press(source, button);
            if (button.dataset.key === "Proceed") window.setTimeout(() => release(source), 200);
            else release(source);
        });
    }

    advance(0);
    new ResizeObserver(paint).observe(canvas);
    requestAnimationFrame(frame);
    return { resetInput };
}

const color = rgb => `#${rgb.toString(16).padStart(6, "0")}`;
