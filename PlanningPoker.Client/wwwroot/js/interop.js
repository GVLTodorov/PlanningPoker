export function focusElement(selector) {
    const element = document.querySelector(selector);
    element?.focus();
}

export function copyToClipboard(text) {
    return navigator.clipboard.writeText(text);
}
