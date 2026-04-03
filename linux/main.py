"""StreamShield Linux - Entry point.

Monitors accessible input fields via AT-SPI2 and shows overlay warnings
over sensitive fields (passwords, credit cards, SSN, etc.).

Note: Linux has no capture exclusion API like Windows WDA_EXCLUDEFROMCAPTURE.
The overlay is visible to both the user and screen capture software.
The Chrome extension provides actual browser-level protection on Linux.
"""

from __future__ import annotations

import argparse
import logging
import signal
import sys
from pathlib import Path

import gi

gi.require_version("Gtk", "4.0")
from gi.repository import GLib, Gtk  # noqa: E402

from detector import SensitiveFieldDetector  # noqa: E402
from overlay import OverlayManager  # noqa: E402
from tray import TrayManager  # noqa: E402

LOG_DIR = Path.home() / ".local" / "share" / "stream-shield"
LOG_FILE = LOG_DIR / "stream-shield.log"


def setup_logging() -> None:
    LOG_DIR.mkdir(parents=True, exist_ok=True)
    logging.basicConfig(
        level=logging.INFO,
        format="[%(asctime)s] %(levelname)s %(name)s: %(message)s",
        datefmt="%Y-%m-%dT%H:%M:%S",
        handlers=[
            logging.FileHandler(LOG_FILE, encoding="utf-8"),
            logging.StreamHandler(sys.stderr),
        ],
    )


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        prog="stream-shield",
        description="StreamShield Linux - Sensitive input field detection",
    )
    parser.add_argument(
        "--disable",
        action="store_true",
        help="Start with detection disabled (can enable via tray menu)",
    )
    return parser.parse_args()


class StreamShieldApp:
    """Top-level application. Wires detector, overlay manager, and tray together."""

    def __init__(self, *, start_disabled: bool = False) -> None:
        self.logger = logging.getLogger("stream-shield")
        self.enabled = not start_disabled

        self.overlay_manager = OverlayManager()
        self.detector = SensitiveFieldDetector(on_detection=self._on_detection)
        self.tray = TrayManager(
            enabled=self.enabled,
            on_toggle=self._on_toggle,
            on_quit=self._on_quit,
        )

        if self.enabled:
            self.detector.start()
            self.logger.info("Detection started")
        else:
            self.logger.info("Started in disabled mode")

    def _on_detection(self, result: dict | None) -> None:
        """Called from detector when focus changes.

        result is None when focus moves to a non-sensitive element.
        Otherwise it contains: kind, bounds (x, y, w, h), description.
        """
        if not self.enabled:
            return

        if result is None:
            self.overlay_manager.hide_all()
            return

        self.overlay_manager.show_overlay(
            field_id=result["field_id"],
            x=result["bounds"][0],
            y=result["bounds"][1],
            width=result["bounds"][2],
            height=result["bounds"][3],
            label=result["description"],
        )

    def _on_toggle(self, enabled: bool) -> None:
        self.enabled = enabled
        if enabled:
            self.detector.start()
            self.logger.info("Detection enabled")
        else:
            self.detector.stop()
            self.overlay_manager.hide_all()
            self.logger.info("Detection disabled")

    def _on_quit(self) -> None:
        self.logger.info("Shutting down")
        self.detector.stop()
        self.overlay_manager.hide_all()
        self.tray.cleanup()
        app_loop = Gtk.Application.get_default()
        if app_loop is not None:
            app_loop.quit()


def main() -> None:
    setup_logging()
    logger = logging.getLogger("stream-shield")
    args = parse_args()

    logger.info("StreamShield Linux starting")

    # GTK4 Application as the main loop driver
    gtk_app = Gtk.Application(application_id="com.medygribkov.streamshield")

    shield: StreamShieldApp | None = None

    def on_activate(_app: Gtk.Application) -> None:
        nonlocal shield
        shield = StreamShieldApp(start_disabled=args.disable)
        # Hold the application open (no visible window needed)
        _app.hold()

    gtk_app.connect("activate", on_activate)

    # Clean shutdown on SIGINT/SIGTERM
    def handle_signal(sig: int, _frame: object) -> None:
        logger.info("Received signal %d, shutting down", sig)
        if shield is not None:
            shield._on_quit()
        else:
            gtk_app.quit()

    signal.signal(signal.SIGINT, handle_signal)
    signal.signal(signal.SIGTERM, handle_signal)

    # GLib needs to be told to handle Unix signals in the main loop
    # NOTE: Linux-specific testing needed. GLib.unix_signal_add may be required
    # on some distros for signal delivery inside the GTK main loop.
    GLib.unix_signal_add(GLib.PRIORITY_DEFAULT, signal.SIGINT, gtk_app.quit)
    GLib.unix_signal_add(GLib.PRIORITY_DEFAULT, signal.SIGTERM, gtk_app.quit)

    exit_code = gtk_app.run(None)
    logger.info("StreamShield Linux exiting")
    sys.exit(exit_code)


if __name__ == "__main__":
    main()
