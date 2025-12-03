"""Pydantic models for API requests and responses."""

from __future__ import annotations

from typing import Literal

from pydantic import BaseModel, Field, HttpUrl, field_validator, model_validator

from app.config_manager import RuntimeConfig
from app.types import Mode, ToneStyle


class HealthResponse(BaseModel):
    """Response model for /health."""

    status: str
    environment: str


class ModelInfo(BaseModel):
    """Metadata returned to the client describing the target model."""

    name: str
    endpoint: HttpUrl
    api_type: Literal["ollama", "openai"]


class EditRequest(BaseModel):
    """Request payload for /v1/text/edit."""

    text: str = Field(..., min_length=1, description="Original text to process.")
    mode: Mode = Field(..., description="Editing mode to execute.")
    tone: ToneStyle | None = Field(
        default=None,
        description="Tone preset required when mode=tone.",
    )
    extra_instructions: str | None = Field(
        default=None,
        description="Optional user-provided clarifications.",
    )

    @field_validator("text")
    @classmethod
    def strip_text(cls, value: str) -> str:
        """Ensure text contains non-whitespace characters."""
        if not value.strip():
            raise ValueError("Text must contain non-whitespace characters.")
        return value

    @model_validator(mode="after")
    def validate_mode_specific_fields(self) -> "EditRequest":
        """Ensure tone is set when needed."""
        if self.mode == "tone" and not self.tone:
            msg = "Tone must be supplied when mode is 'tone'."
            raise ValueError(msg)
        return self


class EditResponse(BaseModel):
    """Response payload for editing calls."""

    mode: Mode
    model: ModelInfo
    output_text: str = Field(..., description="Edited text produced by the LLM.")
    latency_ms: float


class RuntimeConfigResponse(BaseModel):
    """REPresents backend runtime configuration."""

    ollama_base_url: HttpUrl
    grammar_model: str
    general_model: str

    @classmethod
    def from_runtime_config(cls, config: RuntimeConfig) -> "RuntimeConfigResponse":
        return cls(
            ollama_base_url=config.ollama_base_url,
            grammar_model=config.grammar_model,
            general_model=config.general_model,
        )


class RuntimeConfigUpdate(BaseModel):
    """Payload to update runtime configuration."""

    ollama_base_url: HttpUrl | None = None
    grammar_model: str | None = None
    general_model: str | None = None


class LanguageToolConfig(BaseModel):
    """Configuration for LanguageTool rule filtering."""

    disabled_categories: list[str] = Field(
        default_factory=list,
        description="List of category IDs to disable (e.g., 'STYLE', 'TYPOGRAPHY')",
    )


class CheckRequest(BaseModel):
    """Request payload for /v1/text/check."""

    text: str = Field(..., min_length=1, description="Text to check for grammar errors.")
    language_tool_config: LanguageToolConfig | None = Field(
        default=None,
        description="Optional LanguageTool rule configuration",
    )


class GrammarError(BaseModel):
    """Represents a single grammar error found by LanguageTool."""

    message: str
    offset: int
    length: int
    replacements: list[str]
    rule_id: str
    category: str
    context: str = ""              # Sentence/context containing the error
    sentence: str = ""             # Full sentence with the error
    offset_in_context: int = 0     # Position of error within context


class CheckResponse(BaseModel):
    """Response payload for grammar checking."""

    matches: list[GrammarError]


class ModelListResponse(BaseModel):
    """List of available models reported by Ollama."""

    models: list[str]
