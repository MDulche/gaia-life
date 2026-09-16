window.gaiaTheme = (function () {
    var key = "gaia-theme";

    function get() {
        return document.documentElement.getAttribute("data-theme") === "dark" ? "dark" : "light";
    }

    function set(mode) {
        var dark = mode === "dark";
        if (dark) {
            document.documentElement.setAttribute("data-theme", "dark");
        } else {
            document.documentElement.removeAttribute("data-theme");
        }

        try {
            localStorage.setItem(key, dark ? "dark" : "light");
        } catch (e) {
            /* ignore */
        }

        return get();
    }

    function toggle() {
        return set(get() === "dark" ? "light" : "dark");
    }

    return { get: get, set: set, toggle: toggle };
})();
