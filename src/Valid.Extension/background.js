const connections = {};

chrome.runtime.onConnect.addListener((port) => {
    const extensionListener = (message, sender, sendResponse) => {
        if (message.name === 'init') {
            connections[message.tabId] = port;
            return;
        }
        // Forward message to content script
        chrome.tabs.sendMessage(message.tabId, message);
    };

    port.onMessage.addListener(extensionListener);

    port.onDisconnect.addListener((port) => {
        port.onMessage.removeListener(extensionListener);
        const tabs = Object.keys(connections);
        for (let i = 0, len = tabs.length; i < len; i++) {
            if (connections[tabs[i]] === port) {
                delete connections[tabs[i]];
                break;
            }
        }
    });
});

// Receive message from content script and relay to DevTools
chrome.runtime.onMessage.addListener((request, sender, sendResponse) => {
    if (sender.tab) {
        const tabId = sender.tab.id;
        if (tabId in connections) {
            connections[tabId].postMessage(request);
        }
    }
    return true;
});
