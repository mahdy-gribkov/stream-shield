"use strict";

(() => {
  const SENSITIVE_KEYWORDS = [
    // Financial
    "card.?num", "credit.?card", "debit.?card", "cc.?num",
    "cvv", "cvc", "csc", "cid", "cvv2",
    "security.?code", "card.?code",
    "expir", "exp.?date",
    "iban", "routing", "account.?num", "bank.?account",
    "sort.?code", "swift", "bic", "bank.?code", "branch.?code",
    "pin.?number",
    // Identity
    "ssn", "social.?security", "passport", "national.?id",
    "driver.?licen", "tax.?id",
    "license.?num", "id.?number", "state.?id",
    "green.?card", "visa.?num",
    "dob", "date.?of.?birth",
    "member.?id", "patient.?id", "insurance", "maiden",
    // Authentication
    "secret", "token", "pin", "otp", "auth.?code",
    "2fa", "verif.?code", "mfa", "one.?time",
    "passphrase", "credential",
    "api.?key", "private.?key", "access.?token",
    "client.?secret", "bearer", "recovery.?code", "backup.?code",
    // Hebrew
    "\u05ea\\.\u05d6",
    "\u05ea\u05e2\u05d5\u05d3\u05ea.?\u05d6\u05d4\u05d5\u05ea",
    "\u05db\u05e8\u05d8\u05d9\u05e1.?\u05d0\u05e9\u05e8\u05d0\u05d9",
    "\u05e1\u05d9\u05e1\u05de[\u05d4\u05d0]",
    "\u05e7\u05d5\u05d3.?\u05d0\u05d1\u05d8\u05d7\u05d4",
    "\u05ea\u05d5\u05e7\u05e3",
    "\u05de\u05e1\u05e4\u05e8.?\u05d7\u05e9\u05d1\u05d5\u05df",
  ];

  const KEYWORD_REGEX = new RegExp(SENSITIVE_KEYWORDS.join("|"), "i");

  const OVERLAY_ATTR = "data-streamshield-id";
  const overlayMap = new WeakMap();
  let overlayCounter = 0;
  let enabled = true;
  let protectedCount = 0;

  // --- Detection ---

  function isSensitiveField(el) {
    if (el.tagName !== "INPUT" && el.tagName !== "TEXTAREA") return false;

    const type = (el.getAttribute("type") || "").toLowerCase();
    if (type === "password") return true;

    const autocomplete = (el.getAttribute("autocomplete") || "").toLowerCase();
    if (
      autocomplete.includes("cc-") ||
      autocomplete.includes("ssn") ||
      autocomplete.includes("passport") ||
      autocomplete === "current-password" ||
      autocomplete === "new-password" ||
      autocomplete === "one-time-code"
    ) {
      return true;
    }

    const describedById = el.getAttribute("aria-describedby");
    let describedByText = "";
    if (describedById) {
      const descEl = document.getElementById(describedById);
      if (descEl) describedByText = descEl.textContent || "";
    }

    const testValues = [
      el.getAttribute("name"),
      el.getAttribute("id"),
      el.getAttribute("placeholder"),
      el.getAttribute("aria-label"),
      describedByText,
    ]
      .filter(Boolean)
      .join(" ");

    return KEYWORD_REGEX.test(testValues);
  }

  // --- Overlay Creation ---

  function createOverlay(field) {
    if (overlayMap.has(field)) return;

    const overlay = document.createElement("div");
    overlay.className =
      "streamshield-overlay streamshield-overlay--blurred";

    const valueSpan = document.createElement("span");
    valueSpan.className = "streamshield-overlay__value";
    overlay.appendChild(valueSpan);

    const protectedLabel = document.createElement("span");
    protectedLabel.className = "streamshield-overlay__protected-label";
    protectedLabel.innerHTML =
      '<svg viewBox="0 0 24 24"><path d="M12 1L3 5v6c0 5.55 3.84 10.74 9 12 5.16-1.26 9-6.45 9-12V5l-9-4zm0 10.99h7c-.53 4.12-3.28 7.79-7 8.94V12H5V6.3l7-3.11v8.8z"/></svg> Protected';
    overlay.appendChild(protectedLabel);

    const id = "ss-" + overlayCounter++;
    overlay.setAttribute(OVERLAY_ATTR, id);
    field.setAttribute(OVERLAY_ATTR, id);

    document.body.appendChild(overlay);
    overlayMap.set(field, overlay);

    positionOverlay(field, overlay);
    syncOverlayStyle(field, overlay);

    field.addEventListener("input", () => onFieldInput(field, overlay));
    field.addEventListener("focus", () => onFieldFocus(field, overlay));
    field.addEventListener("blur", () => onFieldBlur(field, overlay));

    observeFieldResize(field, overlay);

    protectedCount++;
    reportCount();
  }

  function positionOverlay(field, overlay) {
    const rect = field.getBoundingClientRect();
    overlay.style.top = rect.top + window.scrollY + "px";
    overlay.style.left = rect.left + window.scrollX + "px";
    overlay.style.width = rect.width + "px";
    overlay.style.height = rect.height + "px";
  }

  function syncOverlayStyle(field, overlay) {
    const cs = window.getComputedStyle(field);
    overlay.style.backgroundColor = cs.backgroundColor;
    overlay.style.color = cs.color;
    overlay.style.fontSize = cs.fontSize;
    overlay.style.fontFamily = cs.fontFamily;
    overlay.style.borderRadius = cs.borderRadius;
    overlay.style.paddingLeft = cs.paddingLeft;
    overlay.style.paddingRight = cs.paddingRight;
    overlay.style.lineHeight = cs.lineHeight;
  }

  // --- Overlay State ---

  function onFieldInput(field, overlay) {
    const valueSpan = overlay.querySelector(".streamshield-overlay__value");
    const type = (field.getAttribute("type") || "").toLowerCase();
    if (type === "password") {
      valueSpan.textContent = "\u2022".repeat(field.value.length);
    } else {
      valueSpan.textContent = field.value;
    }
  }

  function onFieldFocus(field, overlay) {
    overlay.classList.remove("streamshield-overlay--blurred");
    overlay.classList.add("streamshield-overlay--focused");
    onFieldInput(field, overlay);
  }

  function onFieldBlur(field, overlay) {
    overlay.classList.remove("streamshield-overlay--focused");
    overlay.classList.add("streamshield-overlay--blurred");
  }

  // --- Repositioning ---

  function repositionAll() {
    const fields = document.querySelectorAll("[" + OVERLAY_ATTR + "]");
    fields.forEach((field) => {
      if (field.classList && field.classList.contains("streamshield-overlay"))
        return;
      const overlay = overlayMap.get(field);
      if (overlay) {
        positionOverlay(field, overlay);
        syncOverlayStyle(field, overlay);
      }
    });
  }

  let repositionRafPending = false;
  function requestReposition() {
    if (repositionRafPending) return;
    repositionRafPending = true;
    requestAnimationFrame(() => {
      repositionRafPending = false;
      repositionAll();
    });
  }

  function observeFieldResize(field, overlay) {
    if (typeof ResizeObserver === "undefined") return;
    const ro = new ResizeObserver(() => {
      positionOverlay(field, overlay);
      syncOverlayStyle(field, overlay);
    });
    ro.observe(field);
  }

  // --- Scanning ---

  function scanAndProtect(root) {
    if (!enabled) return;
    const selector = [
      'input[type="password"]',
      'input[autocomplete*="cc-"]',
      'input[autocomplete*="ssn"]',
      'input[autocomplete*="passport"]',
      "input",
      "textarea",
    ].join(",");

    const fields = root.querySelectorAll
      ? root.querySelectorAll(selector)
      : [];

    fields.forEach((field) => {
      if (isSensitiveField(field)) {
        createOverlay(field);
      }
    });
  }

  // --- Cleanup ---

  function removeOverlay(field) {
    const overlay = overlayMap.get(field);
    if (overlay) {
      overlay.remove();
      overlayMap.delete(field);
      field.removeAttribute(OVERLAY_ATTR);
      protectedCount = Math.max(0, protectedCount - 1);
      reportCount();
    }
  }

  function removeAllOverlays() {
    document
      .querySelectorAll(".streamshield-overlay")
      .forEach((el) => el.remove());
    const fields = document.querySelectorAll("[" + OVERLAY_ATTR + "]");
    fields.forEach((field) => {
      if (!field.classList.contains("streamshield-overlay")) {
        overlayMap.delete(field);
        field.removeAttribute(OVERLAY_ATTR);
      }
    });
    protectedCount = 0;
    reportCount();
  }

  // --- Communication ---

  function reportCount() {
    try {
      chrome.runtime.sendMessage({
        type: "FIELD_COUNT",
        count: protectedCount,
      });
    } catch (_) {
      // Extension context may be invalidated
    }
  }

  function loadState() {
    try {
      chrome.runtime.sendMessage({ type: "GET_STATE" }, (response) => {
        if (chrome.runtime.lastError) return;
        if (response && typeof response.enabled === "boolean") {
          enabled = response.enabled;
          if (enabled) {
            scanAndObserve(document);
          } else {
            removeAllOverlays();
          }
        }
      });
    } catch (_) {
      // Extension context may be invalidated
    }
  }

  try {
    chrome.runtime.onMessage.addListener((message) => {
      if (message.type === "SET_ENABLED") {
        enabled = message.enabled;
        if (enabled) {
          scanAndObserve(document);
        } else {
          removeAllOverlays();
        }
      }

      if (message.type === "GET_COUNT") {
        try {
          chrome.runtime.sendMessage({
            type: "FIELD_COUNT",
            count: protectedCount,
          });
        } catch (_) {
          // Extension context may be invalidated
        }
      }
    });
  } catch (_) {
    // Extension context may be invalidated
  }

  // --- MutationObserver for DOM changes ---

  const domObserver = new MutationObserver((mutations) => {
    if (!enabled) return;

    let needsScan = false;
    for (const mutation of mutations) {
      if (mutation.type === "childList" && mutation.addedNodes.length > 0) {
        for (const node of mutation.addedNodes) {
          if (node.nodeType === Node.ELEMENT_NODE) {
            if (isSensitiveField(node)) {
              createOverlay(node);
            }
            if (node.querySelectorAll) {
              needsScan = true;
            }
          }
        }
      }

      if (mutation.type === "attributes") {
        const target = mutation.target;
        if (
          target.nodeType === Node.ELEMENT_NODE &&
          !target.classList.contains("streamshield-overlay")
        ) {
          if (isSensitiveField(target) && !overlayMap.has(target)) {
            createOverlay(target);
          } else if (!isSensitiveField(target) && overlayMap.has(target)) {
            removeOverlay(target);
          }
        }
      }

      // Detect removed fields
      if (mutation.type === "childList" && mutation.removedNodes.length > 0) {
        for (const node of mutation.removedNodes) {
          if (node.nodeType === Node.ELEMENT_NODE) {
            if (overlayMap.has(node)) {
              removeOverlay(node);
            }
            if (node.querySelectorAll) {
              const removed = node.querySelectorAll("[" + OVERLAY_ATTR + "]");
              removed.forEach((field) => {
                if (!field.classList.contains("streamshield-overlay")) {
                  removeOverlay(field);
                }
              });
            }
          }
        }
      }
    }

    if (needsScan) {
      scanAndObserve(document);
    }
  });

  // --- IntersectionObserver for visibility ---

  let intersectionObserver;
  if (typeof IntersectionObserver !== "undefined") {
    intersectionObserver = new IntersectionObserver(
      (entries) => {
        entries.forEach((entry) => {
          const overlay = overlayMap.get(entry.target);
          if (overlay) {
            overlay.style.display = entry.isIntersecting ? "flex" : "none";
            if (entry.isIntersecting) {
              positionOverlay(entry.target, overlay);
            }
          }
        });
      },
      { threshold: 0 }
    );
  }

  function scanAndObserve(root) {
    scanAndProtect(root);
    if (intersectionObserver) {
      const fields = document.querySelectorAll("[" + OVERLAY_ATTR + "]");
      fields.forEach((field) => {
        if (!field.classList.contains("streamshield-overlay")) {
          intersectionObserver.observe(field);
        }
      });
    }
  }

  // --- SPA Navigation ---

  let lastTitle = document.title;
  let lastUrl = location.href;

  function checkNavigation() {
    if (document.title !== lastTitle || location.href !== lastUrl) {
      lastTitle = document.title;
      lastUrl = location.href;
      // Re-scan after navigation
      removeAllOverlays();
      if (enabled) {
        setTimeout(() => scanAndObserve(document), 200);
      }
    }
  }

  const titleObserver = new MutationObserver(checkNavigation);
  const titleEl = document.querySelector("title");
  if (titleEl) {
    titleObserver.observe(titleEl, { childList: true, characterData: true, subtree: true });
  }

  window.addEventListener("popstate", () => {
    checkNavigation();
  });

  window.addEventListener("hashchange", () => {
    checkNavigation();
  });

  // --- Event Listeners ---

  window.addEventListener("scroll", requestReposition, { passive: true });
  window.addEventListener("resize", requestReposition, { passive: true });

  // --- Init ---

  domObserver.observe(document.documentElement, {
    childList: true,
    subtree: true,
    attributes: true,
    attributeFilter: ["type", "name", "id", "placeholder", "autocomplete", "aria-label"],
  });

  loadState();
  scanAndObserve(document);
})();
