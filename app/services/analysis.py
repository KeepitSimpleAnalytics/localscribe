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
        "description": "Report specific parts of the text that are unclear, wordy, passive, or difficult to read. ALWAYS call this tool if you find ANY of these issues, even minor ones.",
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
                                "description": "The EXACT substring from the original text that has the issue. It MUST be an exact match to a segment of the user's input text."
                            },
                            "issue_type": {
                                "type": "string",
                                "enum": ["complexity", "passive_voice", "wordiness", "jargon", "tone"]
                            },
                            "suggestion": {
                                "type": "string",
                                "description": "A concise improvement or rewrite for the quoted_text. Do NOT rewrite the entire sentence, only the problematic segment."
                            },
                            "confidence": {
                                "type": "number",
                                "description": "Confidence score between 0.0 and 1.0 (e.g., 0.9 for high confidence)."
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
                    "You are a highly critical and precise semantic editor. Your task is to analyze the provided text ONLY for clarity and readability issues. "
                    "Specifically identify and report: "
                    "1. **Passive Voice**: Highlight sentences where the subject is acted upon. "
                    "2. **Wordiness/Redundancy**: Identify phrases that can be shortened without losing meaning. "
                    "3. **Complexity**: Pinpoint overly long, convoluted sentences or phrases that hinder understanding. "
                    "4. **Jargon**: Flag technical terms that might be unclear to a general audience. "
                    "5. **Tone Inconsistency**: Note parts where the tone deviates from a clear, direct style (e.g., overly formal, hedging). "
                    "Use the 'report_clarity_issues' tool to report your findings. "
                    "For each issue, provide the EXACT problematic substring from the user's input and a concise, actionable suggestion for improvement. "
                    "If you find ANY issues, you MUST call the 'report_clarity_issues' tool. If no issues are found, do not call the tool."
                )
            },
            # Few-shot example
            {
                "role": "user", 
                "content": "It is anticipated that a decision will be made by us at some point in time. The situation was being evaluated."
            },
            {
                "role": "assistant",
                "tool_calls": [
                    {
                        "function": {
                            "name": "report_clarity_issues",
                            "arguments": {
                                "issues": [
                                    {
                                        "quoted_text": "It is anticipated that a decision will be made by us",
                                        "issue_type": "passive_voice",
                                        "suggestion": "We anticipate a decision",
                                        "confidence": 0.9
                                    },
                                    {
                                        "quoted_text": "at some point in time",
                                        "issue_type": "wordiness",
                                        "suggestion": "eventually",
                                        "confidence": 0.95
                                    },
                                    {
                                        "quoted_text": "The situation was being evaluated",
                                        "issue_type": "passive_voice",
                                        "suggestion": "We were evaluating the situation",
                                        "confidence": 0.85
                                    }
                                ]
                            }
                        }
                    }
                ]
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
            logger.info(f"LLM returned {len(response.tool_calls)} tool calls.")
            for tool_call in response.tool_calls:
                if tool_call.name == "report_clarity_issues":
                    raw_issues = tool_call.arguments.get("issues", [])
                    
                    for item in raw_issues:
                        quoted = item.get("quoted_text", "")
                        issue_type = item.get("issue_type", "complexity")
                        suggestion = item.get("suggestion", "")
                        confidence = float(item.get("confidence", 1.0))
                        
                        if not quoted or not suggestion:
                            logger.warning(f"Malformed issue from LLM: quoted_text='{quoted}', suggestion='{suggestion}'")
                            continue

                        # Find exact position in text
                        offset = text.find(quoted)
                        
                        if offset != -1:
                            issues.append(AnalysisIssue(
                                offset=offset,
                                length=len(quoted),
                                quoted_text=quoted,
                                issue_type=issue_type,
                                suggestion=suggestion,
                                confidence=confidence
                            ))
                        else:
                            logger.warning(f"LLM hallucinated quote not found in text: '{quoted}' (Issue Type: {issue_type}, Suggestion: '{suggestion}')")
                else:
                    logger.warning(f"LLM called unknown tool: {tool_call.name}")
        else:
            logger.info("LLM did not return any tool calls for analysis.")

        logger.info(
            "Analysis completed | model=%s latency_ms=%.2f issues=%d",
            model_config.name,
            latency_ms,
            len(issues),
        )

        return AnalysisResponse(issues=issues, latency_ms=latency_ms)
