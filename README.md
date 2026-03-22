# Project S — Survey Sentiment Analysis Platform

A fullstack web application for uploading survey CSV files, running automated
sentiment analysis on free-text responses using a RoBERTa NLP model, and
displaying results on a per-survey dashboard.

## What It Does

1. **Upload a CSV** — any CSV with text columns is accepted. The backend auto-detects
   column types (text, numeric, date, boolean) and flags text columns for NLP analysis.
2. **Async NLP Processing** — `POST /api/surveys` returns `202 Accepted` immediately
   after saving the CSV rows. A .NET `BackgroundService` picks the survey off an
   in-memory queue and calls a Python FastAPI microservice running the
   `cardiffnlp/twitter-roberta-base-sentiment-latest` RoBERTa model on each text cell.
3. **Dashboard** — the Angular frontend polls `GET /api/surveys/{id}` every 2s while
   processing, then renders survey stats, detected columns, and processing status.
   KPI aggregates (average positive/neutral/negative scores per column) are
   pre-computed and stored once when processing completes.

## Tech Stack

| Layer | Technology |
|---|---|
| Frontend | Angular 21, Tailwind CSS, standalone components |
| Backend API | ASP.NET Core (.NET 10) Minimal API |
| NLP Service | Python 3.11, FastAPI, Hugging Face Transformers, RoBERTa |
| Database | PostgreSQL (via EF Core + Npgsql) |
| Package mgmt (Python) | uv (pyproject.toml + uv.lock) |
| Containers | Docker + Docker Compose |

## Quick Start

```bash
# 1. Start PostgreSQL
docker-compose up -d postgres

# 2. Start Python NLP service (from nlp/)
cd nlp && uv run uvicorn app.main:app --reload

# 3. Start ASP.NET Core API (from backend/src/_03_Web_API/)
cd backend/src/_03_Web_API && dotnet run

# 4. Start Angular dev server (from frontend-angular/)
cd frontend-angular && ng serve
# → http://localhost:4200
```

See `CLAUDE.md` for full command reference and architecture details.
See `Documentation/` for detailed architecture diagrams and implementation logs.

---

# Nuxt Minimal Starter

Look at the [Nuxt documentation](https://nuxt.com/docs/getting-started/introduction) to learn more.

## Setup

Make sure to install dependencies:

```bash
# npm
npm install

# pnpm
pnpm install

# yarn
yarn install

# bun
bun install
```

## Development Server

Start the development server on `http://localhost:3000`:

```bash
# npm
npm run dev

# pnpm
pnpm dev

# yarn
yarn dev

# bun
bun run dev
```

## Production

Build the application for production:

```bash
# npm
npm run build

# pnpm
pnpm build

# yarn
yarn build

# bun
bun run build
```

Locally preview production build:

```bash
# npm
npm run preview

# pnpm
pnpm preview

# yarn
yarn preview

# bun
bun run preview
```

Check out the [deployment documentation](https://nuxt.com/docs/getting-started/deployment) for more information.


#backend 

```bash 
#creating template asp.net core
dotnet new webapi 

```