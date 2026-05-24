// vavid-bypass.js - MISSION 27.2.9: Surgical Reconstruction (OOM & Performance Fix)
(function () {
    let statePointer = 0;
    let slabLength = 0;
    let isInitialized = false;
    let surgicalMap = []; // [{el, offset, bitByte, bitInByte}]
    let bypassEnabled = true;
    let fps = 0;
    let lastFrameTime = performance.now();
    let frameCount = 0;
    let cachedBuffer = null;
    let cachedHeap32 = null;

    // MISSION 27.2.3: The Global Push Entry Point
    window.__VAVID_INITIALIZE_INDUSTRIAL_BYPASS__ = function(ptr, len) {
        // Allow re-initialization to support navigation/hot-reload
        statePointer = ptr;
        slabLength = len;
        
        const heap = globalThis.Module ? globalThis.Module.HEAPU8 : null;
        if (!heap) {
            console.error("[VAVID] Industrial Engine: Slab pushed but Module.HEAPU8 not found!");
            return;
        }

        console.log(`[VAVID] Industrial Bypass active. Pointer: 0x${statePointer.toString(16)}`);
        
        // MISSION 27.2.9: Build Surgical Map
        rebuildSurgicalMap();

        // If no fields found, set a retry (Blazor might still be rendering 5k rows)
        if (surgicalMap.length === 0) {
            console.warn("[VAVID] No fields found on first pass. Scheduling background discovery...");
            const retryInterval = setInterval(() => {
                rebuildSurgicalMap();
                if (surgicalMap.length > 0) {
                    console.log("[VAVID] Background discovery Successful.");
                    clearInterval(retryInterval);
                }
            }, 1000);
        }

        if (!isInitialized) {
            isInitialized = true;
            startBackgroundInspector();
            requestAnimationFrame(updateLoop);
        }
    };

    window.__VAVID_SET_BYPASS_ENABLED__ = function(enabled) {
        bypassEnabled = enabled;
        console.log(`[VAVID] Bypass Engine: ${enabled ? 'ENABLED' : 'DISABLED'}`);
    };

    window.__VAVID_GET_TELEMETRY__ = function() {
        return {
            fps: Math.round(fps),
            activeRows: surgicalMap.length,
            enabled: bypassEnabled
        };
    };

    function rebuildSurgicalMap() {
        console.info("[VAVID] Building Surgical Map...");
        const rows = document.querySelectorAll('.benchmark-row');
        surgicalMap = [];

        for (let i = 0; i < rows.length; i++) {
            const row = rows[i];
            const slabIndexEl = row.querySelector('[data-vavid-slab-index]');
            if (!slabIndexEl) continue;

            const slabIndex = parseInt(slabIndexEl.getAttribute('data-vavid-slab-index'));
            if (slabIndex <= 0) continue;

            // Primary control field (for bitmask)
            const rowEntry = {
                el: slabIndexEl,
                offset: slabIndex * 64,
                bitByte: Math.floor(parseInt(slabIndexEl.getAttribute('data-vavid-bit') || "0") / 8),
                bitMask: 1 << (parseInt(slabIndexEl.getAttribute('data-vavid-bit') || "0") % 8),
                lastState: -1,
                numericFields: []
            };

            // Discover numeric fields within this row
            const numericEls = row.querySelectorAll('[data-vavid-offset]');
            numericEls.forEach(nel => {
                rowEntry.numericFields.push({
                    el: nel,
                    offset: parseInt(nel.getAttribute('data-vavid-offset')),
                    type: nel.tagName === 'INPUT' ? 'value' : 'text'
                });
            });

            surgicalMap.push(rowEntry);
        }
        console.log(`[VAVID] Surgical Map complete: ${surgicalMap.length} rows with numeric sync.`);
    }

    function startBackgroundInspector() {
        // MISSION 27.2.6: THE MEMORY INSPECTOR (1Hz Heartbeat)
        setInterval(() => {
            if (!statePointer || !globalThis.Module?.HEAPU8) return;
            const h = globalThis.Module.HEAPU8;
            
            // Validate Pulse Header (Slab 0)
            const magic = 0x42;
            let match = true;
            for (let i = 0; i < 16; i++) {
                if (h[statePointer + i] !== magic) { match = false; break; }
            }

            if (!match) {
                console.warn("[VAVID] PULSE LOST. Attempting Search & Rescue...");
                const foundOffset = findMagicPulse(h);
                if (foundOffset !== -1) {
                    console.log(`[VAVID] PULSE RECOVERED at 0x${foundOffset.toString(16)}`);
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

        // FPS Calculation
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
        const h = globalThis.Module ? globalThis.Module.HEAPU8 : null;
        if (!h || !statePointer) return;

        if (cachedBuffer !== h.buffer || !cachedHeap32) {
            cachedBuffer = h.buffer;
            cachedHeap32 = new Int32Array(h.buffer);
        }
        const heap32 = cachedHeap32;
        const base = statePointer;
        const len = surgicalMap.length;

        for (let i = 0; i < len; i++) {
            const entry = surgicalMap[i];
            const ptr = base + entry.offset;
            
            // 1. Bitmask State (Slot 0-2)
            const isDirty = (h[ptr + entry.bitByte] & entry.bitMask) !== 0;
            const isBusy = (h[ptr + 16 + entry.bitByte] & entry.bitMask) !== 0;
            const hasError = (h[ptr + 32 + entry.bitByte] & entry.bitMask) !== 0;

            const currentState = (isDirty ? 1 : 0) | (isBusy ? 2 : 0) | (hasError ? 4 : 0);
            
            if (currentState !== entry.lastState) {
                const el = entry.el;
                if (isDirty) el.classList.add('vavid-dirty'); else el.classList.remove('vavid-dirty');
                if (isBusy) el.classList.add('vavid-busy'); else el.classList.remove('vavid-busy');
                if (hasError) el.classList.add('vavid-error'); else el.classList.remove('vavid-error');
                entry.lastState = currentState;
            }

            // 2. Numeric Sync (Slot 3: Bytes 48-63)
            if (entry.numericFields) {
                for (let j = 0; j < entry.numericFields.length; j++) {
                    const field = entry.numericFields[j];
                    // Pointer is in bytes, heap32 index is in 4-byte words
                    const val = heap32[(ptr + field.offset) / 4];
                    
                    if (field.type === 'value') {
                        if (field.el.value != val) field.el.value = val;
                    } else {
                        if (field.el.textContent != val) field.el.textContent = val;
                    }
                }
            }
        }
    }

    console.log("[VAVID] Surgical Bridge Loaded. Awaiting Global Push...");
})();
