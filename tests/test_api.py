"""API tests for the Gram Clone backend."""

from __future__ import annotations

import os
from pathlib import Path
from typing import Any

import pytest
from fastapi.testclient import TestClient

os.environ["CONFIG_PATH"] = str(Path("tests/tmp_runtime_config.json").absolute())

from app.config import ModelConfig
from app.config_manager import ConfigManager
from app.main import app, config_manager, get_editing_service
from app.services.editing import EditResult


class DummyEditingService:
    """Fake editing service used for endpoint tests."""

    def __init__(self) -> None:
        self.calls: list[dict[str, Any]] = []

    async def edit(self, **kwargs: Any) -> EditResult:
        """Record the call and return a canned response."""
        self.calls.append(kwargs)
        return EditResult(
            mode=kwargs["mode"],
            model_config=ModelConfig(
                name="dummy",
                endpoint="http://localhost:9999",
                api_type="ollama",
                temperature=0.1,
                max_tokens=32,
                top_p=0.9,
            ),
            output_text="edited text",
            latency_ms=12.5,
        )


@pytest.fixture(name="client")
def client_fixture() -> TestClient:
    """Configure dependency overrides and return a TestClient."""
    service = DummyEditingService()
    app.dependency_overrides[get_editing_service] = lambda: service
    client = TestClient(app)
    client.app.state._dummy_service = service  # type: ignore[attr-defined]
    yield client
    app.dependency_overrides.clear()


def test_health_endpoint() -> None:
    """Health endpoint should return ok."""
    client = TestClient(app)
    response = client.get("/health")
    assert response.status_code == 200
    data = response.json()
    assert data["status"] == "ok"


def test_edit_endpoint_proofread(client: TestClient) -> None:
    """Proofread request should call editing service and return payload."""
    response = client.post(
        "/v1/text/edit",
        json={
            "text": "This are bad text.",
            "mode": "proofread",
        },
    )
    assert response.status_code == 200
    data = response.json()
    assert data["mode"] == "proofread"
    assert data["output_text"] == "edited text"
    service: DummyEditingService = client.app.state._dummy_service  # type: ignore[attr-defined]
    assert service.calls, "Editing service should have been invoked."


def test_tone_mode_requires_tone(client: TestClient) -> None:
    """Tone mode without tone should raise validation error."""
    response = client.post(
        "/v1/text/edit",
        json={
            "text": "Please rewrite politely.",
            "mode": "tone",
        },
    )
    assert response.status_code == 422


def test_runtime_config_endpoint(client: TestClient) -> None:
    """Runtime config endpoint returns persisted values."""
    response = client.get("/runtime/config")
    assert response.status_code == 200
    data = response.json()
    assert "grammar_model" in data


def test_runtime_config_update(client: TestClient) -> None:
    """Runtime config can be updated."""
    response = client.post("/runtime/config", json={"grammar_model": "demo-model"})
    assert response.status_code == 200
    assert response.json()["grammar_model"] == "demo-model"


def test_runtime_models_endpoint(monkeypatch: pytest.MonkeyPatch) -> None:
    """Listing models should return mocked list."""

    async def fake_list(self) -> list[str]:
        return ["demo-a", "demo-b"]

    monkeypatch.setattr(ConfigManager, "list_available_models", fake_list)
    client = TestClient(app)
    response = client.get("/runtime/models")
    assert response.status_code == 200
    assert response.json()["models"] == ["demo-a", "demo-b"]
