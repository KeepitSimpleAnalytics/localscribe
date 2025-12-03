"""Service for checking grammar using LanguageTool."""

from __future__ import annotations

import logging
import os
import language_tool_python
from app.schemas import CheckResponse, GrammarError, LanguageToolConfig

logger = logging.getLogger(__name__)

class GrammarCheckService:
    def __init__(self) -> None:
        logger.info(f"Python process PATH: {os.environ.get('PATH')}")
        logger.info(f"Python process JAVA_HOME: {os.environ.get('JAVA_HOME')}")
        try:
            self._tool = language_tool_python.LanguageTool("en-US")
            self._enabled = True
        except Exception as e:
            logger.warning(f"Failed to initialize LanguageTool (Java likely missing): {e}")
            self._tool = None
            self._enabled = False

    def check(
        self, text: str, config: LanguageToolConfig | None = None
    ) -> CheckResponse:
        if not self._enabled or not self._tool:
            return CheckResponse(matches=[])

        try:
            matches = self._tool.check(text)
        except Exception as e:
            logger.error(f"Error during grammar check: {e}")
            return CheckResponse(matches=[])

        # Build set of disabled categories for filtering
        disabled_categories: set[str] = set()
        if config and config.disabled_categories:
            disabled_categories = {cat.upper() for cat in config.disabled_categories}

        errors = []
        for match in matches:
            # Skip matches in disabled categories
            if match.category.upper() in disabled_categories:
                continue
            # Build context from the original text (show ~40 chars before/after the error)
            context_start = max(0, match.offset - 40)
            context_end = min(len(text), match.offset + match.error_length + 40)
            context = text[context_start:context_end]
            offset_in_context = match.offset - context_start

            errors.append(
                GrammarError(
                    message=match.message,
                    offset=match.offset,
                    length=match.error_length,
                    replacements=match.replacements[:5],  # Limit to top 5 suggestions
                    rule_id=match.rule_id,
                    category=match.category,
                    context=context,
                    sentence=context,  # Use context as sentence for now
                    offset_in_context=offset_in_context,
                )
            )

        return CheckResponse(matches=errors)
