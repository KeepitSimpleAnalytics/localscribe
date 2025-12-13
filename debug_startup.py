import sys
import os
from pathlib import Path

# Add project root to path
sys.path.append(os.getcwd())

print("Attempting to import app.main...")
try:
    from app.main import app, grammar_service, analysis_service
    print("SUCCESS: app.main imported.")
    print(f"Grammar Service: {grammar_service}")
    print(f"Analysis Service: {analysis_service}")
except Exception as e:
    print(f"FAILURE: {e}")
    import traceback
    traceback.print_exc()
