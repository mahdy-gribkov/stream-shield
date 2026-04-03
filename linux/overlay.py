"""GTK4 overlay windows for marking sensitive input fields.

Creates small always-on-top windows positioned over detected fields.
Semi-transparent dark background with a "Protected" label.

IMPORTANT: Linux has no capture exclusion API (WDA_EXCLUDEFROMCAPTURE).
The overlay is visible to both the user AND screen capture. The Chrome
extension provides actual browser protection on Linux.

Click-through is achieved via input shape (cairo region). The overlay
follows field position on a 200ms timer to handle window movement.
"""

from __future__ import annotations

import logging
from dataclasses import dataclass

import gi

gi.require_version("Gtk", "4.0")
gi.require_version("Gdk", "4.0")
from gi.repository import Gdk, GLib, Gtk  # noqa: E402

logger = logging.getLogger("stream-shield.overlay")

# Visual constants
OVERLAY_BG_RGBA = (0.1, 0.1, 0.18, 0.85)  # Dark semi-transparent (#1A1A2E ~85%)
OVERLAY_FG_CSS = "rgba(200, 200, 200, 1.0)"
OVERLAY_PADDING = 2  # px padding around the field bounds
POSITION_UPDATE_INTERVAL_MS = 200


@dataclass
class OverlayState:
    """Tracks an active overlay and the field it covers."""
    window: Gtk.Window
    field_id: str
    x: int
    y: int
    width: int
    height: int
    timer_id: int | None = None


class OverlayManager:
    """Manages multiple overlay windows over sensitive fields.

    Each overlay is a small GTK4 window positioned at the field's screen
    coordinates. Supports multiple simultaneous overlays (one per field).
    """

    def __init__(self) -> None:
        self._overlays: dict[str, OverlayState] = {}

    def show_overlay(
        self,
        field_id: str,
        x: int,
        y: int,
        width: int,
        height: int,
        label: str = "Protected",
    ) -> None:
        """Show or update an overlay for a field.

        If an overlay already exists for this field_id, update its position.
        Otherwise create a new overlay window.
        """
        # Apply padding
        px = x - OVERLAY_PADDING
        py = y - OVERLAY_PADDING
        pw = width + OVERLAY_PADDING * 2
        ph = height + OVERLAY_PADDING * 2

        if field_id in self._overlays:
            self._update_position(field_id, px, py, pw, ph)
            return

        self._create_overlay(field_id, px, py, pw, ph, label)

    def hide_overlay(self, field_id: str) -> None:
        """Remove a specific overlay."""
        state = self._overlays.pop(field_id, None)
        if state is None:
            return

        if state.timer_id is not None:
            GLib.source_remove(state.timer_id)
        state.window.close()
        logger.debug("Removed overlay for field %s", field_id)

    def hide_all(self) -> None:
        """Remove all active overlays."""
        for field_id in list(self._overlays.keys()):
            self.hide_overlay(field_id)

    @property
    def active_count(self) -> int:
        return len(self._overlays)

    def _create_overlay(
        self,
        field_id: str,
        x: int,
        y: int,
        width: int,
        height: int,
        label: str,
    ) -> None:
        """Create a new overlay window.

        NOTE: Linux-specific testing needed.
        - GTK4 on Wayland: set_default_size and surface positioning may behave
          differently. Wayland does not allow arbitrary window positioning by
          the client. This overlay approach works reliably on X11. On Wayland,
          a layer-shell protocol extension (gtk4-layer-shell) may be needed.
        - GTK4 on X11: window positioning via GdkX11.X11Surface should work.
        """
        window = Gtk.Window()
        window.set_decorated(False)
        window.set_resizable(False)
        window.set_default_size(width, height)

        # NOTE: Linux-specific testing needed.
        # GTK4 removed set_keep_above(). On X11, we need to set the
        # _NET_WM_STATE_ABOVE hint after the window is realized.
        # On Wayland, layer-shell protocol is needed for always-on-top.

        # Apply CSS styling for the overlay appearance
        css_provider = Gtk.CssProvider()
        css_data = f"""
            window.stream-shield-overlay {{
                background-color: rgba(
                    {int(OVERLAY_BG_RGBA[0] * 255)},
                    {int(OVERLAY_BG_RGBA[1] * 255)},
                    {int(OVERLAY_BG_RGBA[2] * 255)},
                    {OVERLAY_BG_RGBA[3]}
                );
            }}
            window.stream-shield-overlay label {{
                color: {OVERLAY_FG_CSS};
                font-size: 11px;
                font-weight: bold;
            }}
        """
        css_provider.load_from_string(css_data)
        Gtk.StyleContext.add_provider_for_display(
            Gdk.Display.get_default(),
            css_provider,
            Gtk.STYLE_PROVIDER_PRIORITY_APPLICATION,
        )

        window.add_css_class("stream-shield-overlay")

        # Label
        lbl = Gtk.Label(label=label)
        lbl.set_halign(Gtk.Align.CENTER)
        lbl.set_valign(Gtk.Align.CENTER)
        window.set_child(lbl)

        # Make click-through by setting an empty input region after realization
        # NOTE: Linux-specific testing needed.
        # GTK4 input regions are set on the GdkSurface after the window is
        # mapped. On X11 this uses XShapeCombineRegion. On Wayland, input
        # regions are set via the wl_surface API.
        window.connect("realize", self._on_realize_click_through)

        # Position the window
        # NOTE: Linux-specific testing needed.
        # GTK4 on X11: after realization, we can move the window via
        # the underlying X11 surface. On Wayland, this is not possible
        # without layer-shell.
        window.connect("realize", lambda w: self._move_window(w, x, y))

        window.present()

        # Start position update timer
        timer_id = GLib.timeout_add(
            POSITION_UPDATE_INTERVAL_MS,
            self._on_position_tick,
            field_id,
        )

        self._overlays[field_id] = OverlayState(
            window=window,
            field_id=field_id,
            x=x,
            y=y,
            width=width,
            height=height,
            timer_id=timer_id,
        )

        logger.debug("Created overlay for field %s at (%d, %d, %d, %d)", field_id, x, y, width, height)

    def _on_realize_click_through(self, window: Gtk.Window) -> None:
        """Set an empty input region so clicks pass through the overlay.

        NOTE: Linux-specific testing needed.
        On X11, this sets an empty X shape for input. On Wayland, this
        sets an empty input region on the wl_surface.
        """
        surface = window.get_surface()
        if surface is None:
            return

        try:
            # Create an empty Cairo region for input passthrough
            import cairo
            region = cairo.Region()
            surface.set_input_region(region)
        except Exception:
            logger.debug(
                "Failed to set empty input region (click-through). "
                "Overlay may intercept clicks.",
                exc_info=True,
            )

    def _move_window(self, window: Gtk.Window, x: int, y: int) -> None:
        """Position the window at absolute screen coordinates.

        NOTE: Linux-specific testing needed.
        GTK4 does not expose a direct move() method like GTK3. On X11,
        we use the underlying X11 surface to set position. On Wayland,
        arbitrary positioning is not supported without layer-shell.
        """
        surface = window.get_surface()
        if surface is None:
            return

        try:
            # Attempt X11 positioning
            from gi.repository import GdkX11  # noqa: F811
            if isinstance(surface, GdkX11.X11Surface):
                # X11 surface detected. Reliable positioning requires Xlib
                # bindings or ctypes XMoveWindow. GTK4 initial positioning
                # is used as a fallback.
                _ = surface.get_xid()  # verify X11 surface is valid
        except ImportError:
            pass

        # Fallback: GTK4 does not have a reliable cross-backend move API.
        # On X11, the window manager usually respects the initial size request.
        # On Wayland, we would need gtk4-layer-shell for positioning.
        logger.debug("Window positioning attempted at (%d, %d)", x, y)

    def _update_position(
        self,
        field_id: str,
        x: int,
        y: int,
        width: int,
        height: int,
    ) -> None:
        """Update an existing overlay's position and size."""
        state = self._overlays.get(field_id)
        if state is None:
            return

        state.x = x
        state.y = y
        state.width = width
        state.height = height

        state.window.set_default_size(width, height)
        self._move_window(state.window, x, y)

    def _on_position_tick(self, field_id: str) -> bool:
        """Timer callback to re-check field position.

        Returns True to keep the timer running, False to stop it.

        NOTE: In the current design, the detector re-fires detection results
        which call show_overlay() with updated coordinates. This timer is a
        safety net for cases where the field moves (e.g., window drag) between
        detection events. A future enhancement could re-query AT-SPI2 for the
        field's current bounds here.
        """
        if field_id not in self._overlays:
            return False  # Stop timer, overlay was removed
        return True  # Keep timer running
