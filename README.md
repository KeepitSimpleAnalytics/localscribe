# Gram Clone

A local AI writing assistant that mimics the functionality of premium grammar checkers using open-source tools.

## Quick Start (Windows)

1.  **Prerequisites:** 
    *   Install **Python 3.11+** and ensure it's in your PATH.
    *   Install **Java Runtime (JRE 8+)** (required for grammar checking).
    *   Run `pip install -r requirements.txt` in this folder.
2.  **Run:**
    *   Open `client/GramCloneClient/bin/Debug/net8.0-windows10.0.19041.0/GramCloneClient.exe`.
    *   The client will automatically start the Python backend in the background.
    *   Look for the "G" icon in your system tray.

## Features

*   **Real-Time Checking:** Monitors text focus and provides instant grammar feedback via a floating "bubble".
*   **Local Processing:** All text stays on your machine.
*   **Rewriting:** Use the hotkey (default `Ctrl+Alt+G`) to open the full editor for rewriting (Professional, Casual, Academic modes).
*   **Ollama Integration:** Uses local LLMs for advanced style improvements.

## Architecture

### Backend (Python/FastAPI)
*   **Grammar:** `language-tool-python` (Java wrapper for LanguageTool).
*   **Rewriting:** Ollama (via `langchain` or direct API).
*   **API:** Exposes endpoints at `http://localhost:8000`.

### Client (C# WPF)
*   **UI Automation:** Uses Microsoft UI Automation to detect focused text fields.
*   **Tray App:** Runs silently in the background.
*   **Backend Manager:** Automatically launches and manages the Python backend process.

## Development

### Manual Startup (Optional)
If you prefer to run components separately:
1.  Start Backend: `python -m uvicorn app.main:app --reload`
2.  Start Client: Open `GramCloneClient.sln` in Visual Studio or use `dotnet run`.


## Editing Modes

| Mode      | Behavior                                                                 |
|-----------|---------------------------------------------------------------------------|
| proofread | Minimal grammar/spelling/punctuation fixes, preserves tone/meaning.      |
| rewrite   | Clarity-first rewrite, mild structural edits allowed.                    |
| tone      | Tone transformations (`professional`, `concise`, `friendly`).            |
| technical | Cybersecurity-focused rewrite keeping technical accuracy and precision.  |

Prompts live in `app/prompts.py` for easy iteration.

## Backend Setup

Requirements: Python 3.11+, local model runtime (Ollama or vLLM).

```powershell
cd X:\docker_images\gram_clone
python -m venv .venv
.venv\Scripts\activate
pip install -r requirements.txt
uvicorn app.main:app --host 0.0.0.0 --port 8000
```

### Model server (Ollama example)

```powershell
ollama pull llama3:instruct             # proofread model
ollama pull qwen2:7b-instruct           # rewrite/tone/technical model
ollama serve
```

### Configuration

- At startup, the backend creates `config/runtime_config.json` (ignored by git) with defaults:
  ```json
  {
    "ollama_base_url": "http://10.8.14.169:11434",
    "grammar_model": "llama3:instruct",
    "general_model": "qwen2:7b-instruct"
  }
  ```
  - The Windows settings dialog reads/writes this file through the backend’s `/runtime/config` endpoint. Changing models or the Ollama base URL never requires editing environment variables.
- Optional environment variables (via `.env`) control logging: `LOG_LEVEL`, `LOG_CONTENT_ENABLED`, etc.

### API

- `GET /health` → `{ "status": "ok", "environment": "development" }`
- `POST /v1/text/edit` – main editing API.
- `GET /runtime/config` / `POST /runtime/config` – read and update Ollama base URL + model selection.
- `GET /runtime/models` – proxies to Ollama’s `/api/tags` and returns available model names.

Test editing quickly:

```powershell
curl -X POST http://localhost:8000/v1/text/edit ^
  -H "Content-Type: application/json" ^
  -d "{\"text\":\"Please fix this sentence.\",\"mode\":\"proofread\"}"
```

## Windows Tray Client

Requirements: Windows 10/11, .NET 8 SDK (or Visual Studio 2022 with the .NET Desktop workload).

```powershell
dotnet build client/GramCloneClient.sln
dotnet run --project client/GramCloneClient/GramCloneClient.csproj
```

### Features

- Tray icon with “Settings…” and “Exit”.
- Global hotkey `Ctrl+Alt+G` (configurable in Settings; stored under `%AppData%\GramClone\settings.json`).
- Popup editor:
  - Shows captured “Original” text and the “Improved” result.
  - Mode dropdown (proofread, rewrite, tone, technical).
  - Tone dropdown when mode=tone.
  - Run button calls `/v1/text/edit`.
  - Apply button copies the improved text and pastes it back into the source application.
- Settings dialog:
  - Backend URL (FastAPI base).
  - Hotkey editor (`Ctrl+Alt+G`, `Win+Shift+H`, etc.).
  - Default mode and tone.
  - Ollama base URL (set to wherever Ollama listens). Use **Reload Models** to push the URL to the backend and fetch the model list from that host.
  - Proofread model + Rewrite/Tone/Technical model dropdowns (editable) populated via `GET /runtime/models` once the Ollama URL is reachable.
  - Saving updates both the local client settings and the backend runtime configuration.

### Usage Flow

1. Start the backend (`uvicorn app.main:app ...`) and ensure Ollama is running.
2. Launch the tray client (icon appears near the clock; look under the `^` caret if hidden).
3. Configure backend URL / hotkey / models via Settings (the dialog queries the backend to list available Ollama models).
4. Highlight text in any Windows application and press the hotkey (defaults to `Ctrl+Alt+G`).
5. Use the popup editor to select mode/tone, then click **Run**.
6. Review the improved text and click **Apply** to paste it back into the original application.

## Logging & Privacy

- Backend logs include timestamp, mode, model, latency, and text length only. Set `LOG_CONTENT_ENABLED=true` (optional) to include a short preview for debugging.
- Tray client clipboard swaps are local and the previous clipboard contents are restored after Apply.
- No cloud endpoints are used unless you explicitly change the backend or Ollama URLs.

## Testing & Validation

```powershell
pytest                           # backend tests (includes config/model endpoints)
python scripts/sanity_check.py   # sample backend calls
# Manual client test:
#   1. Run backend + Ollama.
#   2. Run tray app, set backend URL/models in Settings.
#   3. Highlight text, press the hotkey, Run proofread, Apply.
```

## Deployment Notes

- Backend: run `uvicorn app.main:app --host 0.0.0.0 --port 8000`. Use NSSM/Task Scheduler/Systemd as desired.
- Runtime config lives in `config/runtime_config.json`; the tray app manages it, but you can edit manually if needed.
- Tray client: publish with `dotnet publish -c Release -r win10-x64` for a self-contained executable. Place a shortcut to the `.exe` in `%AppData%\Microsoft\Windows\Start Menu\Programs\Startup` for auto-start.
