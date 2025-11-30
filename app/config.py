"""Application configuration."""

from __future__ import annotations

from dataclasses import dataclass
from typing import Literal

from pydantic import Field
from pydantic_settings import BaseSettings, SettingsConfigDict

ModelAPIType = Literal["ollama", "openai"]


@dataclass(slots=True)
class ModelConfig:
    """Configuration for a single model target."""

    name: str
    endpoint: str
    api_type: ModelAPIType
    temperature: float
    max_tokens: int
    top_p: float


class Settings(BaseSettings):
    """Pydantic settings wrapper."""

    environment: str = Field(default="development", alias="ENVIRONMENT")
    request_timeout_seconds: float = Field(default=120.0, alias="REQUEST_TIMEOUT_SECONDS")
    log_level: str = Field(default="INFO", alias="LOG_LEVEL")
    log_content_enabled: bool = Field(default=False, alias="LOG_CONTENT_ENABLED")
    config_path: str = Field(default="config/runtime_config.json", alias="CONFIG_PATH")

    model_config = SettingsConfigDict(env_file=".env", env_file_encoding="utf-8", extra="ignore")


settings = Settings()
