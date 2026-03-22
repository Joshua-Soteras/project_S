# ============================================
# NLP Service Dockerfile
# ============================================
#
# Uses uv for fast, reproducible dependency installation.
# uv reads pyproject.toml + uv.lock — no requirements.txt needed.
#
# KEY DECISIONS:
#
# 1. CPU-only PyTorch via uv source override
#    pyproject.toml points torch to PyTorch's CPU wheel index.
#    uv respects this automatically. Result: ~700MB image vs ~3.5GB.
#
# 2. Model baked in at build time
#    The model weights (~500MB) are downloaded into the Hugging Face
#    cache during docker build. Container startup is instant at runtime.
#
# 3. --frozen flag on uv sync
#    Ensures the exact versions from uv.lock are installed.
#    Build fails if uv.lock is out of sync with pyproject.toml.
#    This guarantees reproducible builds across all environments.
#
# 4. Non-root user
#    The app runs as a non-root user (appuser) for security.
# ============================================

FROM python:3.11-slim

# ----------------------------------------
# Install uv
# ----------------------------------------
# Copies the uv binary directly from the official uv image.
# This is the recommended approach — no pip install needed.
COPY --from=ghcr.io/astral-sh/uv:latest /uv /uvx /bin/

WORKDIR /app

# ----------------------------------------
# Install system dependencies
# ----------------------------------------
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# ----------------------------------------
# Install Python dependencies via uv
# ----------------------------------------
# Copy lockfile and project metadata first (before app code).
# Docker layer caching: if pyproject.toml and uv.lock don't change,
# this layer is reused on rebuild — no reinstall needed.
COPY pyproject.toml uv.lock ./
RUN uv sync --frozen --no-install-project

# ----------------------------------------
# Copy application code
# ----------------------------------------
COPY app/ ./app/

# ----------------------------------------
# Pre-download the RoBERTa model weights
# ----------------------------------------
# Downloads ~500MB of model weights into the Hugging Face cache at
# build time so runtime startup is instant and network-independent.
RUN uv run python -c "\
from transformers import pipeline; \
pipeline( \
    'text-classification', \
    model='cardiffnlp/twitter-roberta-base-sentiment-latest', \
    return_all_scores=True \
)"

# ----------------------------------------
# Create non-root user and fix permissions
# ----------------------------------------
RUN useradd --create-home appuser \
    && chown -R appuser /app \
    && chown -R appuser /root/.cache
USER appuser

# ----------------------------------------
# Expose port and start server
# ----------------------------------------
EXPOSE 8000

CMD ["uv", "run", "uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8000"]
