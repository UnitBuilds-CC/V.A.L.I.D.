(function () {
    let statePointer = 0;
    let slabLength = 0;
    let isInitialized = false;
    let maxSlabIndex = 0;
    let bypassEnabled = true;
    let fps = 0;
    let lastFrameTime = performance.now();
    let frameCount = 0;
    let cachedBuffer = null;
    let cachedHeap32 = null;

    function getWasmHeap() {
        if (globalThis.Module && globalThis.Module.HEAPU8) {
            return globalThis.Module.HEAPU8;
        }
        if (globalThis.dotnet && globalThis.dotnet.Module && globalThis.dotnet.Module.HEAPU8) {
            return globalThis.dotnet.Module.HEAPU8;
        }
        if (globalThis.dotnet && globalThis.dotnet.instance && globalThis.dotnet.instance.Module && globalThis.dotnet.instance.Module.HEAPU8) {
            return globalThis.dotnet.instance.Module.HEAPU8;
        }
        for (const key of Object.keys(globalThis)) {
            if (key.includes("dotnet") || key.includes("Dotnet") || key.includes("DotNet")) {
                const val = globalThis[key];
                if (val && val.Module && val.Module.HEAPU8) {
                    return val.Module.HEAPU8;
                }
            }
        }
        return null;
    }

    function getHeapResolutionInfo() {
        if (globalThis.Module && globalThis.Module.HEAPU8) {
            return "globalThis.Module";
        }
        if (globalThis.dotnet && globalThis.dotnet.Module && globalThis.dotnet.Module.HEAPU8) {
            return "globalThis.dotnet.Module";
        }
        if (globalThis.dotnet && globalThis.dotnet.instance && globalThis.dotnet.instance.Module && globalThis.dotnet.instance.Module.HEAPU8) {
            return "globalThis.dotnet.instance.Module";
        }
        for (const key of Object.keys(globalThis)) {
            if (key.includes("dotnet") || key.includes("Dotnet") || key.includes("DotNet")) {
                const val = globalThis[key];
                if (val && val.Module && val.Module.HEAPU8) {
                    return `globalThis.${key}.Module`;
                }
            }
        }
        return "Not Resolved";
    }

    window.__VAVID_INITIALIZE_INDUSTRIAL_BYPASS__ = function(ptr, len) {
        statePointer = ptr;
        slabLength = len;
        
        const heap = getWasmHeap();
        if (!heap) {
            console.error("[VAVID] Slab pushed but HEAPU8 not found!");
        } else {
            console.log(`[VAVID] Bypass active: 0x${statePointer.toString(16)} via ${getHeapResolutionInfo()}`);
        }
        
        if (!isInitialized) {
            isInitialized = true;
            startBackgroundInspector();
            requestAnimationFrame(updateLoop);
        }
    };

    window.__VAVID_SET_BYPASS_ENABLED__ = function(enabled) {
        bypassEnabled = enabled;
        console.log(`[VAVID] Bypass: ${enabled ? 'ENABLED' : 'DISABLED'}`);
    };

    window.__VAVID_SET_MAX_SLAB_INDEX__ = function(val) {
        maxSlabIndex = val;
        console.log(`[VAVID] Max Slab Index: ${maxSlabIndex}`);
    };

    window.__VAVID_GET_TELEMETRY__ = function() {
        const heap = getWasmHeap();
        let activeCount = 0;
        if (heap && statePointer && maxSlabIndex > 0) {
            if (cachedBuffer !== heap.buffer || !cachedHeap32) {
                cachedBuffer = heap.buffer;
                cachedHeap32 = new Int32Array(heap.buffer);
            }
            const heap32 = cachedHeap32;
            for (let i = 1; i <= maxSlabIndex; i++) {
                const ptr = statePointer + i * 64;
                const isDirty = heap32[ptr / 4] !== 0;
                const isBusy = heap32[(ptr + 16) / 4] !== 0;
                const hasError = heap32[(ptr + 32) / 4] !== 0;
                const depVal = heap32[(ptr + 48) / 4] || 0;
                const payVal = heap32[(ptr + 52) / 4] || 0;
                if (isDirty || isBusy || hasError || depVal > 0 || payVal > 0) {
                    activeCount++;
                }
            }
        }
        return {
            fps: Math.round(fps),
            activeRows: activeCount || document.querySelectorAll('.cashbook-row').length,
            enabled: bypassEnabled,
            hasModule: !!(globalThis.Module || globalThis.dotnet?.Module),
            hasHeap: !!heap,
            heapResolver: getHeapResolutionInfo()
        };
    };

    function startBackgroundInspector() {
        setInterval(() => {
            const h = getWasmHeap();
            if (!statePointer || !h) return;
            
            const magic = 0x42;
            let match = true;
            for (let i = 0; i < 16; i++) {
                if (h[statePointer + i] !== magic) { match = false; break; }
            }

            if (!match) {
                console.warn("[VAVID] Pulse lost. Attempting recovery...");
                const foundOffset = findMagicPulse(h);
                if (foundOffset !== -1) {
                    console.log(`[VAVID] Pulse recovered at 0x${foundOffset.toString(16)}`);
                    statePointer = foundOffset;
                }
            }
        }, 1000);
    }

    function findMagicPulse(heap) {
        const magic = 0x42;
        const limit = Math.min(heap.length, 64 * 1024 * 1024);
        for (let i = 0; i < limit; i += 16) {
            if (heap[i] === magic && heap[i+1] === magic) {
                let match = true;
                for (let j = 0; j < 16; j++) { if (heap[i+j] !== magic) { match = false; break; } }
                if (match) return i;
            }
        }
        return -1;
    }

    function updateLoop() {
        if (bypassEnabled) {
            syncSurgicalMap();
        }

        frameCount++;
        const now = performance.now();
        if (now - lastFrameTime > 1000) {
            fps = (frameCount * 1000) / (now - lastFrameTime);
            frameCount = 0;
            lastFrameTime = now;
        }

        requestAnimationFrame(updateLoop);
    }

    function syncSurgicalMap() {
        const h = getWasmHeap();
        if (!h || !statePointer) return;

        if (cachedBuffer !== h.buffer || !cachedHeap32) {
            cachedBuffer = h.buffer;
            cachedHeap32 = new Int32Array(h.buffer);
        }
        const heap32 = cachedHeap32;
        const base = statePointer;
        
        const rows = document.querySelectorAll('.cashbook-row');
        const len = rows.length;

        let totalDeposits = 0;
        let totalPayments = 0;

        for (let i = 0; i < len; i++) {
            const row = rows[i];
            const slabIndexAttr = row.getAttribute('data-vavid-slab-index');
            if (!slabIndexAttr) continue;
            
            const slabIndex = parseInt(slabIndexAttr);
            if (slabIndex <= 0) continue;

            if (row._lastSlabIndex !== slabIndex) {
                row._cachedInputs = null;
                row._lastState = null;
                row._lastSlabIndex = slabIndex;
            }

            const ptr = base + slabIndex * 64;
            
            const bitByte = 0;
            const bitMask = 1;
            const isDirty = (h[ptr + bitByte] & bitMask) !== 0;
            const isBusy = (h[ptr + 16 + bitByte] & bitMask) !== 0;
            const hasError = (h[ptr + 32 + bitByte] & bitMask) !== 0;

            const currentState = (isDirty ? 1 : 0) | (isBusy ? 2 : 0) | (hasError ? 4 : 0);
            
            if (row._lastState !== currentState) {
                if (isDirty) row.classList.add('vavid-dirty'); else row.classList.remove('vavid-dirty');
                if (isBusy) row.classList.add('vavid-busy'); else row.classList.remove('vavid-busy');
                if (hasError) row.classList.add('vavid-error'); else row.classList.remove('vavid-error');
                row._lastState = currentState;
            }

            if (!row._cachedInputs) {
                row._cachedInputs = Array.from(row.querySelectorAll('[data-vavid-offset]')).map(nel => ({
                    el: nel,
                    offset: parseInt(nel.getAttribute('data-vavid-offset')),
                    type: nel.tagName === 'INPUT' ? 'value' : 'text'
                }));
            }

            const inputs = row._cachedInputs;
            for (let j = 0; j < inputs.length; j++) {
                const input = inputs[j];
                const val = heap32[(ptr + input.offset) / 4];
                if (input.type === 'value') {
                    if (input.el.value != val) input.el.value = val;
                } else {
                    if (input.el.textContent != val) input.el.textContent = val;
                }
            }
        }

        for (let i = 1; i <= maxSlabIndex; i++) {
            const ptr = base + i * 64;
            const depVal = heap32[(ptr + 48) / 4] || 0;
            const payVal = heap32[(ptr + 52) / 4] || 0;
            totalDeposits += depVal;
            totalPayments += payVal;
        }

        const elTotalDep = document.getElementById('total-actual-deposits');
        const elTotalPay = document.getElementById('total-actual-payments');
        const elTargetDep = document.getElementById('target-deposits-input');
        const elTargetPay = document.getElementById('target-payments-input');
        const elDiffDep = document.getElementById('total-diff-deposits');
        const elDiffPay = document.getElementById('total-diff-payments');

        let targetDep = 0;
        let targetPay = 0;
        if (elTargetDep) targetDep = parseFloat(elTargetDep.value) || 0;
        if (elTargetPay) targetPay = parseFloat(elTargetPay.value) || 0;

        if (elTotalDep) {
            const formatted = totalDeposits.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
            if (elTotalDep.textContent !== formatted) elTotalDep.textContent = formatted;
        }
        if (elTotalPay) {
            const formatted = totalPayments.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
            if (elTotalPay.textContent !== formatted) elTotalPay.textContent = formatted;
        }

        if (elDiffDep) {
            const diffDep = totalDeposits - targetDep;
            const formatted = diffDep.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
            if (elDiffDep.textContent !== formatted) elDiffDep.textContent = formatted;
            
            if (Math.abs(diffDep) < 0.01) {
                elDiffDep.style.color = '#16a34a';
                if (elTotalDep) elTotalDep.style.color = '#16a34a';
            } else {
                elDiffDep.style.color = '#ef4444';
                if (elTotalDep) elTotalDep.style.color = '#d97706';
            }
        }
        if (elDiffPay) {
            const diffPay = totalPayments - targetPay;
            const formatted = diffPay.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
            if (elDiffPay.textContent !== formatted) elDiffPay.textContent = formatted;
            
            if (Math.abs(diffPay) < 0.01) {
                elDiffPay.style.color = '#16a34a';
                if (elTotalPay) elTotalPay.style.color = '#16a34a';
            } else {
                elDiffPay.style.color = '#ef4444';
                if (elTotalPay) elTotalPay.style.color = '#d97706';
            }
        }
    }
})();
