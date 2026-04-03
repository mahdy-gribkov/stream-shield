# StreamShield Testing Guide

All tests must pass before any release artifacts are built.

---

## 1. Unit Tests (Automated)

**Location:** `tests/StreamShield.Tests/`

```bash
dotnet test tests/StreamShield.Tests/StreamShield.Tests.csproj
```

| Suite | What it covers |
|-------|---------------|
| SensitivityPatternsTests | Keyword matching for all categories (financial, identity, auth, OTP). Custom patterns. False positive checks (email, name, address, phone). Hebrew keyword detection (5 tests). LabeledBy/HelpText detection (8 tests). Name matches former ID-only keywords (4 tests). |
| FieldTrackerTests | Track/untrack, unfocus, position tolerance matching, reveal-password detection, window-change cleanup, persistent field queries. |
| AppSettingsTests | Default values, color hex parsing, JSON serialization round-trip, corrupt input handling. |
| StreamShield.E2E | E2E detection against real WinForms UIA elements. Password, CC, Hebrew ID, SSN detection. Email false-positive check. FieldTracker multi-field and reveal-password. |

---

## 2. Integration Tests (Manual)

Run the Windows app with `dotnet run --project src/StreamShield/` and perform each test.

| ID | Scenario | Steps | Expected |
|----|----------|-------|----------|
| INT-01 | Password detection | Chrome > GitHub login > click password field | Overlay appears over password field |
| INT-02 | CC detection | Chrome > Stripe checkout > fill CC number | Overlay on CC field |
| INT-03 | SSN detection | Chrome > form with SSN field > focus | Overlay appears |
| INT-04 | Token detection | Chrome > AWS console > secret key field | Overlay appears |
| INT-05 | Reveal password | Click eye icon on password field | Overlay persists (FieldTracker catches position match) |
| INT-06 | Multi-field persistence | Fill CC > tab to expiry > tab to CVV | All 3 fields stay covered |
| INT-07 | Window change cleanup | Fill form > Alt+Tab to Notepad > Alt+Tab back | Overlays cleared for old context |
| INT-08 | Settings toggle | Disable via tray > verify overlays gone > re-enable | Overlays resume on re-enable |
| INT-09 | Custom pattern | Add "my-secret-field" in Settings > Patterns tab > focus matching field | Overlay appears |
| INT-10 | Fast scroll | Focus password > scroll page quickly | Overlay tracks within 50ms |
| INT-11 | Settings persistence | Change mask color > close app > reopen | Color setting preserved |
| INT-12 | Content mirror | Focus CC field > type digits | Mirror shows digits as you type |
| INT-13 | Password mirror | Focus password field > type characters | Mirror shows dots, not cleartext |
| INT-14 | Tray icon state | Enable/disable via tray menu | Green circle when enabled, red when disabled |
| INT-15 | Single instance | Run app twice | Second instance shows "already running" message |
| INT-16 | Address bar exclusion | Chrome > click address bar > type URL with "token" | NO overlay appears |
| INT-17 | Hebrew ID detection | Hebrew payment form > focus ת.ז. field | Overlay appears |
| INT-18 | Settings UI | Double-click tray icon | Settings opens, all 4 tabs functional |
| INT-19 | Padding control | Settings > Appearance > change padding 0-10 | NumericUpDown works precisely |
| INT-20 | Chrome password manager | Chrome > Settings > Passwords > view a password | Overlay appears |

---

## 3. Capture Compatibility Tests (Manual, Per-Service)

For EACH service below, start a screen share session, then run all sub-tests.

### Services to test

- Discord (window share + screen share)
- Zoom (screen share)
- Google Meet (tab share + screen share)
- Microsoft Teams (window share + screen share)
- OBS Studio (window capture + display capture)
- Skype (screen share)
- Slack Huddles (screen share)

### Sub-tests per service

| ID | Scenario | Expected in shared/captured view |
|----|----------|----------------------------------|
| CAP-xx-01 | Full screen share, focus password field | Grey mask visible, NO field content |
| CAP-xx-02 | Window share (Chrome), focus password | Grey mask visible, NO field content |
| CAP-xx-03 | Multiple sensitive fields filled | ALL fields show grey masks |
| CAP-xx-04 | Content mirror visible locally | User sees typed content in mirror overlay |
| CAP-xx-05 | Settings window open during share | No sensitive data visible in shared view |
| CAP-xx-06 | Snipping Tool screenshot during share | Screenshot shows grey masks |
| CAP-xx-07 | Alt+PrintScreen during share | Screenshot shows grey masks |

Replace `xx` with service abbreviation: DC (Discord), ZM (Zoom), GM (Google Meet), TM (Teams), OB (OBS), SK (Skype), SL (Slack).

### Additional capture tools

| Tool | Expected |
|------|----------|
| Windows Snipping Tool | Grey masks visible (WDA_EXCLUDEFROMCAPTURE respected) |
| Xbox Game Bar recording | Grey masks in recorded video |
| PrintScreen / Alt+PrintScreen | Grey masks in clipboard image |

---

## 4. Security Tests

| ID | Scenario | Steps | Expected |
|----|----------|-------|----------|
| SEC-01 | Log file audit | Use app for 5 minutes with sensitive fields. Inspect `%APPDATA%\StreamShield\log.txt` | NO field values, names, or content in logs |
| SEC-02 | Settings file audit | Inspect `%APPDATA%\StreamShield\settings.json` | Only colors, padding, enabled state, custom keywords. No sensitive data. |
| SEC-03 | UIA mirror inspection | Use Accessibility Insights to inspect the mirror overlay window | Mirror content should NOT be readable via UIA (WDA blocks) |
| SEC-04 | Extension DOM audit | Open DevTools on page with protected field. Inspect overlay element. | Overlay div exists but field value not in storage, network, or console |
| SEC-05 | Extension network audit | Open DevTools Network tab. Interact with protected fields. | Zero network requests from extension |
| SEC-06 | Memory dump check | Attach debugger to StreamShield process. Search memory for typed values. | Field values exist in mirror window text but are transient, not persisted |

---

## 5. Performance Tests

| ID | Metric | Target | How to verify |
|----|--------|--------|---------------|
| PERF-01 | CPU idle (no sensitive field) | < 0.1% | Open Task Manager, observe StreamShield process with no fields focused |
| PERF-02 | CPU active (field focused, 50ms timer) | < 1% | Focus a sensitive field, observe CPU in Task Manager |
| PERF-03 | Memory baseline | < 30 MB | Task Manager, no overlays active |
| PERF-04 | Memory with 10 overlays | < 50 MB | Fill 10 sensitive fields, check memory |
| PERF-05 | Focus detection latency | < 100ms | Click on password field, measure time until overlay appears |
| PERF-06 | Overlay position lag | < 50ms | Scroll page with focused field, observe overlay tracking |
| PERF-07 | Startup time | < 2 seconds | Time from exe launch to tray icon visible |
| PERF-08 | 1-hour stability | No memory leak, no crash | Run app for 1 hour with Chrome open, monitor memory |
| PERF-09 | GDI handle stability | < 0 growth/hr | Run app 30 min, monitor GDI handles in Task Manager |

---

## 6. Chrome Extension Tests

| ID | Scenario | Steps | Expected |
|----|----------|-------|----------|
| EXT-01 | Password field detection | Navigate to login page | Password field has overlay |
| EXT-02 | CC field detection | Navigate to payment page | CC fields have overlays |
| EXT-03 | Type in protected field | Type in password field | Overlay updates with dots |
| EXT-04 | SPA navigation | Navigate within SPA | Old overlays removed, new ones detected |
| EXT-05 | Window resize | Resize browser window | Overlays reposition correctly |
| EXT-06 | Extension toggle | Disable extension via popup | All overlays removed |
| EXT-07 | Badge count | Focus 3 sensitive fields | Extension badge shows "3" |
| EXT-08 | Multiple tabs | Open login pages in 3 tabs | Each tab tracks independently |

---

## 7. Integrity Gate

ALL of the following must pass before tagging a release:

- [ ] All unit tests pass (`dotnet test`)
- [ ] All E2E tests pass (`dotnet test tests/StreamShield.E2E/`)
- [ ] All INT-xx integration tests pass
- [ ] At least Discord + Zoom + Meet + Teams capture tests pass
- [ ] All SEC-xx security tests pass
- [ ] All PERF-xx performance tests pass
- [ ] All EXT-xx extension tests pass
- [ ] CI pipeline is green on main branch
- [ ] README is accurate and complete
- [ ] No field values are logged anywhere in the codebase (verified by grep)
- [ ] Extension manifest has no unnecessary permissions

**No release artifacts are built until ALL gates pass.**
