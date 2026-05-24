(function() {
    const tabId = chrome.devtools.inspectedWindow.tabId;
    const backgroundPageConnection = chrome.runtime.connect({ name: "vavid-devtools" });

    backgroundPageConnection.postMessage({
        name: 'init',
        tabId: tabId
    });

    backgroundPageConnection.postMessage({
        type: 'vavid-discovery-request',
        tabId: tabId
    });

    let activeObjectId = null;
    let historyIndex = -1;
    let activeTab = 'xray';
    let valueHistory = {};
    let latencyHistory = [];
    let objects = [];

    backgroundPageConnection.onMessage.addListener((message) => {
        if (message.type === 'vavid-discovery') {
            objects = message.data;
            renderHUD();
        } else if (message.type === 'vavid-telemetry') {
            if (activeObjectId === message.data.id) {
                updatePerfDisplay(message.data.latency);
            }
        } else if (message.type === 'vavid-delta') {
            handleDelta(message.data);
        } else if (message.type === 'vavid-security') {
            renderHUD();
        }
    });

    function handleDelta(data) {
        const delta = JSON.parse(data.json);
        const objId = data.id;
        if (!valueHistory[objId]) valueHistory[objId] = {};
        for (const [prop, val] of Object.entries(delta)) {
            if (!valueHistory[objId][prop]) valueHistory[objId][prop] = [];
            valueHistory[objId][prop].unshift({ val, time: Date.now() });
            if (valueHistory[objId][prop].length > 10) valueHistory[objId][prop].pop();
        }
        renderHUD();
    }

    function dispatchToPage(type, payload) {
        chrome.devtools.inspectedWindow.eval(`
            window.dispatchEvent(new CustomEvent('${type}', { detail: ${JSON.stringify(payload)} }));
        `);
    }

    function renderHUD() {
        const container = document.getElementById('vavid-extension-hud');
        const obj = objects.find(o => o.id === activeObjectId);

        if (!obj) {
            container.innerHTML = `
                <div style="text-align: center; padding: 40px 20px; opacity: 0.5;">
                    <div style="font-size: 24px; margin-bottom: 10px;">🛰️</div>
                    <div style="font-size: 10px; letter-spacing: 1px;">AWAITING_OBJECT_SELECTION...</div>
                    <select id="obj-selector" style="margin-top: 20px; background: #111; color: #00ffff; border: 1px solid #00ffff; padding: 5px; font-family: inherit;">
                        <option value="">-- Select Object --</option>
                        ${objects.map(o => `<option value="${o.id}">${o.metadata.Name} (${o.id})</option>`).join('')}
                    </select>
                </div>
            `;
            const selector = document.getElementById('obj-selector');
            if (selector) selector.onchange = (e) => { activeObjectId = e.target.value; renderHUD(); };
            return;
        }

        const metadata = obj.metadata;
        const currentFlags = (historyIndex === -1 || !obj.history || !obj.history[historyIndex]) 
            ? (obj.flags || { dirty: "0", busy: "0", error: "0" }) 
            : obj.history[historyIndex];
        
        const prop = metadata.Properties[0];

        let html = `
            <div style="border-bottom: 1px solid rgba(0,255,255,0.2); padding: 15px; background: rgba(0,0,0,0.3);">
                <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 10px;">
                    <span class="vavid-ext-axiom-header" style="font-size: 11px; letter-spacing: 2px;">V.A.V.I.D. DEV_TOOLS</span>
                    <select id="obj-selector-mini" style="background: rgba(0,255,255,0.1); color: #00ffff; border: 1px solid #00ffff; padding: 2px; font-size: 9px; cursor: pointer; border-radius: 2px;">
                        ${objects.map(o => `<option value="${o.id}" ${o.id === activeObjectId ? 'selected' : ''}>${o.metadata.Name}</option>`).join('')}
                    </select>
                </div>
                <div style="display: flex; gap: 4px;">
                    <button class="vavid-tab-btn ${activeTab === 'xray' ? 'active' : ''}" id="tab-xray">X-RAY</button>
                    <button class="vavid-tab-btn ${activeTab === 'security' ? 'active' : ''}" id="tab-security">SECURITY</button>
                    <button class="vavid-tab-btn ${activeTab === 'deltas' ? 'active' : ''}" id="tab-deltas">DELTAS</button>
                    <button class="vavid-tab-btn ${activeTab === 'perf' ? 'active' : ''}" id="tab-perf">PERF</button>
                </div>
            </div>
            <div style="padding: 20px; height: calc(100vh - 100px); overflow-y: auto;">
        `;

        if (activeTab === 'xray') {
            let bitGrid = '';
            for (let i = 0; i < 128; i++) {
                const isDirty = (BigInt(currentFlags.dirty) & (1n << BigInt(i))) !== 0n;
                const isBusy = (BigInt(currentFlags.busy) & (1n << BigInt(i))) !== 0n;
                const isError = (BigInt(currentFlags.error) & (1n << BigInt(i))) !== 0n;
                bitGrid += `<div class="vavid-bit-node ${isDirty ? 'active' : ''} ${isBusy ? 'busy' : ''} ${isError ? 'error' : ''}" title="Bit ${i}"></div>`;
            }

            html += `
                <div style="margin-bottom: 20px;">
                    <div style="font-size: 10px; opacity: 0.6;">MODEL_CONTEXT</div>
                    <div style="font-size: 16px; color: #fff;">${metadata.Name}</div>
                    <div style="font-size: 9px; opacity: 0.4;">OBJ_REF: ${obj.id}</div>
                </div>
                <div style="margin-bottom: 20px;">
                    <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 8px;">
                        <div style="font-size: 10px; color: #00ffff;">TIME_TRAVEL_INJECTION</div>
                        <div style="font-size: 10px; color: #fff;">${(historyIndex === -1 || !obj.history) ? 'LIVE' : 'T-' + ((obj.history.length || 1) - 1 - historyIndex)}</div>
                    </div>
                    <input type="range" style="width: 100%; margin-bottom: 10px;" id="vavid-history-range" 
                           min="-1" max="${(obj.history ? obj.history.length : 0) - 1}" value="${historyIndex}">
                    <div style="display: grid; grid-template-columns: repeat(32, 1fr); gap: 2px;">${bitGrid}</div>
                </div>
                <div style="margin-bottom: 20px;">
                    <button id="btn-snap-test" style="width: 100%; padding: 8px; background: rgba(0,255,255,0.1); border: 1px solid #00ffff; color: #00ffff; font-family: inherit; font-size: 10px; cursor: pointer;">GENERATE_XUNIT_TEST</button>
                    <textarea id="xunit-output" style="width: 100%; height: 100px; margin-top: 10px; background: #000; color: #22c55e; border: 1px solid #333; font-family: monospace; font-size: 9px; display: none;" readonly></textarea>
                </div>
            `;
        } else if (activeTab === 'security') {
            const hasViolations = BigInt(currentFlags.error) !== 0n;
            html += `
                <div style="font-size: 10px; opacity: 0.6; margin-bottom: 15px;">AXIOM_SECURITY_AUDIT</div>
                <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 20px; padding: 15px; background: ${hasViolations ? 'rgba(239, 64, 64, 0.05)' : 'rgba(34, 197, 94, 0.05)'}; border: 1px solid ${hasViolations ? '#ef4444' : '#22c55e'}; border-radius: 4px;">
                    <div>
                        <div style="font-size: 10px; opacity: 0.6;">THREAT_LEVEL</div>
                        <div style="font-size: 24px; color: ${hasViolations ? '#ef4444' : '#22c55e'};">${hasViolations ? 'ELEVATED' : 'STABLE'}</div>
                    </div>
                </div>
            `;
        } else if (activeTab === 'deltas') {
            html += `<div style="font-size: 10px; opacity: 0.6;">DELTA STREAM EXPORT NOT IMPLEMENTED IN DEMO</div>`;
        } else if (activeTab === 'perf') {
            html += `
                <div style="font-size: 10px; opacity: 0.6; margin-bottom: 15px;">PERFORMANCE_PROFILER</div>
                <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 20px; padding: 15px; background: rgba(0,255,255,0.05); border: 1px solid rgba(0,255,255,0.1); border-radius: 4px;">
                    <div>
                        <div style="font-size: 10px; opacity: 0.6;">BIT-FLIP LATENCY</div>
                        <div style="font-size: 24px; color: #00ffff;" id="vavid-latency-val">${(obj.lastLatency || 0).toFixed(4)}ms</div>
                    </div>
                </div>
            `;
        }

        html += `</div>`;
        container.innerHTML = html;

        document.getElementById('tab-xray').onclick = () => { activeTab = 'xray'; renderHUD(); };
        document.getElementById('tab-security').onclick = () => { activeTab = 'security'; renderHUD(); };
        document.getElementById('tab-deltas').onclick = () => { activeTab = 'deltas'; renderHUD(); };
        document.getElementById('tab-perf').onclick = () => { activeTab = 'perf'; renderHUD(); };
        
        const selMini = document.getElementById('obj-selector-mini');
        if (selMini) selMini.onchange = (e) => { activeObjectId = e.target.value; renderHUD(); };

        const historySlider = document.getElementById('vavid-history-range');
        if (historySlider) {
            historySlider.oninput = (e) => {
                historyIndex = parseInt(e.target.value);
                const historyLen = obj.history ? obj.history.length : 0;
                const stepsBack = historyLen - 1 - historyIndex;
                if (stepsBack >= 0 && stepsBack < 16) {
                    chrome.devtools.inspectedWindow.eval(`window.vavid.restoreHistory('${obj.id}', ${stepsBack});`);
                }
                renderHUD();
            };
        }

        const btnSnap = document.getElementById('btn-snap-test');
        if (btnSnap) {
            btnSnap.onclick = () => {
                const out = document.getElementById('xunit-output');
                out.style.display = 'block';
                out.value = generateXUnit(obj, valueHistory[obj.id] || {});
            };
        }
    }

    function generateXUnit(obj, history) {
        let code = `[Fact]\npublic void Generated_State_Replication_Test()\n{\n`;
        code += `    var target = new ${obj.metadata.Name}();\n`;
        for (const prop in history) {
            if (history[prop] && history[prop].length > 0) {
                const val = history[prop][0].val;
                let valStr = typeof val === 'string' ? `"${val}"` : val;
                code += `    target.${prop} = ${valStr};\n`;
            }
        }
        const flags = historyIndex === -1 ? obj.flags : obj.history[historyIndex];
        if (flags) {
            code += `\n    // Verify replication\n`;
            code += `    Assert.True(target.ErrorFlags == System.UInt128.Parse("${BigInt(flags.error || 0n).toString()}"));\n`;
        }
        code += `}\n`;
        return code;
    }

    function updatePerfDisplay(latency) {
        latencyHistory.push(latency);
        if (latencyHistory.length > 50) latencyHistory.shift();
        const valEl = document.getElementById('vavid-latency-val');
        if (valEl) valEl.innerText = latency.toFixed(4) + 'ms';
    }

})();
