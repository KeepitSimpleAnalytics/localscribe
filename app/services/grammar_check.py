"""Service for checking grammar using LanguageTool."""

import logging
import os
import language_tool_python
from app.schemas import CheckResponse, GrammarError

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

    def check(self, text: str) -> CheckResponse:
        if not self._enabled or not self._tool:
            return CheckResponse(matches=[])

        try:
            matches = self._tool.check(text)
        except Exception as e:
            logger.error(f"Error during grammar check: {e}")
            return CheckResponse(matches=[])
        
        errors = []
        for match in matches:
            errors.append(
                GrammarError(
                    message=match.message,
                    offset=match.offset,
                    length=match.error_length,
                    replacements=match.replacements[:5],  # Limit to top 5 suggestions
                    rule_id=match.rule_id,
                    category=match.category,
                )
            )
            
        return CheckResponse(matches=errors)
