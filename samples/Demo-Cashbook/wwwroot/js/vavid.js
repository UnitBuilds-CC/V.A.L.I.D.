class VavidBridge {
    constructor() {
        this.objects = new Map();
        console.log("VAVID 3.0.0 Bridge Initialized.");
    }

    registerObject(id, metadata, dotNetRef) {
        this.objects.set(id, { metadata, dotNetRef });
        console.log(`[VAVID] Registered object: ${id}`);
    }

    unregisterObject(id) {
        this.objects.delete(id);
        console.log(`[VAVID] Unregistered object: ${id}`);
    }

    logDelta(id, deltaJson) {
        console.debug(`[VAVID] Delta for ${id}:`, deltaJson);
    }

    updateState(id, dirtyStr, busyStr, errorStr) {
        const dirty = BigInt("0x" + dirtyStr);
        const busy = BigInt("0x" + busyStr);
        const error = BigInt("0x" + errorStr);

        const elements = document.querySelectorAll(`[vavid-obj="${id}"]`);
        elements.forEach(el => {
            const bitAttr = el.getAttribute('vavid-bit');
            if (!bitAttr) return;
            const bit = BigInt(bitAttr);
            
            const isDirty = (dirty & (1n << bit)) !== 0n;
            const isBusy = (busy & (1n << bit)) !== 0n;
            const isError = (error & (1n << bit)) !== 0n;

            el.classList.toggle('vavid-dirty', isDirty);
            el.classList.toggle('vavid-busy', isBusy);
            el.classList.toggle('vavid-error', isError);

            if (el.tagName === 'INPUT' || el.tagName === 'SELECT') {
                if (isError) {
                    el.style.boxShadow = '0 0 10px rgba(239, 68, 68, 0.5)';
                    el.style.borderColor = '#ef4444';
                } else if (isDirty) {
                    el.style.boxShadow = '0 0 10px rgba(245, 158, 11, 0.5)';
                    el.style.borderColor = '#f59e0b';
                } else {
                    el.style.boxShadow = '';
                    el.style.borderColor = '';
                }
            }
        });
    }
}

window.vavid = new VavidBridge();
