"""Simple sanity check script to exercise each editing mode."""

from __future__ import annotations

import json
from typing import Any

import httpx

API_URL = "http://localhost:8000/v1/text/edit"


def run_sample(text: str, payload: dict[str, Any]) -> None:
    """Send a sample request and dump the response."""
    with httpx.Client(timeout=60.0) as client:
        response = client.post(API_URL, json={"text": text, **payload})
        response.raise_for_status()
        data = response.json()
        print(f"Mode: {data['mode']}")
        print(f"Model: {data['model']['name']}")
        print(f"Output: {data['output_text']}")
        print("-" * 60)


def main() -> None:
    """Invoke each editing mode with canned text."""
    sample_text = "this are sample text needing help explaining a security finding."
    scenarios = [
        {"mode": "proofread"},
        {"mode": "rewrite"},
        {"mode": "tone", "tone": "professional"},
        {"mode": "technical", "extra_instructions": "Focus on practical remediation."},
    ]

    for payload in scenarios:
        run_sample(sample_text, payload)


if __name__ == "__main__":
    main()
