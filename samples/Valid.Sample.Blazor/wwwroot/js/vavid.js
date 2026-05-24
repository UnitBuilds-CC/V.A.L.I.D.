/**
 * VAVID 3.0.0 Micro-Runtime
 * Handles high-frequency bitmask state updates and surgical DOM mutations.
 */
class VavidBridge {
    constructor() {
        this.objects = new Map();
        console.log("VAVID 3.0.0 Bridge Initialized.");
    }

    /**
     * Updates an object's state in the DOM based on its bitmasks.
     * @param {string} id - The object identifier.
     * @param {bigint} dirty - The dirty bitmask.
     * @param {bigint} busy - The busy bitmask.
     * @param {bigint} error - The error bitmask.
     */
    updateState(id, dirty, busy, error) {
        const elements = document.querySelectorAll(`[vavid-obj="${id}"]`);
        elements.forEach(el => {
            const bit = BigInt(el.getAttribute('vavid-bit'));
            
            // Toggle classes based on bitwise logic
            el.classList.toggle('vavid-dirty', (dirty & (1n << bit)) !== 0n);
            el.classList.toggle('vavid-busy', (busy & (1n << bit)) !== 0n);
            el.classList.toggle('vavid-error', (error & (1n << bit)) !== 0n);
        });
    }
}

window.vavid = new VavidBridge();
