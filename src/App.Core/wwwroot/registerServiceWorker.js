// Enregistré uniquement hors Development (voir App.razor) pour ne pas cacher le hot-reload.
if ("serviceWorker" in navigator) {
    window.addEventListener("load", () => {
        navigator.serviceWorker.register("/service-worker.js").catch((error) => {
            console.error("Enregistrement du service worker impossible.", error);
        });
    });
}
