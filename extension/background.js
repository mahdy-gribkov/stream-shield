"use strict";

chrome.runtime.onInstalled.addListener(() => {
  chrome.storage.local.set({ enabled: true });
});

chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
  if (message.type === "GET_STATE") {
    chrome.storage.local.get("enabled", (data) => {
      sendResponse({ enabled: data.enabled !== false });
    });
    return true; // async response
  }

  if (message.type === "SET_ENABLED") {
    chrome.storage.local.set({ enabled: message.enabled });
    // Broadcast to all content scripts in the sender's tab
    if (sender.tab && sender.tab.id) {
      chrome.tabs.sendMessage(sender.tab.id, {
        type: "SET_ENABLED",
        enabled: message.enabled,
      });
    }
  }

  if (message.type === "TOGGLE") {
    chrome.storage.local.get("enabled", (data) => {
      const newState = !(data.enabled !== false);
      chrome.storage.local.set({ enabled: newState });

      // Broadcast to the active tab
      chrome.tabs.query({ active: true, currentWindow: true }, (tabs) => {
        if (tabs[0]) {
          chrome.tabs.sendMessage(tabs[0].id, {
            type: "SET_ENABLED",
            enabled: newState,
          });
        }
      });

      sendResponse({ enabled: newState });
    });
    return true; // async response
  }

  if (message.type === "FIELD_COUNT") {
    const count = message.count || 0;
    const text = count > 0 ? String(count) : "";
    const tabId = sender.tab ? sender.tab.id : null;

    if (tabId) {
      chrome.action.setBadgeText({ text, tabId });
      chrome.action.setBadgeBackgroundColor({ color: "#059669", tabId });
    }
  }

  if (message.type === "GET_COUNT_FOR_POPUP") {
    chrome.tabs.query({ active: true, currentWindow: true }, (tabs) => {
      if (tabs[0]) {
        chrome.tabs.sendMessage(tabs[0].id, { type: "GET_COUNT" });
      }
    });
  }
});
