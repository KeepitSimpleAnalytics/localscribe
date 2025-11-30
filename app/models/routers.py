"""Routing helpers for selecting models per editing mode."""

from __future__ import annotations

from app.types import Mode

MODE_TO_MODEL_KEY: dict[Mode, str] = {
    "proofread": "grammar",
    "rewrite": "general",
    "tone": "general",
    "technical": "general",
}


def resolve_model_key(mode: Mode) -> str:
    """Return the logical model key for a given mode."""
    return MODE_TO_MODEL_KEY[mode]
