# ============================================
# main.py — FastAPI Entry Point
# ============================================
#
# This file defines the FastAPI application and its two endpoints:
#
#   GET  /health   — liveness check (used by Docker and load balancers)
#   POST /analyze  — run sentiment analysis on a text string
#
# The RoBERTa model is loaded ONCE at startup via the lifespan hook.
# Every subsequent request reuses the same in-memory pipeline —
# no reload cost per request.
#
# HOW THIS FITS IN THE SYSTEM:
#   ASP.NET Core API
#       → POST /analyze { "text": "..." }   ← THIS SERVICE
#           → { "label": "positive", "positive": 0.94, ... }
#       → saves SentimentResult to PostgreSQL
# ============================================

from contextlib import asynccontextmanager

from fastapi import FastAPI, HTTPException
from pydantic import BaseModel

from app.model import load_model
from app.model import analyze as run_sentiment


# ============================================
# LIFESPAN — Startup / Shutdown
# ============================================
# FastAPI's lifespan hook replaces the deprecated @app.on_event("startup").
# Code before `yield` runs on startup; code after runs on shutdown.
#
# We load the model here so it is in memory before any request arrives.
# The first request does NOT pay the model-loading cost.
# ============================================
@asynccontextmanager
async def lifespan(app: FastAPI):
    # --- STARTUP ---
    load_model()
    yield
    # --- SHUTDOWN (nothing to clean up for the model) ---


# ============================================
# APP INSTANCE
# ============================================
app = FastAPI(
    title="Project S — Sentiment Analysis Service",
    description="Runs RoBERTa-based sentiment analysis on free-text survey responses.",
    version="1.0.0",
    lifespan=lifespan,
)


# ============================================
# REQUEST / RESPONSE MODELS
# ============================================
# Pydantic models define the shape of the JSON body for the endpoint.
# FastAPI uses them to validate incoming data and serialize the response.
# ============================================

class AnalyzeRequest(BaseModel):
    """
    The JSON body sent by the ASP.NET Core API when requesting analysis.

    text — the raw free-text cell value from the CSV response.
            Example: "The support team was incredibly helpful and fast."
    """
    text: str


class AnalyzeResponse(BaseModel):
    """
    The JSON body this service returns after running the model.

    label    — winning sentiment: "positive" | "neutral" | "negative"
    positive — probability that the text is positive (0.0–1.0)
    neutral  — probability that the text is neutral  (0.0–1.0)
    negative — probability that the text is negative (0.0–1.0)

    The three scores sum to ~1.0 (softmax output from the model).
    """
    label: str
    positive: float
    neutral: float
    negative: float


# ============================================
# ENDPOINTS
# ============================================

# ----------------------------------------
# GET /health — Liveness Check
# ----------------------------------------
# Used by:
#   - Docker healthcheck (docker-compose.yml)
#   - Azure load balancer health probes
#   - ASP.NET Core startup check before routing NLP requests
#
# Returns 200 {"status": "healthy"} if the service is up.
# ----------------------------------------
@app.get("/health")
def health():
    return {"status": "healthy"}


# ----------------------------------------
# POST /analyze — Run Sentiment Analysis
# ----------------------------------------
# Accepts: { "text": "The product was great!" }
# Returns: { "label": "positive", "positive": 0.94, "neutral": 0.04, "negative": 0.02 }
#
# The ASP.NET Core API calls this once per text cell in the uploaded CSV.
# Results are stored as SentimentResult rows in PostgreSQL.
# ----------------------------------------
@app.post("/analyze", response_model=AnalyzeResponse)
def analyze(request: AnalyzeRequest):
    # Reject empty strings — the model will produce nonsense output on them
    # and they represent missing/blank cells that don't need analysis.
    if not request.text.strip():
        raise HTTPException(
            status_code=400,
            detail="text must not be empty or whitespace-only.",
        )

    # run_sentiment() calls the loaded RoBERTa pipeline.
    # Returns {"label": ..., "positive": ..., "neutral": ..., "negative": ...}
    result = run_sentiment(request.text)

    return result
