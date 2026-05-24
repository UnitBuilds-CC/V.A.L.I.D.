/**
 * V.A.V.I.D. Bridge (V.A.L.I.D. Automated Visual Intelligent Diagnostics)
 * Connects .NET Business Objects to the Browser HUD.
 */
(function () {
    console.log("V.A.V.I.D. Bridge Initialized.");

    window.VAVID = {
        discover: function () {
            if (!window.__VALID_OBJECTS__) return [];
            return window.__VALID_OBJECTS__;
        },

        syncFullState: function () {
            const objects = this.discover();
            objects.forEach(obj => {
                const metadata = obj.metadata;
                try {
                    const state = JSON.parse(obj.instance.getStateJson());
                    const dirtyMask = BigInt(state.dirty);
                    const busyMask = BigInt(state.busy);
                    const errorMask = BigInt(state.error);

                    metadata.properties.forEach((prop, index) => {
                        const element = document.querySelector(`[data-valid-prop="${prop.name}"]`);
                        if (element) {
                            const bit = 1n << BigInt(index);

                            if ((dirtyMask & bit) !== 0n) element.classList.add("valid-dirty");
                            else element.classList.remove("valid-dirty");

                            if ((busyMask & bit) !== 0n) element.classList.add("valid-busy");
                            else element.classList.remove("valid-busy");

                            if ((errorMask & bit) !== 0n) element.classList.add("valid-error");
                            else element.classList.remove("valid-error");
                        }
                    });
                } catch (e) {
                    console.error("Failed to sync state for", metadata.typeName, e);
                }
            });
        },

        showRuleTooltips: function () {
            // ... (keep existing logic)
            const objects = this.discover();
            objects.forEach(obj => {
                obj.metadata.properties.forEach(prop => {
                    const element = document.querySelector(`[data-valid-prop="${prop.name}"]`);
                    if (element && prop.rules.length > 0) {
                        const rulesText = prop.rules.map(r => `${r.name}(${r.args.join(', ')})`).join('\n');
                        element.setAttribute("title", `V.A.L.I.D. Rules:\n${rulesText}`);
                    }
                });
            });
        }
    };

    // Auto-refresh states for POC
    setInterval(() => window.VAVID.syncFullState(), 500);
})();
