"""Prompt templates and builders for each editing mode."""

from __future__ import annotations

from typing import Any

from app.types import Mode, ToneStyle

PROMPTS: dict[Mode, dict[str, Any]] = {
    "proofread": {
        "system": (
            "You are a meticulous copy editor. Fix grammar, spelling, and punctuation errors. "
            "Make the smallest changes possible while preserving voice and meaning. "
            "Return only the corrected text."
        ),
    },
    "rewrite": {
        "system": (
            "You rewrite text to improve clarity and flow. "
            "Reduce redundancy, keep the tone neutral, and preserve original intent."
        ),
    },
    "technical": {
        "system": (
            "You are a cybersecurity-focused technical editor. "
            "Improve precision and clarity while keeping all technical facts intact. "
            "Assume the reader is a security practitioner or researcher."
        ),
    },
    "tone": {
        "system": (
            "You adjust tone according to the requested style while maintaining the underlying facts. "
            "Do not introduce new information. Return only the updated text."
        ),
    },
}

TONE_PROMPTS: dict[ToneStyle, str] = {
    "professional": (
        "Rewrite the text with a confident, respectful professional voice suitable for business communication."
    ),
    "concise": "Rewrite the text so it is brief, direct, and free of filler while staying polite.",
    "friendly": "Rewrite the text so it sounds approachable, warm, and encouraging.",
}


def build_messages(
    text: str,
    mode: Mode,
    tone: ToneStyle | None = None,
    extra_instructions: str | None = None,
) -> list[dict[str, str]]:
    """Build chat messages for the downstream LLM."""

    prompt_def = PROMPTS[mode]
    system_prompt = prompt_def["system"]

    if mode == "tone":
        if not tone:
            raise ValueError("Tone mode requires 'tone' to be provided.")
        tone_instruction = TONE_PROMPTS[tone]
        system_prompt = f"{system_prompt} {tone_instruction}"

    if extra_instructions:
        system_prompt = f"{system_prompt} Additional guidance: {extra_instructions.strip()}"

    user_prompt = (
        "Apply the instructions to the text below and respond with edited text only.\n\n"
        f"Text:\n{text.strip()}"
    )

    return [
        {"role": "system", "content": system_prompt},
        {"role": "user", "content": user_prompt},
    ]
