window.devNote = window.devNote || {};
window.devNote.scrollToResultAnchor = function (id) {
    document.getElementById(id)?.scrollIntoView({ behavior: "smooth" });
};
