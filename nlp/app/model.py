# ============================================
# model.py
# ============================================
#
# RESPONSIBILITY:
# Loads the RoBERTa sentiment model once at startup and exposes
# a single analyze() function that returns sentiment scores for
# a given piece of text.
#
# MODEL: cardiffnlp/twitter-roberta-base-sentiment-latest
# Trained on ~124M tweets. Returns three labels:
#   - negative  (LABEL_0)
#   - neutral   (LABEL_1)
#   - positive  (LABEL_2)
#
# WHY A SEPARATE MODULE?
# Loading the model takes 2–5 seconds and ~700MB of RAM.
# Isolating it here means:
#   1. The app loads it once at startup (not per request).
#   2. Unit tests can mock this module without touching FastAPI.
#   3. If we swap models, only this file changes.
# ============================================

from transformers import pipeline

# Module-level variable. None until load_model() is called at startup.
_pipeline = None


def load_model() -> None:
    """
    Load the RoBERTa sentiment pipeline into memory.

    Called once during app startup via FastAPI's lifespan hook.
    Loads from the local Hugging Face cache if the model was
    pre-downloaded at Docker build time — no network call needed.
    """
    global _pipeline

    # return_all_scores=True means we get a score for every label
    # (negative, neutral, positive) rather than just the top one.
    # We need all three to return them in the API response.
    _pipeline = pipeline(
        "text-classification",
        model="cardiffnlp/twitter-roberta-base-sentiment-latest",
        return_all_scores=True,
    )


def analyze(text: str) -> dict:
    """
    Run sentiment analysis on a single text string.

    Returns a dict with:
      label    — the winning sentiment ("positive", "neutral", "negative")
      positive — probability score for positive sentiment (0.0 to 1.0)
      neutral  — probability score for neutral sentiment  (0.0 to 1.0)
      negative — probability score for negative sentiment (0.0 to 1.0)

    The three scores always sum to ~1.0 (softmax output).

    Example:
        analyze("Great product, very happy!")
        → { "label": "positive", "positive": 0.94, "neutral": 0.04, "negative": 0.02 }
    """
    if _pipeline is None:
        raise RuntimeError("Model has not been loaded. Call load_model() first.")

    # _pipeline(text) returns a list of lists (one per input).
    # Since we pass a single string, we get [[{label, score}, ...]].
    # [0] unwraps the outer list to get the per-label results for our text.
    results: list[dict] = _pipeline(text)[0]

    # Build a lookup: { "negative": 0.02, "neutral": 0.04, "positive": 0.94 }
    # .lower() normalizes label names in case the model returns uppercase.
    scores = {r["label"].lower(): r["score"] for r in results}

    # The winning label is whichever has the highest probability.
    # Using scores[k] instead of scores.get to satisfy the type checker
    # (dict.get returns V | None, but we know all keys exist here).
    label = max(scores, key=lambda k: scores[k])

    return {
        "label": label,
        "positive": scores.get("positive", 0.0),
        "neutral": scores.get("neutral", 0.0),
        "negative": scores.get("negative", 0.0),
    }
