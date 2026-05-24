class VavidBridge {
    constructor() {
        this.objects = new Map();
        this.dotNetRefs = new WeakMap();
        window.__VALID_OBJECTS__ = [];
        this.deltas = []; 
        this.elementCache = new Map(); 
        this.observer = new MutationObserver(() => {
            this.elementCache.clear();
        });
        this.observer.observe(document.body, { childList: true, subtree: true });
        console.log("VAVID Bridge Initialized.");
    }

    registerObject(id, metadataJson, dotNetRef) {
        try {
            const metadata = JSON.parse(metadataJson);
            const obj = { 
                id, 
                metadata, 
                flags: { dirty: 0n, busy: 0n, error: 0n },
                history: [],
                lastUpdate: null
            };
            
            if (!window.__VALID_OBJECTS__.find(o => o.id === id)) {
                this.dotNetRefs.set(obj, dotNetRef);
                window.__VALID_OBJECTS__.push(obj);
                this.refreshElementCache(id);
            }
        } catch (e) {
            console.error("Failed to register object:", e);
        }
    }

    refreshElementCache(id) {
        const elements = Array.from(document.querySelectorAll(`[vavid-obj="${id}"]`));
        this.elementCache.set(id, elements.filter(el => el.hasAttribute('vavid-bit')).map(el => ({
            el,
            bit: BigInt(el.getAttribute('vavid-bit'))
        })));
    }

    unregisterObject(id) {
        window.__VALID_OBJECTS__ = window.__VALID_OBJECTS__.filter(o => o.id !== id);
        this.elementCache.delete(id);
    }

    updateState(id, dirtyHex, busyHex, errorHex) {
        const startTime = performance.now();
        const obj = window.__VALID_OBJECTS__.find(o => o.id === id);

        const toBigInt = (hex) => {
            if (!hex || typeof hex !== 'string' || hex.trim() === '') return 0n;
            try {
                const cleanHex = hex.replace(/^0x/i, '');
                if (cleanHex === '') return 0n;
                return BigInt("0x" + cleanHex);
            } catch (e) {
                return 0n;
            }
        };

        const dirty = toBigInt(dirtyHex);
        const busy = toBigInt(busyHex);
        const error = toBigInt(errorHex);

        if (obj) {
            const newState = { dirty, busy, error, timestamp: Date.now() };
            obj.flags = newState;
            obj.history.push(newState);
            if (obj.history.length > 100) obj.history.shift();
        }

        let cached = this.elementCache.get(id);
        if (!cached || cached.length === 0) {
            this.refreshElementCache(id);
            cached = this.elementCache.get(id);
        }

        if (cached) {
            cached.forEach(entry => {
                const el = entry.el;
                const bit = entry.bit;
                const mask = 1n << bit;

                if ((dirty & mask) !== 0n) {
                    el.classList.add('vavid-dirty');
                } else {
                    el.classList.remove('vavid-dirty');
                }

                if ((busy & mask) !== 0n) {
                    el.classList.add('vavid-busy');
                } else {
                    el.classList.remove('vavid-busy');
                }

                if ((error & mask) !== 0n) {
                    el.classList.add('vavid-error');
                    if (!el._lastErrorTime || Date.now() - el._lastErrorTime > 5000) {
                        const propMeta = obj.metadata.Properties?.find(p => p.BitIndex === Number(bit));
                        const propName = propMeta ? propMeta.Name : 'UNKNOWN';
                        this.showSecurityToast(id, propName);
                        el._lastErrorTime = Date.now();
                        window.postMessage({ source: 'vavid-extension-bridge', type: 'vavid-security-violation', detail: { id, property: propName, timestamp: Date.now() } }, "*");
                    }
                } else {
                    el.classList.remove('vavid-error');
                }
            });
        }

        const latency = performance.now() - startTime;
        if (obj) obj.lastLatency = latency;

        window.postMessage({ source: 'vavid-extension-bridge', type: 'vavid-telemetry-pulse', detail: { id, latency } }, "*");
    }

    showSecurityToast(id, property) {
        let container = document.getElementById('vavid-toast-container');
        if (!container) {
            container = document.createElement('div');
            container.id = 'vavid-toast-container';
            container.style = "position: fixed; bottom: 20px; right: 20px; z-index: 9999; display: flex; flex-direction: column-reverse; gap: 10px;";
            document.body.appendChild(container);
        }

        const toast = document.createElement('div');
        toast.className = 'vavid-security-toast';
        toast.style = "background: rgba(127, 0, 0, 0.9); border: 1px solid #ff4444; color: #fff; padding: 12px 20px; border-radius: 4px; font-family: monospace; font-size: 11px; box-shadow: 0 0 20px rgba(255,0,0,0.4); cursor: pointer;";
        toast.innerHTML = `<span style="color: #ff4444; font-weight: bold;">[SECURITY_VIOLATION]</span><br/>Object: ${id}<br/>Property: ${property || 'UNKNOWN'}`;
        
        toast.onclick = () => toast.remove();
        container.appendChild(toast);

        setTimeout(() => {
            toast.style.opacity = '0';
            toast.style.transform = 'translateX(100px)';
            toast.style.transition = 'all 0.5s ease-in';
            setTimeout(() => toast.remove(), 500);
        }, 4000);
    }

    logDelta(id, json) {
        const delta = JSON.parse(json);
        const logEntry = { id, json, timestamp: Date.now() };
        this.deltas.unshift(logEntry);
        if (this.deltas.length > 50) this.deltas.pop();

        const obj = window.__VALID_OBJECTS__.find(o => o.id === id);
        const rootElements = document.querySelectorAll(`[vavid-obj="${id}"]`);
        rootElements.forEach(root => {
            for (const [prop, value] of Object.entries(delta)) {
                let target = null;
                if (obj && obj.metadata && obj.metadata.Properties) {
                    const propMeta = obj.metadata.Properties.find(p => p.Name === prop);
                    if (propMeta) {
                        target = root.querySelector(`[vavid-bit="${propMeta.BitIndex}"]`);
                    }
                }
                
                if (target) {
                    if (typeof value === 'boolean') {
                        if (target.hasAttribute('vavid-bool')) {
                            target.innerText = value ? "✓" : "○";
                            if (value) {
                                target.classList.add('text-green-400');
                                target.classList.remove('text-yellow-400');
                            } else {
                                target.classList.add('text-yellow-400');
                                target.classList.remove('text-green-400');
                            }
                        } else {
                            target.innerText = value.toString();
                        }
                    } else if (typeof value === 'number') {
                        const format = target.getAttribute('vavid-format');
                        if (format === 'currency') {
                            target.innerText = "$" + value.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
                        } else {
                            target.innerText = value.toString();
                        }
                    } else {
                        target.innerText = value;
                    }

                    target.classList.add('vavid-pulse-active');
                    setTimeout(() => target.classList.remove('vavid-pulse-active'), 500);
                }
            }
        });

        window.postMessage({ source: 'vavid-extension-bridge', type: 'vavid-delta-logged', detail: logEntry }, "*");
    }

    pushValue(id, propertyName, value) {
        const obj = window.__VALID_OBJECTS__.find(o => o.id === id);
        const ref = obj ? this.dotNetRefs.get(obj) : null;
        if (ref) {
            ref.invokeMethodAsync('UpdateProperty', propertyName, value.toString());
        }
    }

    restoreHistory(id, stepsBack) {
        const obj = window.__VALID_OBJECTS__.find(o => o.id === id);
        const ref = obj ? this.dotNetRefs.get(obj) : null;
        if (ref) {
            ref.invokeMethodAsync('RestoreHistory', stepsBack);
        }
    }

    triggerChaos() {
        console.warn("GLOBAL CHAOS TRIGGERED");
        window.__VALID_OBJECTS__.forEach(obj => {
            const r128 = () => (BigInt(Math.floor(Math.random() * 0xFFFFFFFF)) << 96n) | 
                               (BigInt(Math.floor(Math.random() * 0xFFFFFFFF)) << 64n) |
                               (BigInt(Math.floor(Math.random() * 0xFFFFFFFF)) << 32n) |
                                BigInt(Math.floor(Math.random() * 0xFFFFFFFF));
            
            const mockDirty = r128();
            const mockBusy = r128();
            this.updateState(obj.id, mockDirty.toString(16), mockBusy.toString(16), "0");
        });
    }
}

window.vavid = new VavidBridge();
