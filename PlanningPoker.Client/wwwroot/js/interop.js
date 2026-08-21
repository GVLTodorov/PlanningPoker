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
