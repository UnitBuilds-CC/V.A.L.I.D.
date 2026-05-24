(function() {
    let lastKnownCount = -1;

    function discoverValidObjects() {
        if (window.__VALID_OBJECTS__) {
            if (window.__VALID_OBJECTS__.length !== lastKnownCount) {
                lastKnownCount = window.__VALID_OBJECTS__.length;
                
                const serializableObjects = window.__VALID_OBJECTS__.map(o => {
                    return {
                        id: o.id,
                        metadata: o.metadata,
                        flags: {
                            dirty: (o.flags.dirty || 0n).toString(),
                            busy: (o.flags.busy || 0n).toString(),
                            error: (o.flags.error || 0n).toString()
                        },
                        history: (o.history || []).map(h => ({
                            dirty: (h.dirty || 0n).toString(),
                            busy: (h.busy || 0n).toString(),
                            error: (h.error || 0n).toString(),
                            timestamp: h.timestamp
                        })),
                        lastLatency: o.lastLatency || 0,
                        lastUpdate: o.lastUpdate || null
                    };
                });

                try {
                    window.postMessage({ 
                        source: 'vavid-extension-bridge', 
                        type: 'vavid-ext-discovery', 
                        detail: serializableObjects 
                    }, "*");
                } catch (e) {
                    console.error("Discovery postMessage failed:", e);
                }
            }
        }
    }

    window.addEventListener('message', (event) => {
        if (event.source !== window) return;
        const data = event.data;
        if (!data || data.source !== 'vavid-extension-content') return;

        if (data.type === 'vavid-discovery-request') {
            lastKnownCount = -1;
            discoverValidObjects();
        }
    });

    if (window.__VAVID_BRIDGE_INTERVAL__) {
        clearInterval(window.__VAVID_BRIDGE_INTERVAL__);
    }

    window.__VAVID_BRIDGE_INTERVAL__ = setInterval(discoverValidObjects, 1000);
})();
