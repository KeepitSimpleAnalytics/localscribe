"""LocalScribe Backend - AI-powered grammar and text editing service."""

from pathlib import Path


def _read_version() -> str:
    """Read version from VERSION file at repository root."""
    # VERSION file is at repo root, one level up from app/
    version_file = Path(__file__).parent.parent / "VERSION"
    try:
        return version_file.read_text().strip()
    except FileNotFoundError:
        return "0.0.0"  # Fallback if VERSION file is missing


__version__ = _read_version()
