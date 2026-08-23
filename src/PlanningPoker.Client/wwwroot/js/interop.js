import confetti from "./vendor/confetti.js";

function fireConfettiBurst(particleRatio, options) {
    confetti({
        ...options,
        disableForReducedMotion: true,
        particleCount: Math.floor(200 * particleRatio),
    });
}

// Mirrors the burst sequence self-host-planning-poker fires when a round reveals unanimous agreement.
export function celebrateConsensus() {
    const origin = { x: 0.5, y: 0.6 };
    fireConfettiBurst(0.25, { origin, spread: 26, startVelocity: 55 });
    fireConfettiBurst(0.2, { origin, spread: 60 });
    fireConfettiBurst(0.35, { origin, spread: 100, decay: 0.91, scalar: 0.8 });
    fireConfettiBurst(0.1, { origin, spread: 120, startVelocity: 25, decay: 0.92, scalar: 1.2 });
    fireConfettiBurst(0.1, { origin, spread: 120, startVelocity: 45 });
}

export function focusElement(selector) {
    const element = document.querySelector(selector);
    element?.focus();
}

export function copyToClipboard(text) {
    return navigator.clipboard.writeText(text);
}

export function confirmAction(message) {
    return window.confirm(message);
}

// sessionStorage (not localStorage) deliberately: survives a page refresh within the same tab, but
// doesn't linger indefinitely across unrelated future visits once the tab closes.
export function saveSessionItem(key, value) {
    sessionStorage.setItem(key, value);
}

export function loadSessionItem(key) {
    return sessionStorage.getItem(key);
}
