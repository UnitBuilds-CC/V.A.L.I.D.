window.vavidBridge = {
    metadataCache: {},
    isSupported: typeof BigInt !== 'undefined',

    // Called once per object type to cache the bit-index map
    init: function (objectName, metadataJson) {
        this.metadataCache[objectName] = JSON.parse(metadataJson);
    },

    // Called every time PropertyChanged fires — applies bitmask to DOM
    renderState: function (objectName, stateJson) {
        if (!this.isSupported) return;
        const state = JSON.parse(stateJson);
        this._updateDom(objectName, state);
    },

    // Bitmask DVR: Apply historical state without triggering mutations
    applyReplayState: function (objectName, stateJson, dataJson) {
        if (!this.isSupported) return;
        const state = JSON.parse(stateJson);
        const data = JSON.parse(dataJson);

        this._isReplaying = true;
        this._updateDom(objectName, state);

        // Update input values directly from dataJson
        Object.keys(data).forEach(prop => {
            const elements = document.querySelectorAll(`[data-prop="${prop}"]`);
            elements.forEach(el => {
                if (el.tagName === 'INPUT') el.value = data[prop];
                else el.innerText = data[prop];
            });
        });
        this._isReplaying = false;
    },

    _updateDom: function (objectName, state) {
        const meta = this.metadataCache[objectName];
        if (!meta) return;

        meta.properties.forEach(prop => {
            const mask = 1n << BigInt(prop.bitIndex);
            const isError = (BigInt(state.error) & mask) !== 0n;
            const isBusy = (BigInt(state.busy) & mask) !== 0n;
            const isDirty = (BigInt(state.dirty) & mask) !== 0n;

            const elements = document.querySelectorAll(`[data-prop="${prop.name}"]`);
            elements.forEach(el => {
                el.classList.toggle('vavid-error', isError);
                el.classList.toggle('vavid-busy', isBusy);
                el.classList.toggle('vavid-dirty', isDirty);
            });
        });
    },

    registerValidator: function (element, propertyName, stateMask) {
        element.addEventListener('input', (e) => {
            if (this._isReplaying) return;
            // Native mutation logic here...
        });
    }
};
