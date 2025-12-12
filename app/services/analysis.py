"""Service for semantic analysis using LLM Tool Use."""

from __future__ import annotations

import time
import logging
from typing import Any

from app.config_manager import ConfigManager
from app.models.client import ModelClient
from app.schemas import AnalysisResponse, AnalysisIssue

logger = logging.getLogger(__name__)

# --- Tool Definition ---

CLARITY_TOOL = {
    "type": "function",
    "function": {
        "name": "report_clarity_issues",
        "description": "Report specific parts of the text that are unclear, wordy, passive, or difficult to read.",
        "parameters": {
            "type": "object",
            "properties": {
                "issues": {
                    "type": "array",
                    "items": {
                        "type": "object",
                        "properties": {
                            "quoted_text": {
                                "type": "string",
                                "description": "The EXACT substring from the original text that has the issue."
                            },
                            "issue_type": {
                                "type": "string",
                                "enum": ["complexity", "passive_voice", "wordiness", "jargon", "tone"]
                            },
                            "suggestion": {
                                "type": "string",
                                "description": "A concise improvement or rewrite."
                            },
                            "confidence": {
                                "type": "number",
                                "description": "Confidence score between 0.0 and 1.0"
                            }
                        },
                        "required": ["quoted_text", "issue_type", "suggestion"]
                    }
                }
            },
            "required": ["issues"]
        }
    }
}


class AnalysisService:
    """Service for semantic text analysis."""

    def __init__(self, *, config_manager: ConfigManager, timeout: float) -> None:
        self._config_manager = config_manager
        self._timeout = timeout

    async def analyze(self, text: str) -> AnalysisResponse:
        """Analyze text for semantic clarity issues."""
        model_config = self._config_manager.get_model_config("analysis")
        client = ModelClient(model_config, timeout=self._timeout)

        messages = [
            {
                "role": "system", 
                "content": (
                    "You are a strict semantic editor. Analyze the text for readability and clarity. "
                    "Identify issues like passive voice, unnecessary wordiness, jargon, or complex sentence structures. "
                    "Use the 'report_clarity_issues' tool to report your findings. "
                    "Do not report trivial issues. Focus on things that hinder reading flow."
                )
            },
            {"role": "user", "content": text},
        ]

        start = time.perf_counter()
        
        try:
            # Call the model with the tool definition
            response = await client.generate(messages, tools=[CLARITY_TOOL])
        except Exception as e:
            logger.error(f"Analysis model call failed: {e}")
            return AnalysisResponse(issues=[], latency_ms=0.0)

        latency_ms = (time.perf_counter() - start) * 1000
        
        issues: list[AnalysisIssue] = []

        if response.tool_calls:
            for tool_call in response.tool_calls:
                if tool_call.name == "report_clarity_issues":
                    raw_issues = tool_call.arguments.get("issues", [])
                    
                    for item in raw_issues:
                        quoted = item.get("quoted_text", "")
                        if not quoted:
                            continue

                        # Find exact position in text
                        # TODO: Handle multiple occurrences better (currently finds first)
                        offset = text.find(quoted)
                        
                        if offset != -1:
                            issues.append(AnalysisIssue(
                                offset=offset,
                                length=len(quoted),
                                quoted_text=quoted,
                                issue_type=item.get("issue_type", "complexity"),
                                suggestion=item.get("suggestion", ""),
                                confidence=float(item.get("confidence", 1.0))
                            ))
                        else:
                            logger.warning(f"LLM hallucinated quote not found in text: '{quoted}'")

        logger.info(
            "Analysis completed | model=%s latency_ms=%.2f issues=%d",
            model_config.name,
            latency_ms,
            len(issues),
        )

        return AnalysisResponse(issues=issues, latency_ms=latency_ms)
