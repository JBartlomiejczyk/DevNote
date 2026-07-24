window.devNote = {
    scrollToResultAnchor: function (elementId) {
        const el = document.getElementById(elementId);
        if (el) {
            el.scrollIntoView({ behavior: 'smooth', block: 'start' });
        }
    }
};

function initLucide() {
    if (window.lucide) window.lucide.createIcons();
}
document.addEventListener('DOMContentLoaded', initLucide);
document.addEventListener('blazor:navigated', initLucide);
