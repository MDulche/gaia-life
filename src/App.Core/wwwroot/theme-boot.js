/* Applique le thème avant le premier paint (évite le flash clair/sombre). */
(function () {
    try {
        var key = "gaia-theme";
        var stored = localStorage.getItem(key);
        var dark = stored === "dark"
            || (stored !== "light" && window.matchMedia("(prefers-color-scheme: dark)").matches);
        if (dark) {
            document.documentElement.setAttribute("data-theme", "dark");
        } else {
            document.documentElement.removeAttribute("data-theme");
        }
    } catch (e) {
        /* localStorage indisponible : mode clair par défaut. */
    }
})();
