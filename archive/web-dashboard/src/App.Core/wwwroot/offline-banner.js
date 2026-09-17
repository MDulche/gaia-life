(() => {
    const banner = document.getElementById("gaia-offline-banner");
    if (!banner) {
        return;
    }

    const sync = () => {
        banner.hidden = navigator.onLine;
    };

    window.addEventListener("offline", sync);
    window.addEventListener("online", sync);
    sync();
})();
