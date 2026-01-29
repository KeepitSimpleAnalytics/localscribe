# LocalScribe

A local AI writing assistant that provides real-time grammar checking, semantic clarity analysis, and text rewriting—all processed on your machine for complete privacy.

## Features

- **Real-Time Grammar Checking** — LanguageTool-powered detection with red underlines on errors
- **Semantic Clarity Analysis** — LLM-based detection of passive voice, wordiness, complexity, and jargon (yellow/blue/purple highlights)
- **Multi-Mode Editing** — Proofread, rewrite, tone adjustment, and technical editing modes
- **System Tray App** — Runs silently in the background with global hotkey support
- **Local Processing** — All text stays on your machine; no cloud services required

## Quick Start (Windows)

**Prerequisites:**
- Python 3.11+
- Java Runtime Environment (JRE 8+)
- .NET 8 SDK
- Ollama with models installed

```powershell
# 1. Install Python dependencies
pip install -r requirements.txt

# 2. Pull Ollama models
ollama pull llama3:instruct
ollama pull qwen2:7b-instruct

# 3. Start backend
uvicorn app.main:app --host 0.0.0.0 --port 8000

# 4. Build and run client
dotnet build client/GramCloneClient.sln
dotnet run --project client/GramCloneClient/GramCloneClient.csproj
```

Look for the tray icon near the clock. Press `Ctrl+Alt+G` with text selected to open the editor.

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    Windows Tray Client                       │
│                      (C# WPF / .NET 8)                       │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────────────┐  │
│  │ Tray Icon   │  │ Overlay     │  │ Editor Window       │  │
│  │ & Settings  │  │ (Underlines)│  │ (Proofread/Rewrite) │  │
│  └─────────────┘  └─────────────┘  └─────────────────────┘  │
└────────────────────────────┬────────────────────────────────┘
                             │ HTTP
                             ▼
┌─────────────────────────────────────────────────────────────┐
│                    Backend (FastAPI)                         │
│                       Port 8000                              │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────────────┐  │
│  │ Grammar     │  │ Analysis    │  │ Editing             │  │
│  │ (LangTool)  │  │ (LLM)       │  │ (LLM)               │  │
│  └─────────────┘  └─────────────┘  └─────────────────────┘  │
└────────────────────────────┬────────────────────────────────┘
                             │
              ┌──────────────┴──────────────┐
              ▼                             ▼
     ┌─────────────────┐          ┌─────────────────┐
     │  LanguageTool   │          │     Ollama      │
     │  (Java)         │          │  (Local LLMs)   │
     └─────────────────┘          └─────────────────┘
```

## Editing Modes

| Mode | Description |
|------|-------------|
| `proofread` | Minimal grammar/spelling fixes, preserves tone and meaning |
| `rewrite` | Clarity-first rewrite with mild structural edits |
| `tone` | Tone transformation (professional, concise, friendly) |
| `technical` | Cybersecurity-focused rewrite maintaining technical accuracy |

## API Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/health` | Health check with LanguageTool status |
| `POST` | `/v1/text/check` | Grammar checking |
| `POST` | `/v1/text/analyze` | Semantic clarity analysis |
| `POST` | `/v1/text/edit` | Multi-mode text editing |
| `GET` | `/runtime/config` | Read runtime configuration |
| `POST` | `/runtime/config` | Update Ollama URL and models |
| `GET` | `/runtime/models` | List available Ollama models |

**Example:**
```bash
curl -X POST http://localhost:8000/v1/text/edit \
  -H "Content-Type: application/json" \
  -d '{"text":"Please fix this sentence.","mode":"proofread"}'
```

## Configuration

### Runtime Config (`config/runtime_config.json`)

Generated on first run. Managed via Settings dialog or API.

```json
{
  "ollama_base_url": "http://localhost:11434",
  "grammar_model": "llama3:instruct",
  "general_model": "qwen2:7b-instruct"
}
```

### Environment Variables (`.env`)

```bash
ENVIRONMENT=development
LOG_LEVEL=INFO
LOG_CONTENT_ENABLED=false
REQUEST_TIMEOUT_SECONDS=600.0
```

### Client Settings

Stored at `%AppData%\GramClone\settings.json`:
- Backend URL
- Global hotkey (default: `Ctrl+Alt+G`)
- Default mode and tone
- Overlay customization

## Visual Feedback

The overlay draws directly on screen:
- **Red underlines** — Grammar errors (LanguageTool)
- **Yellow highlights** — Wordiness/redundancy
- **Blue highlights** — Passive voice
- **Purple highlights** — Complexity issues

Hover over highlighted text for suggestions.

## Development

### Backend
```powershell
python -m venv .venv
.venv\Scripts\activate
pip install -r requirements.txt
uvicorn app.main:app --reload
```

### Client
```powershell
cd client/GramCloneClient
dotnet build
dotnet run
```

### Testing
```powershell
pytest                          # Backend tests
python scripts/sanity_check.py  # Sample API calls
```

## Deployment

**Backend:**
```powershell
uvicorn app.main:app --host 0.0.0.0 --port 8000
```
Use Task Scheduler, NSSM, or systemd to run as a service.

**Client:**
```powershell
dotnet publish -c Release -r win10-x64
```
Add shortcut to `%AppData%\Microsoft\Windows\Start Menu\Programs\Startup` for auto-start.

## Privacy

- All processing happens locally
- No cloud endpoints unless explicitly configured
- Logs contain timestamps and text length only (no content by default)
- Clipboard contents restored after paste operations

## Tech Stack

**Backend:** Python 3.11+, FastAPI, LanguageTool, Ollama
**Client:** C# / .NET 8, WPF, Windows UI Automation
**Models:** llama3:instruct (proofread), qwen2:7b-instruct (rewrite)
