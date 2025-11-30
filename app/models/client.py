"""Model client abstraction layer."""

from __future__ import annotations

from dataclasses import dataclass
from typing import Any

import httpx

from app.config import ModelConfig


class ModelClientError(RuntimeError):
    """Raised when a downstream model call fails."""


@dataclass
class ChatMessage:
    """Minimal structure describing a chat message."""

    role: str
    content: str


class ModelClient:
    """HTTP client for Ollama or OpenAI-compatible chat endpoints."""

    def __init__(self, config: ModelConfig, *, timeout: float = 120.0) -> None:
        self.config = config
        self.timeout = timeout

    async def generate(self, messages: list[dict[str, str]]) -> str:
        """Call the downstream model with the constructed messages."""
        payload = self._build_payload(messages)

        async with httpx.AsyncClient(timeout=self.timeout) as client:
            try:
                response = await client.post(self.config.endpoint, json=payload)
                response.raise_for_status()
            except httpx.HTTPError as exc:  # pragma: no cover - network errors
                raise ModelClientError(str(exc)) from exc

        data = response.json()
        return self._parse_response(data)

    def _build_payload(self, messages: list[dict[str, str]]) -> dict[str, Any]:
        """Translate messages to the downstream API shape."""
        if self.config.api_type == "ollama":
            return {
                "model": self.config.name,
                "messages": messages,
                "stream": False,
                "options": {
                    "temperature": self.config.temperature,
                    "num_predict": self.config.max_tokens,
                    "top_p": self.config.top_p,
                },
            }

        return {
            "model": self.config.name,
            "messages": messages,
            "temperature": self.config.temperature,
            "max_tokens": self.config.max_tokens,
            "top_p": self.config.top_p,
        }

    def _parse_response(self, payload: dict[str, Any]) -> str:
        """Extract text from both Ollama and OpenAI compatible responses."""
        if self.config.api_type == "ollama":
            message = payload.get("message")
            if not message:
                raise ModelClientError("Malformed Ollama response: missing 'message'.")
            return message.get("content", "").strip()

        choices = payload.get("choices")
        if not choices:
            raise ModelClientError("Malformed OpenAI response: missing 'choices'.")
        return choices[0]["message"]["content"].strip()
