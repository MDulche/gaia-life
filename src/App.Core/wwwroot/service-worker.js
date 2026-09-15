// Cache des assets statiques uniquement. Les pages Blazor Server / SignalR
// ne sont jamais mises en cache : hors ligne, on affiche offline.html.
const CACHE_NAME = "gaia-life-static-v1";
const OFFLINE_URL = "/offline.html";
const PRECACHE = [
    "/offline.html",
    "/manifest.json",
    "/favicon.png",
    "/icons/icon-192.png",
    "/icons/icon-512.png",
    "/icons/icon-512-maskable.png",
    "/icons/icon-180.png",
    "/app.css",
    "/lib/bootstrap/dist/css/bootstrap.min.css"
];

self.addEventListener("install", (event) => {
    event.waitUntil(
        caches.open(CACHE_NAME).then((cache) => cache.addAll(PRECACHE)).then(() => self.skipWaiting())
    );
});

self.addEventListener("activate", (event) => {
    event.waitUntil(
        caches.keys().then((keys) =>
            Promise.all(keys.filter((key) => key !== CACHE_NAME).map((key) => caches.delete(key)))
        ).then(() => self.clients.claim())
    );
});

function isBypass(request, url) {
    if (request.method !== "GET") {
        return true;
    }

    if (url.origin !== self.location.origin) {
        return true;
    }

    const path = url.pathname;
    return path.startsWith("/_blazor")
        || path.startsWith("/_framework")
        || path.startsWith("/health")
        || path.startsWith("/Account")
        || request.headers.get("upgrade") === "websocket";
}

async function networkFirst(request) {
    const cache = await caches.open(CACHE_NAME);
    try {
        const response = await fetch(request);
        if (response.ok) {
            cache.put(request, response.clone());
        }
        return response;
    } catch (error) {
        const cached = await cache.match(request);
        if (cached) {
            return cached;
        }
        throw error;
    }
}

self.addEventListener("fetch", (event) => {
    const request = event.request;
    const url = new URL(request.url);

    if (request.mode === "navigate") {
        event.respondWith(
            fetch(request).catch(() => caches.match(OFFLINE_URL).then((cached) => cached || Response.error()))
        );
        return;
    }

    if (isBypass(request, url)) {
        return;
    }

    event.respondWith(networkFirst(request));
});
