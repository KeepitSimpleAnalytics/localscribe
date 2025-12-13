import sys
import os
import logging

# Setup logging
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger("verify_backend")

try:
    logger.info("Importing app.main...")
    from app.main import app, grammar_service, analysis_service
    
    logger.info("Successfully imported app.main")
    logger.info(f"Grammar Service: {grammar_service}")
    logger.info(f"Analysis Service: {analysis_service}")
    
    logger.info("Testing ConfigManager...")
    from app.config_manager import ConfigManager
    from pathlib import Path
    from app.config import settings
    
    cm = ConfigManager(Path(settings.config_path))
    config = cm.get_runtime_config()
    logger.info(f"Runtime Config loaded: {config}")
    
    model_config = cm.get_model_config("analysis")
    logger.info(f"Model Config (analysis): {model_config}")
    
    print("VERIFICATION SUCCESSFUL")

except Exception as e:
    logger.error("VERIFICATION FAILED")
    logger.exception(e)
    sys.exit(1)
