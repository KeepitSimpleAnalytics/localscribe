# "On-the-Fly" Grammar Checking Plan

This document outlines the plan to transform Gram Clone from an on-demand editor into a real-time grammar checker using a hybrid "Tier 1 / Tier 2" architecture.

## Architecture Overview

The system uses a two-tiered approach to balance speed and capability:
1.  **Tier 1 (Fast & Local):** `language-tool-python` (Java-based LanguageTool) runs on the backend to provide instant feedback on mechanics (spelling, punctuation, basic grammar).
2.  **Tier 2 (Smart & Heavy):** Ollama (LLM) runs on the backend for complex rewriting, tone adjustments, and structural improvements.

## Progress So Far

### Backend (Completed)
- [x] Added `language-tool-python` to `requirements.txt`.
- [x] Created `app/schemas.py` models (`CheckRequest`, `GrammarError`, `CheckResponse`) for structured error reporting.
- [x] Implemented `GrammarCheckService` in `app/services/grammar_check.py`.
    - Includes graceful degradation if Java is missing (logs warning, returns empty list).
- [x] Registered `GrammarCheckService` in `app/main.py`.
- [x] Created new endpoint: `POST /v1/text/check`.

### Current Status
- The backend is running.
- The `/v1/text/check` endpoint is active.
- **Issue:** The backend currently cannot find the Java runtime environment, so it is operating in "degraded mode" (grammar check returns no errors).
- **Fix Required:** The user must configure `JAVA_HOME` and `PATH` environment variables and restart the terminal.

## Next Steps (To Be Implemented)

### Phase 1: Client-Side "Bubble" UI
To achieve the "Grammarly-like" experience, we need to implement the client-side observer.

1.  **Input Monitoring (The "Observer"):**
    - Implement a background service in the C# client using **Microsoft UI Automation (UIA)**.
    - Detect `AutomationFocusChangedEvent` to know when the user is in a text field (Notepad, Browser, etc.).
    - Use `TextPattern` to read the text around the caret.

2.  **Debouncing Strategy (The "Thinker"):**
    - Do not send every keystroke.
    - Wait for a pause in typing (e.g., 1-2 seconds) or a sentence terminator (`.`, `?`, `!`).
    - Extract the current sentence/paragraph.

3.  **Backend Communication:**
    - Send the extracted text to the new `POST /v1/text/check` endpoint.
    - Receive the list of `GrammarError` objects.

4.  **Feedback UI (The "Notifier"):**
    - **Initial Prototype:** Use a transparent "Floating Bubble" window that follows the text cursor.
    - **State:**
        - **Green:** No errors found.
        - **Red:** Errors found (display count).
    - **Interaction:**
        - Clicking the bubble opens a small tooltip showing the specific errors and suggestions (from `language-tool-python`).
        - Clicking a "Fix" button in the tooltip could optionally open the full Main Editor window for deeper rewriting.

### Phase 2: Optimization
- **Streaming:** Consider moving to WebSockets if HTTP latency becomes noticeable for real-time checking.
- **Local C# Library:** If the round-trip to Python is too slow, investigate embedding a C# port of LanguageTool directly into the client, removing the network hop entirely.

## Instructions for Resuming
1.  **Fix Java:** Ensure `java -version` works in the terminal.
2.  **Restart Backend:** Run the `uvicorn` start command again.
3.  **Verify API:** `curl -X POST http://localhost:8000/v1/text/check -d '{"text": "I has a apple"}'` should return a JSON with errors.
4.  **Begin Client Dev:** Start coding the UIA observer in `GramCloneClient`.
