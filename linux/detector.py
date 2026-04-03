"""Sensitive field detection via AT-SPI2 accessibility API.

Subscribes to focus events from the accessibility bus. When an accessible
object gains focus, inspects its role, name, and description to determine
if it is a sensitive input field (password, credit card, SSN, etc.).

Uses the AT-SPI2 Component interface to retrieve bounding boxes for overlay
positioning.
"""

from __future__ import annotations

import hashlib
import logging
from collections.abc import Callable
from typing import TypedDict

import gi

gi.require_version("Atspi", "2.0")
from gi.repository import Atspi  # noqa: E402

logger = logging.getLogger("stream-shield.detector")


class DetectionResult(TypedDict):
    kind: str  # "password" | "sensitive_text"
    bounds: tuple[int, int, int, int]  # x, y, width, height
    description: str
    field_id: str


# ---------------------------------------------------------------------------
# Keyword lists - mirrored from the Windows C# SensitivityPatterns.cs
# ---------------------------------------------------------------------------

KEYWORDS: tuple[str, ...] = (
    # Financial
    "card number", "credit card", "debit card", "cc number", "card num",
    "cardnumber", "cc-number", "ccnum", "creditcard", "debitcard",
    "card-number", "card_number", "cc_number", "cc-num",
    "cvv", "cvc", "csc", "cvv2", "cvc2", "cid",
    "security code", "securitycode", "card code", "cardcode",
    "expiry", "expiration", "exp date", "expdate",
    "card-expiry", "card_expiry", "card-exp",
    "iban", "routing number", "routing", "account number",
    "accountnumber", "account-number", "account_number",
    "bank account", "bank code", "branch code",
    "sort code", "sortcode", "sort-code",
    "swift", "swift-code", "bic", "bic-code",
    "pin number",
    # Identity
    "ssn", "social security", "social-security",
    "passport", "national id", "national-id",
    "driver license", "driver-license", "drivers license",
    "license number", "license-number",
    "tax id", "tax-id", "ein", "tin",
    "id number", "id-number", "idnumber",
    "state id", "green card", "greencard",
    "visa number", "visa-number",
    "dob", "date of birth", "date-of-birth",
    "member id", "member-id", "memberid",
    "patient id", "patient-id", "patient_id",
    "insurance number", "insurance-number",
    "maiden",
    # Authentication
    "password",
    "secret", "secret key", "secret-key",
    "token", "access token", "access-token",
    "api key", "api-key", "apikey",
    "private key", "private-key",
    "client secret", "client-secret",
    "bearer", "bearer-token",
    "authorization", "auth code", "authcode",
    "recovery code", "recovery-code",
    "backup code", "backup-code",
    "verification code", "verification-code",
    "passphrase", "credential", "oauth",
    # MFA / OTP
    "pin", "otp", "totp", "mfa",
    "2fa", "one-time", "mfa code",
    "pin-input",
    # Hebrew
    "\u05ea.\u05d6",                     # ת.ז
    "\u05ea\u05e2\u05d5\u05d3\u05ea \u05d6\u05d4\u05d5\u05ea",  # תעודת זהות
    "\u05db\u05e8\u05d8\u05d9\u05e1 \u05d0\u05e9\u05e8\u05d0\u05d9",  # כרטיס אשראי
    "\u05de\u05e1\u05e4\u05e8 \u05db\u05e8\u05d8\u05d9\u05e1",  # מספר כרטיס
    "\u05e1\u05d9\u05e1\u05de\u05d4",   # סיסמה
    "\u05e1\u05d9\u05e1\u05de\u05d0",   # סיסמא
    "\u05e7\u05d5\u05d3 \u05d0\u05d1\u05d8\u05d7\u05d4",  # קוד אבטחה
    "\u05e7\u05d5\u05d3 \u05e1\u05d5\u05d3\u05d9",  # קוד סודי
    "\u05ea\u05d5\u05e7\u05e3",         # תוקף
    "\u05de\u05e1\u05e4\u05e8 \u05d7\u05e9\u05d1\u05d5\u05df",  # מספר חשבון
)

# Roles that represent text entry fields in AT-SPI2
_ENTRY_ROLES: frozenset[int] = frozenset({
    Atspi.Role.ENTRY,
    Atspi.Role.PASSWORD_TEXT,
    Atspi.Role.SPIN_BUTTON,       # Some number inputs use this
    Atspi.Role.TEXT,              # Generic text role in some toolkits
    Atspi.Role.DOCUMENT_TEXT,     # Editable document regions
})


def _matches_any(value: str | None, keywords: tuple[str, ...]) -> bool:
    """Case-insensitive substring match against keyword list."""
    if not value:
        return False
    lower = value.lower()
    return any(kw in lower for kw in keywords)


def _is_sensitive_field(name: str | None, description: str | None) -> bool:
    """Check name and description against the unified keyword list."""
    return _matches_any(name, KEYWORDS) or _matches_any(description, KEYWORDS)


def _get_component_bounds(accessible: Atspi.Accessible) -> tuple[int, int, int, int] | None:
    """Get the screen-space bounding box via AT-SPI2 Component interface.

    Returns (x, y, width, height) or None if unavailable.

    NOTE: Linux-specific testing needed. The coordinate type SCREEN should
    give absolute screen coordinates, but Wayland compositors may return
    (0, 0) for security reasons. X11 should work correctly.
    """
    try:
        component = accessible.get_component_iface()
        if component is None:
            return None

        # get_extents returns an Atspi.Rect with x, y, width, height
        rect = component.get_extents(Atspi.CoordType.SCREEN)
        if rect.width < 10 or rect.height < 5:
            return None
        return (rect.x, rect.y, rect.width, rect.height)
    except Exception:
        logger.debug("Failed to get component bounds", exc_info=True)
        return None


def _make_field_id(bounds: tuple[int, int, int, int], name: str | None) -> str:
    """Generate a stable ID for a field based on position and name.

    Quantizes position to a 15px grid (same as Windows version) so small
    position jitter does not create duplicate overlays.
    """
    qx = bounds[0] // 15
    qy = bounds[1] // 15
    raw = f"{qx}:{qy}:{name or ''}"
    return hashlib.md5(raw.encode(), usedforsecurity=False).hexdigest()[:12]


class SensitiveFieldDetector:
    """Monitors AT-SPI2 focus events and detects sensitive input fields.

    Calls `on_detection(result)` when a sensitive field gains focus, or
    `on_detection(None)` when focus moves to a non-sensitive element.
    """

    def __init__(self, on_detection: Callable[[DetectionResult | None], None]) -> None:
        self._on_detection = on_detection
        self._listener_registered = False
        self._running = False

    def start(self) -> None:
        """Begin monitoring AT-SPI2 focus events.

        NOTE: Linux-specific testing needed. pyatspi2/Atspi event registration
        requires the AT-SPI2 bus to be running (typically started by the desktop
        environment). If AT-SPI2 is not available, this will log an error.
        """
        if self._running:
            return

        try:
            Atspi.Event.listener_register(
                Atspi.EventListener.new(self._on_focus_event),
                "focus:",
            )
            # Also listen for object:state-changed:focused which some toolkits use
            Atspi.Event.listener_register(
                Atspi.EventListener.new(self._on_state_changed_focus),
                "object:state-changed:focused",
            )
            self._listener_registered = True
            self._running = True
            logger.info("AT-SPI2 focus listeners registered")
        except Exception:
            logger.error(
                "Failed to register AT-SPI2 listeners. "
                "Is at-spi2-core running? Install with: "
                "sudo apt install at-spi2-core",
                exc_info=True,
            )

    def stop(self) -> None:
        """Stop monitoring focus events.

        NOTE: Linux-specific testing needed. Atspi event deregistration
        may not cleanly remove all listeners on all distros.
        """
        if not self._running:
            return
        self._running = False
        # AT-SPI2 does not provide a clean per-listener deregister in all
        # versions of pyatspi. We mark ourselves as stopped and ignore
        # subsequent callbacks.
        logger.info("AT-SPI2 focus monitoring stopped")

    def _on_focus_event(self, event: Atspi.Event) -> None:
        """Handle AT-SPI2 focus: events."""
        if not self._running:
            return
        self._inspect(event.source)

    def _on_state_changed_focus(self, event: Atspi.Event) -> None:
        """Handle object:state-changed:focused events.

        detail1 == 1 means the object gained focus, 0 means lost focus.
        """
        if not self._running:
            return
        if event.detail1 == 1:
            self._inspect(event.source)

    def _inspect(self, accessible: Atspi.Accessible | None) -> None:
        """Inspect an accessible object for sensitivity."""
        if accessible is None:
            self._on_detection(None)
            return

        try:
            role = accessible.get_role()
        except Exception:
            logger.debug("Failed to get role from accessible", exc_info=True)
            self._on_detection(None)
            return

        # Only inspect text entry fields
        if role not in _ENTRY_ROLES:
            self._on_detection(None)
            return

        # Password fields are always sensitive
        if role == Atspi.Role.PASSWORD_TEXT:
            bounds = _get_component_bounds(accessible)
            if bounds is None:
                self._on_detection(None)
                return

            field_id = _make_field_id(bounds, "__password__")
            self._on_detection(DetectionResult(
                kind="password",
                bounds=bounds,
                description="Password field",
                field_id=field_id,
            ))
            return

        # For other entry roles, check name/description against keywords
        try:
            name = accessible.get_name()
            description = accessible.get_description()
        except Exception:
            logger.debug("Failed to read accessible name/description", exc_info=True)
            self._on_detection(None)
            return

        if _is_sensitive_field(name, description):
            bounds = _get_component_bounds(accessible)
            if bounds is None:
                self._on_detection(None)
                return

            desc_text = name or description or "Sensitive field"
            field_id = _make_field_id(bounds, name)
            self._on_detection(DetectionResult(
                kind="sensitive_text",
                bounds=bounds,
                description=desc_text,
                field_id=field_id,
            ))
            return

        # Also check accessible relations and attributes for sensitivity hints
        # NOTE: Linux-specific testing needed. Some toolkits (GTK, Qt) expose
        # autocomplete="cc-number" or similar attributes via AT-SPI2 object
        # attributes. This is a future enhancement.

        self._on_detection(None)
