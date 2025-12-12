"""Runtime configuration manager for model routing."""

from __future__ import annotations

import json
import threading
from dataclasses import dataclass
from pathlib import Path
from typing import Any

import httpx

from app.config import settings
from app.config import ModelConfig

DEFAULT_RUNTIME_CONFIG = {
    "ollama_base_url": "http://10.8.14.169:11434",
    "grammar_model": "llama3:instruct",
    "general_model": "qwen2:7b-instruct",
}


@dataclass
class RuntimeConfig:
    """Runtime configuration persisted to disk."""

    ollama_base_url: str
    grammar_model: str
    general_model: str


class ConfigManager:
    """Loads and persists runtime configuration."""

    def __init__(self, path: Path) -> None:
        self._path = path
        self._lock = threading.Lock()
        self._config = self._load()

    def _load(self) -> RuntimeConfig:
        self._path.parent.mkdir(parents=True, exist_ok=True)
        if not self._path.exists():
            self._write(DEFAULT_RUNTIME_CONFIG)
        with self._path.open("r", encoding="utf-8") as file:
            data = json.load(file)
            # No longer need to handle migration/missing keys for analysis_model as it's removed
        return RuntimeConfig(
            ollama_base_url=data["ollama_base_url"].strip().rstrip("/"),
            grammar_model=data["grammar_model"].strip(),
            general_model=data["general_model"].strip(),
        )

    def _write(self, data: dict[str, Any]) -> None:
        self._path.write_text(json.dumps(data, indent=2), encoding="utf-8")

    def get_runtime_config(self) -> RuntimeConfig:
        with self._lock:
            return RuntimeConfig(
                ollama_base_url=self._config.ollama_base_url,
                grammar_model=self._config.grammar_model,
                general_model=self._config.general_model,
            )

    def update_runtime_config(
        self,
        *,
        ollama_base_url: str | None = None,
        grammar_model: str | None = None,
        general_model: str | None = None,
    ) -> RuntimeConfig:
        with self._lock:
            updated = RuntimeConfig(
                ollama_base_url=(ollama_base_url or self._config.ollama_base_url).strip().rstrip("/"),
                grammar_model=(grammar_model or self._config.grammar_model).strip(),
                general_model=(general_model or self._config.general_model).strip(),
            )
            self._write(
                {
                    "ollama_base_url": updated.ollama_base_url,
                    "grammar_model": updated.grammar_model,
                    "general_model": updated.general_model,
                }
            )
            self._config = updated
            return RuntimeConfig(
                ollama_base_url=updated.ollama_base_url,
                grammar_model=updated.grammar_model,
                general_model=updated.general_model,
            )

    def get_model_config(self, key: str) -> ModelConfig:
        config = self.get_runtime_config()
        
        if key == "grammar" or key == "analysis": # Use grammar model for analysis
            model_name = config.grammar_model
            temperature = 0.1
            max_tokens = 512
            top_p = 0.95
        else: # general
            model_name = config.general_model
            temperature = 0.5
            max_tokens = 768
            top_p = 0.9

        endpoint = f"{config.ollama_base_url}/api/chat"
        
        return ModelConfig(
            name=model_name,
            endpoint=endpoint,
            api_type="ollama",
            temperature=temperature,
            max_tokens=max_tokens,
            top_p=top_p,
        )

    async def list_available_models(self) -> list[str]:
        config = self.get_runtime_config()
        url = f"{config.ollama_base_url}/api/tags"
        async with httpx.AsyncClient(timeout=settings.request_timeout_seconds) as client:
            response = await client.get(url)
            response.raise_for_status()
            payload = response.json()
        models = payload.get("models") or []
        return [model.get("name") or model.get("model") for model in models if model.get("name") or model.get("model")]
