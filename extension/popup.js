"use strict";

const toggle = document.getElementById("toggle");
const countEl = document.getElementById("count");
const statusEl = document.getElementById("status");

// Load current state
chrome.storage.local.get("enabled", (data) => {
  const isEnabled = data.enabled !== false;
  toggle.checked = isEnabled;
  statusEl.textContent = isEnabled ? "Active" : "Paused";
});

// Toggle handler
toggle.addEventListener("change", () => {
  const newState = toggle.checked;
  chrome.storage.local.set({ enabled: newState });

  // Notify the active tab's content script
  chrome.tabs.query({ active: true, currentWindow: true }, (tabs) => {
    if (tabs[0]) {
      chrome.tabs.sendMessage(tabs[0].id, {
        type: "SET_ENABLED",
        enabled: newState,
      });
    }
  });

  statusEl.textContent = newState ? "Active" : "Paused";
});

// Listen for count updates from background
chrome.runtime.onMessage.addListener((message) => {
  if (message.type === "FIELD_COUNT") {
    countEl.textContent = message.count;
  }
});

// Request current count from content script
chrome.tabs.query({ active: true, currentWindow: true }, (tabs) => {
  if (tabs[0]) {
    chrome.tabs.sendMessage(tabs[0].id, { type: "GET_COUNT" });
  }
});
