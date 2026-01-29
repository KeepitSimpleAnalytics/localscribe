"""Editing service orchestrating prompt assembly and model invocation."""

from __future__ import annotations

import time
from dataclasses import dataclass

from app.config import ModelConfig
from app.config_manager import ConfigManager
from app.logging_utils import get_logger
from app.models.client import ModelClient
from app.models.routers import resolve_model_key
from app.prompts import build_messages
from app.types import Mode, ToneStyle

logger = get_logger(__name__)


@dataclass
class EditResult:
    """Structured response returned by the editing service."""

    mode: Mode
    model_config: ModelConfig
    output_text: str
    latency_ms: float


class EditingService:
    """Service combining prompts, routing, and downstream model calls."""

    def __init__(self, *, config_manager: ConfigManager, timeout: float) -> None:
        self._config_manager = config_manager
        self._timeout = timeout

    async def edit(
        self,
        *,
        text: str,
        mode: Mode,
        tone: ToneStyle | None = None,
        extra_instructions: str | None = None,
    ) -> EditResult:
        """Route request to the correct model and return structured result."""
        model_key = resolve_model_key(mode)
        model_config = self._config_manager.get_model_config(model_key)
        client = ModelClient(model_config, timeout=self._timeout)

        messages = build_messages(
            text=text,
            mode=mode,
            tone=tone,
            extra_instructions=extra_instructions,
        )

        start = time.perf_counter()
        response = await client.generate(messages)
        output_text = response.content
        latency_ms = (time.perf_counter() - start) * 1000

        logger.debug(
            "Editing completed | mode=%s model=%s latency_ms=%.2f text_len=%d",
            mode,
            model_config.name,
            latency_ms,
            len(text),
        )

        return EditResult(
            mode=mode,
            model_config=model_config,
            output_text=output_text,
            latency_ms=latency_ms,
        )
