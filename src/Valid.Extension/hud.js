(function() {
    let activeObjectId = null;
    let historyIndex = -1;
    let activeTab = 'xray';
    let isInspectMode = false;
    let latencyHistory = [];
    let valueHistory = {};

    const STORAGE_KEY = 'vavid-hud-visible';
    let isVisible = localStorage.getItem(STORAGE_KEY) !== 'false';

    window.addEventListener('vavid-ext-discovery', (e) => {
        const objects = e.detail;
        objects.forEach(obj => attachHUD(obj));
    });

    window.addEventListener('vavid-telemetry-pulse', (e) => {
        if (activeObjectId === e.detail.id && activeTab === 'perf') {
            updatePerfDisplay(e.detail.latency);
        }
    });

    window.addEventListener('vavid-delta-logged', (e) => {
        const delta = JSON.parse(e.detail.json);
        const objId = e.detail.id;
        
        if (!valueHistory[objId]) valueHistory[objId] = {};
        
        for (const [prop, val] of Object.entries(delta)) {
            if (!valueHistory[objId][prop]) valueHistory[objId][prop] = [];
            valueHistory[objId][prop].unshift({ val, time: Date.now() });
            if (valueHistory[objId][prop].length > 10) valueHistory[objId][prop].pop();
        }

        if (isVisible && (activeTab === 'deltas' || (activeTab === 'xray' && activeObjectId === objId))) {
            renderParadiseHUD(null);
        }
    });

    window.addEventListener('vavid-security-violation', (e) => {
        if (activeObjectId === e.detail.id) {
            triggerSecurityFlash();
            if (isVisible && (activeTab === 'security' || activeTab === 'xray')) {
                renderParadiseHUD(null);
            }
        }
    });

    function triggerSecurityFlash() {
        const sidebar = document.getElementById('vavid-extension-hud');
        if (sidebar) {
            sidebar.classList.add('vavid-security-flash');
            setTimeout(() => sidebar.classList.remove('vavid-security-flash'), 1000);
        }
    }

    document.addEventListener('mouseover', (e) => {
        if (isInspectMode) return;
        const target = e.target.closest('[vavid-obj]');
        if (target) {
            const objId = target.getAttribute('vavid-obj');
            if (activeObjectId !== objId) {
                activeObjectId = objId;
                if (isVisible) renderParadiseHUD(target.getAttribute('vavid-bit'));
            }
        }
    });

    document.addEventListener('mouseout', (e) => {
        if (isInspectMode) return;
        const target = e.target.closest('[vavid-obj]');
        if (target) {
            const sidebar = document.getElementById('vavid-extension-hud');
            if (sidebar && isVisible) sidebar.style.opacity = '0.9';
        }
    });

    document.addEventListener('mousedown', (e) => {
        if (!isInspectMode) return;
        
        const target = e.target.closest('[vavid-obj]');
        if (target) {
            e.preventDefault();
            e.stopPropagation();
            const objId = target.getAttribute('vavid-obj');
            activeObjectId = objId;
            isInspectMode = false;
            isVisible = true;
            localStorage.setItem(STORAGE_KEY, 'true');
            updateToggleState();
            renderParadiseHUD(target.getAttribute('vavid-bit'));
        }
    }, true);

    function renderParadiseHUD(focusBitIndex) {
        if (!isVisible) return;
        
        const obj = (window.__VALID_OBJECTS__ || []).find(o => o.id === activeObjectId);
        if (!obj) {
            const sidebar = getOrCreateSidebar();
            sidebar.innerHTML = `
                <div style="text-align: center; padding: 40px 20px; opacity: 0.5;">
                    <div style="font-size: 24px; margin-bottom: 10px;">🛰️</div>
                    <div style="font-size: 10px; letter-spacing: 1px;">AWAITING_OBJECT_SELECTION...</div>
                    <div style="font-size: 8px; margin-top: 10px; opacity: 0.5;">Hover over a VALID component or use INSPECT</div>
                </div>
                <div style="border-top: 1px solid rgba(0,255,255,0.1); padding-top: 15px; margin-top: 15px; display: flex; justify-content: center;">
                     <button id="vavid-inspect-btn-empty" onclick="window.vavidExt.toggleInspect()" style="background: transparent; border: 1px solid #00ffff; color: #00ffff; font-size: 10px; padding: 4px 10px; cursor: pointer; border-radius: 2px;">ACTIVATE_INSPECTOR</button>
                </div>
            `;
            sidebar.style.display = 'block';
            sidebar.style.opacity = '1';
            return;
        }

        const sidebar = getOrCreateSidebar();
        
        const currentFlags = obj.flags || { dirty: 0n, busy: 0n, error: 0n };
        const stateKey = `${isVisible}-${activeTab}-${focusBitIndex}-${currentFlags.dirty}-${currentFlags.busy}-${currentFlags.error}-${historyIndex}-${obj.history.length}`;
        if (sidebar.getAttribute('vavid-state-key') === stateKey) return;
        sidebar.setAttribute('vavid-state-key', stateKey);

        const metadata = obj.metadata;
        
        let prop = metadata.Properties.find(p => p.BitIndex == focusBitIndex);
        if (!prop && focusBitIndex === null) {
            const lastFocusedBit = sidebar.getAttribute('vavid-last-bit');
            prop = metadata.Properties.find(p => p.BitIndex == lastFocusedBit) || metadata.Properties[0];
            focusBitIndex = prop.BitIndex;
        } else if (!prop) {
            prop = metadata.Properties[0];
            focusBitIndex = prop.BitIndex;
        }

        sidebar.setAttribute('vavid-last-bit', focusBitIndex);

        let html = `
            <div style="border-bottom: 1px solid rgba(0,255,255,0.2); padding-bottom: 10px; margin-bottom: 15px;">
                <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 8px;">
                    <span class="vavid-ext-axiom-header" style="font-size: 11px; letter-spacing: 2px;">VAVID_HUD_v3.0.7</span>
                    <div style="display: flex; gap: 8px; align-items: center;">
                        <button id="vavid-inspect-btn" onclick="window.vavidExt.toggleInspect()" style="background: ${isInspectMode ? '#00ffff' : 'transparent'}; border: 1px solid #00ffff; color: ${isInspectMode ? '#000' : '#00ffff'}; font-size: 8px; padding: 2px 5px; cursor: pointer; border-radius: 2px; font-weight: bold;">INSPECT</button>
                        <span style="font-size: 9px; opacity: 0.5;">OUTBOX:</span>
                        <div class="vavid-outbox-dot"></div>
                    </div>
                </div>
                <div style="display: flex; gap: 2px;">
                    <button class="vavid-tab-btn ${activeTab === 'xray' ? 'active' : ''}" onclick="window.vavidExt.setTab('xray', '${focusBitIndex}')">X-RAY</button>
                    <button class="vavid-tab-btn ${activeTab === 'security' ? 'active' : ''}" onclick="window.vavidExt.setTab('security', '${focusBitIndex}')">SECURITY</button>
                    <button class="vavid-tab-btn ${activeTab === 'deltas' ? 'active' : ''}" onclick="window.vavidExt.setTab('deltas', '${focusBitIndex}')">DELTAS</button>
                    <button class="vavid-tab-btn ${activeTab === 'perf' ? 'active' : ''}" onclick="window.vavidExt.setTab('perf', '${focusBitIndex}')">PERF</button>
                </div>
            </div>
        `;

        if (activeTab === 'xray') {
            html += renderXRayTab(obj, prop, focusBitIndex);
        } else if (activeTab === 'security') {
            html += renderSecurityTab(obj);
        } else if (activeTab === 'deltas') {
            html += renderDeltasTab();
        } else if (activeTab === 'perf') {
            html += renderPerfTab(obj);
        }

        sidebar.innerHTML = html;
        sidebar.style.display = 'block';
        sidebar.style.opacity = '1';

        if (activeTab === 'xray') {
            const editField = document.getElementById('vavid-edit-field');
            if (editField) {
                editField.onkeydown = (e) => {
                    if (e.key === 'Enter') {
                        let val = editField.value;
                        if (!val.startsWith('"') && !val.startsWith('{') && isNaN(val) && val !== 'true' && val !== 'false') {
                            val = JSON.stringify(val);
                        }
                        window.vavid.pushValue(obj.id, prop.Name, val);
                        editField.value = '';
                        editField.placeholder = "Pushed!";
                        setTimeout(() => editField.placeholder = "Push new value...", 1000);
                    }
                };
            }

            const historySlider = document.getElementById('vavid-history-range');
            if (historySlider) {
                historySlider.oninput = (e) => {
                    historyIndex = parseInt(e.target.value);
                    renderParadiseHUD(focusBitIndex);
                };
            }
        }
    }

    function renderXRayTab(obj, prop, bitIndex) {
        const metadata = obj.metadata;
        const currentFlags = historyIndex === -1 
            ? (obj.flags || { dirty: 0n, busy: 0n, error: 0n })
            : obj.history[historyIndex];

        let bitGrid = '';
        for (let i = 0; i < 128; i++) {
            const isDirty = (BigInt(currentFlags.dirty) & (1n << BigInt(i))) !== 0n;
            const isBusy = (BigInt(currentFlags.busy) & (1n << BigInt(i))) !== 0n;
            const isError = (BigInt(currentFlags.error) & (1n << BigInt(i))) !== 0n;
            const isFocus = i == bitIndex;
            bitGrid += `<div class="vavid-bit-node ${isDirty ? 'active' : ''} ${isBusy ? 'busy' : ''} ${isError ? 'error' : ''}" 
                             style="${isFocus ? 'border-color: #fff; transform: scale(1.1);' : ''}" 
                             title="Bit ${i}"></div>`;
        }

        return `
            <div style="margin-bottom: 15px;">
                <div style="font-size: 10px; opacity: 0.6; margin-bottom: 2px;">MODEL_CONTEXT</div>
                <div style="font-size: 16px; color: #fff; font-weight: 300;">${metadata.Name}</div>
                <div style="font-size: 9px; opacity: 0.4;">OBJ_REF: ${obj.id}</div>
            </div>

            <div style="margin-bottom: 15px; padding: 12px; background: rgba(0,255,255,0.05); border-radius: 4px; border: 1px solid rgba(0,255,255,0.1);">
                <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 8px;">
                    <div style="font-size: 14px; color: #00ffff;">${prop.Name}</div>
                    <div style="font-size: 10px; opacity: 0.6;">TYPE: ${prop.Type || 'Unknown'}</div>
                </div>
                <input type="text" class="vavid-edit-input" placeholder="Push new value..." id="vavid-edit-field">
            </div>

            <div style="margin-bottom: 15px;">
                <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 8px;">
                    <div style="font-size: 10px; opacity: 0.6;">TIME_TRAVEL</div>
                    <div style="font-size: 10px; color: #00ffff;">${historyIndex === -1 ? 'LIVE' : 'T-' + (obj.history.length - 1 - historyIndex)}</div>
                </div>
                <input type="range" class="vavid-history-slider" id="vavid-history-range" 
                       min="-1" max="${obj.history.length - 1}" value="${historyIndex}">
                
                <div class="vavid-telemetry-grid">${bitGrid}</div>
            </div>

            <div style="margin-bottom: 15px;">
                <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 8px;">
                    <div style="font-size: 10px; opacity: 0.6;">VALUE_HISTORY</div>
                    <div style="font-size: 8px; opacity: 0.4;">LAST_10_MUTATIONS</div>
                </div>
                <div style="background: rgba(0,0,0,0.2); border-radius: 4px; padding: 8px; font-size: 9px; max-height: 80px; overflow-y: auto;">
                    ${(valueHistory[obj.id] && valueHistory[obj.id][prop.Name]) ? valueHistory[obj.id][prop.Name].map(h => `
                        <div style="margin-bottom: 4px; display: flex; justify-content: space-between;">
                            <span style="color: #00ffff;">${h.val}</span>
                            <span style="opacity: 0.4;">${new Date(h.time).toLocaleTimeString([], { hour12: false, hour: '2-digit', minute: '2-digit', second: '2-digit' })}</span>
                        </div>
                    `).join('') : '<div style="opacity: 0.3; font-style: italic;">No mutations logged yet...</div>'}
                </div>
            </div>

            <div style="display: grid; grid-template-columns: 1fr 1fr; gap: 8px;">
                <button onclick="window.vavid.triggerChaos()" style="padding: 6px; background: rgba(0,255,255,0.1); border: 1px solid #00ffff; color: #00ffff; font-family: inherit; font-size: 10px; cursor: pointer;">TRIGGER_CHAOS</button>
                <button onclick="window.vavidExt.reset()" style="padding: 6px; background: rgba(255,255,255,0.05); border: 1px solid rgba(255,255,255,0.2); color: #fff; font-family: inherit; font-size: 10px; cursor: pointer;">LIVE_FEED</button>
            </div>
        `;
    }

    function renderDeltasTab() {
        const deltas = window.vavid.deltas || [];
        const deltaHtml = deltas.slice(-10).map(d => `
            <div style="margin-bottom: 10px;">
                <div style="font-size: 9px; opacity: 0.5;">${new Date(d.timestamp).toLocaleTimeString()} | ID: ${d.id}</div>
                <pre class="vavid-delta-block">${JSON.stringify(JSON.parse(d.json), null, 2)}</pre>
            </div>
        `).join('') || '<div style="opacity: 0.4; font-size: 10px; text-align: center; padding: 20px;">NO_DELTAS_LOGGED</div>';

        return `
            <div style="font-size: 10px; opacity: 0.6; margin-bottom: 10px;">SURGICAL_DELTA_STREAM</div>
            <div style="max-height: 400px; overflow-y: auto;">
                ${deltaHtml}
            </div>
        `;
    }

    function renderSecurityTab(obj) {
        const currentFlags = obj.flags || { dirty: 0n, busy: 0n, error: 0n };
        const hasViolations = BigInt(currentFlags.error) !== 0n;

        let errorDetails = '';
        if (hasViolations) {
            for (let i = 0; i < 128; i++) {
                if ((BigInt(currentFlags.error) & (1n << BigInt(i))) !== 0n) {
                    const prop = obj.metadata.Properties.find(p => p.BitIndex == i);
                    errorDetails += `
                        <div style="margin-bottom: 8px; padding: 8px; background: rgba(239, 64, 64, 0.1); border-left: 3px solid #ef4444; border-radius: 2px;">
                            <div style="font-size: 11px; color: #ef4444; font-weight: bold;">VIOLATION: BIT_${i}</div>
                            <div style="font-size: 10px; color: #fff; opacity: 0.8;">PROP: ${prop ? prop.Name : 'Internal'}</div>
                            <div style="font-size: 9px; opacity: 0.6;">VAL: OUT_OF_RANGE</div>
                        </div>
                    `;
                }
            }
        }

        return `
            <div style="font-size: 10px; opacity: 0.6; margin-bottom: 15px;">AXIOM_SECURITY_AUDIT</div>
            <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 20px; padding: 15px; background: ${hasViolations ? 'rgba(239, 64, 64, 0.05)' : 'rgba(34, 197, 94, 0.05)'}; border: 1px solid ${hasViolations ? '#ef4444' : '#22c55e'}; border-radius: 4px;">
                <div>
                    <div style="font-size: 10px; opacity: 0.6;">THREAT_LEVEL</div>
                    <div style="font-size: 24px; color: ${hasViolations ? '#ef4444' : '#22c55e'};">${hasViolations ? 'ELEVATED' : 'STABLE'}</div>
                </div>
            </div>
            <div style="max-height: 300px; overflow-y: auto;">
                ${errorDetails || '<div style="opacity: 0.4; font-size: 10px; text-align: center; padding: 20px;">NO_SECURITY_VIOLATIONS_DETECTED</div>'}
            </div>
        `;
    }

    function renderPerfTab(obj) {
        return `
            <div style="font-size: 10px; opacity: 0.6; margin-bottom: 15px;">PERFORMANCE_PROFILER</div>
            <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 20px; padding: 15px; background: rgba(0,255,255,0.05); border: 1px solid rgba(0,255,255,0.1); border-radius: 4px;">
                <div>
                    <div style="font-size: 10px; opacity: 0.6;">BIT-FLIP LATENCY</div>
                    <div style="font-size: 24px; color: #00ffff;" id="vavid-latency-val">${(obj.lastLatency || 0).toFixed(4)}ms</div>
                </div>
                <div class="vavid-ext-latency-pulse"></div>
            </div>
            <div style="margin-bottom: 20px; border: 1px solid rgba(0,255,255,0.1); border-radius: 4px; padding: 5px; background: rgba(0,0,0,0.2);">
                <canvas id="vavid-perf-canvas" width="280" height="60" style="width: 100%; height: 60px;"></canvas>
            </div>
        `;
    }

    function updatePerfDisplay(latency) {
        latencyHistory.push(latency);
        if (latencyHistory.length > 50) latencyHistory.shift();
        const valEl = document.getElementById('vavid-latency-val');
        if (valEl) valEl.innerText = latency.toFixed(4) + 'ms';
        drawSparkline();
    }

    function drawSparkline() {
        const canvas = document.getElementById('vavid-perf-canvas');
        if (!canvas) return;
        const ctx = canvas.getContext('2d');
        const w = canvas.width;
        const h = canvas.height;
        ctx.clearRect(0, 0, w, h);
        if (latencyHistory.length < 2) return;
        ctx.beginPath();
        ctx.strokeStyle = '#00ffff';
        ctx.lineWidth = 1;
        const max = Math.max(...latencyHistory, 0.1);
        const step = w / (latencyHistory.length - 1);
        latencyHistory.forEach((l, i) => {
            const x = i * step;
            const y = h - (l / max) * h;
            if (i === 0) ctx.moveTo(x, y);
            else ctx.lineTo(x, y);
        });
        ctx.stroke();
    }

    function getOrCreateSidebar() {
        let sidebar = document.getElementById('vavid-extension-hud');
        if (!sidebar) {
            sidebar = document.createElement('div');
            sidebar.id = 'vavid-extension-hud';
            sidebar.className = 'vavid-ext-glass';
            sidebar.style.cssText = `
                position: fixed; top: 20px; right: 20px; width: 340px; padding: 20px;
                font-family: 'JetBrains Mono', 'Fira Code', monospace; z-index: 2147483647;
                border-radius: 12px; color: #e2e8f0; display: ${isVisible ? 'block' : 'none'};
            `;
            document.body.appendChild(sidebar);
            createToggleButton();
        }
        return sidebar;
    }

    function createToggleButton() {
        if (document.getElementById('vavid-hud-toggle')) return;
        const btn = document.createElement('div');
        btn.id = 'vavid-hud-toggle';
        btn.className = `vavid-toggle-btn ${isVisible ? 'active' : ''}`;
        btn.innerHTML = 'V';
        btn.title = 'Toggle V.A.V.I.D. HUD';
        btn.onclick = () => {
            isVisible = !isVisible;
            localStorage.setItem(STORAGE_KEY, isVisible);
            const sidebar = document.getElementById('vavid-extension-hud');
            if (sidebar) sidebar.style.display = isVisible ? 'block' : 'none';
            updateToggleState();
            if (isVisible) renderParadiseHUD(null);
        };
        document.body.appendChild(btn);
    }

    function updateToggleState() {
        const btn = document.getElementById('vavid-hud-toggle');
        if (btn) {
            if (isVisible) btn.classList.add('active');
            else btn.classList.remove('active');
        }
    }

    window.vavidExt = {
        setTab: (tab, bit) => {
            activeTab = tab;
            renderParadiseHUD(bit);
        },
        toggleInspect: () => {
            isInspectMode = !isInspectMode;
            if (isInspectMode && !isVisible) {
                isVisible = true;
                localStorage.setItem(STORAGE_KEY, 'true');
                const sidebar = document.getElementById('vavid-extension-hud');
                if (sidebar) sidebar.style.display = 'block';
                updateToggleState();
            }
            renderParadiseHUD(null);
        },
        reset: () => {
            historyIndex = -1;
            renderParadiseHUD(null);
        }
    };

    setInterval(() => {
        if (isVisible && activeObjectId && historyIndex === -1) {
            renderParadiseHUD(null);
        }
    }, 500);

    setTimeout(getOrCreateSidebar, 500);

})();
