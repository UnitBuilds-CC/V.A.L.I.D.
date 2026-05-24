window.addEventListener('message', (event) => {
    if (event.source !== window) return;

    const data = event.data;
    if (!data || data.source !== 'vavid-extension-bridge') return;

    switch (data.type) {
        case 'vavid-ext-discovery':
            chrome.runtime.sendMessage({ type: 'vavid-discovery', data: data.detail });
            break;
        case 'vavid-telemetry-pulse':
            chrome.runtime.sendMessage({ type: 'vavid-telemetry', data: data.detail });
            break;
        case 'vavid-delta-logged':
            chrome.runtime.sendMessage({ type: 'vavid-delta', data: data.detail });
            break;
        case 'vavid-security-violation':
            chrome.runtime.sendMessage({ type: 'vavid-security', data: data.detail });
            break;
    }
});

chrome.runtime.onMessage.addListener((message) => {
    window.postMessage({ source: 'vavid-extension-content', type: message.type, detail: message.data }, "*");
});
