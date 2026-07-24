window.devNote = {
    scrollToResultAnchor: function (elementId) {
        const el = document.getElementById(elementId);
        if (el) {
            el.scrollIntoView({ behavior: 'smooth', block: 'start' });
        }
    }
};
