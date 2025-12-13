"""FastAPI entrypoint for the Gram Clone backend."""

from __future__ import annotations

from functools import lru_cache
from pathlib import Path

import httpx
from fastapi import Depends, FastAPI, HTTPException, status

from app import __version__, schemas
from app.config import settings
from app.config_manager import ConfigManager
from app.logging_utils import configure_logging, get_logger
from app.models.client import ModelClientError
from app.services.editing import EditResult, EditingService
from app.services.grammar_check import GrammarCheckService
from app.services.analysis import AnalysisService

configure_logging(level=settings.log_level)
logger = get_logger(__name__)

app = FastAPI(title="LocalScribe Backend")
config_manager = ConfigManager(Path(settings.config_path))
grammar_service = GrammarCheckService()
analysis_service = AnalysisService(config_manager=config_manager, timeout=settings.request_timeout_seconds)


@lru_cache(maxsize=1)
def get_editing_service() -> EditingService:
    """Instantiate the editing service."""
    return EditingService(config_manager=config_manager, timeout=settings.request_timeout_seconds)


@app.get("/health", response_model=schemas.HealthResponse)
async def health() -> schemas.HealthResponse:
    """Simple health-check endpoint."""
    return schemas.HealthResponse(
        status="ok",
        environment=settings.environment,
        version=__version__,
    )


@app.get("/runtime/config", response_model=schemas.RuntimeConfigResponse)
async def get_runtime_config() -> schemas.RuntimeConfigResponse:
    """Return current runtime configuration."""
    runtime = config_manager.get_runtime_config()
    return schemas.RuntimeConfigResponse.from_runtime_config(runtime)


@app.post("/runtime/config", response_model=schemas.RuntimeConfigResponse)
async def update_runtime_config(payload: schemas.RuntimeConfigUpdate) -> schemas.RuntimeConfigResponse:
    """Update runtime configuration."""
    runtime = config_manager.update_runtime_config(
        ollama_base_url=str(payload.ollama_base_url) if payload.ollama_base_url else None,
        grammar_model=payload.grammar_model,
        general_model=payload.general_model,
    )
    return schemas.RuntimeConfigResponse.from_runtime_config(runtime)


@app.get("/runtime/models", response_model=schemas.ModelListResponse)
async def list_available_models() -> schemas.ModelListResponse:
    """Return available Ollama models."""
    try:
        models = await config_manager.list_available_models()
    except httpx.HTTPError as exc:  # pragma: no cover - network issue
        raise HTTPException(
            status_code=status.HTTP_502_BAD_GATEWAY,
            detail=f"Failed to query Ollama: {exc}",
        ) from exc

    return schemas.ModelListResponse(models=models)


@app.post("/runtime/warmup")
async def warmup_model() -> dict:
    """Send a minimal request to Ollama to load the model into memory."""
    config = config_manager.get_runtime_config()
    url = f"{config.ollama_base_url}/api/generate"

    # Minimal request to trigger model loading
    payload = {
        "model": config.grammar_model,
        "prompt": "",
        "stream": False,
        "options": {"num_predict": 1}
    }

    try:
        async with httpx.AsyncClient(timeout=600.0) as client:
            response = await client.post(url, json=payload)
            response.raise_for_status()
        logger.info(f"Model warmup complete: {config.grammar_model}")
        return {"status": "ok", "model": config.grammar_model}
    except httpx.HTTPError as exc:
        logger.warning(f"Model warmup failed: {exc}")
        raise HTTPException(
            status_code=status.HTTP_502_BAD_GATEWAY,
            detail=f"Warmup failed: {exc}",
        ) from exc


@app.post("/v1/text/check", response_model=schemas.CheckResponse)
async def check_text(payload: schemas.CheckRequest) -> schemas.CheckResponse:
    """Check text for grammar errors using LanguageTool."""
    return grammar_service.check(payload.text, payload.language_tool_config)


@app.post("/v1/text/analyze", response_model=schemas.AnalysisResponse)
async def analyze_text(payload: schemas.AnalysisRequest) -> schemas.AnalysisResponse:
    """Analyze text for semantic clarity using LLM."""
    return await analysis_service.analyze(payload.text)


@app.post("/v1/text/edit", response_model=schemas.EditResponse)
async def edit_text(
    payload: schemas.EditRequest,
    service: EditingService = Depends(get_editing_service),
) -> schemas.EditResponse:
    """Main editing endpoint."""
    try:
        result: EditResult = await service.edit(
            text=payload.text,
            mode=payload.mode,
            tone=payload.tone,
            extra_instructions=payload.extra_instructions,
        )
    except ModelClientError as exc:  # pragma: no cover
        logger.exception("Model call failed")
        raise HTTPException(
            status_code=status.HTTP_502_BAD_GATEWAY,
            detail=f"Model request failed: {exc}",
        ) from exc

    text_preview = ""
    if settings.log_content_enabled:  # pragma: no cover
        text_preview = f" preview={payload.text[:200]!r}"

    logger.info(
        "Edit request processed | mode=%s model=%s latency_ms=%.2f text_len=%d%s",
        result.mode,
        result.model_config.name,
        result.latency_ms,
        len(payload.text),
        text_preview,
    )

    return schemas.EditResponse(
        mode=result.mode,
        model=schemas.ModelInfo(
            name=result.model_config.name,
            endpoint=result.model_config.endpoint,
            api_type=result.model_config.api_type,
        ),
        output_text=result.output_text,
        latency_ms=result.latency_ms,
    )
