# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Architecture

This is a fullstack project with a monorepo structure:

- **frontend/** - Nuxt 4 application (Vue 3, Tailwind CSS)
- **backend/** - ASP.NET Core Web API (middleware layer)

The frontend and backend are separate services designed to be containerized with Docker.

## Frontend (Nuxt)

**Working directory:** Always `cd frontend/` before running frontend commands.

### Development Commands

```bash
# Install dependencies
npm install

# Run dev server (http://localhost:3000)
npm run dev

# Build for production
npm run build

# Preview production build
npm run preview

# Generate static site
npm run generate
```

### Configuration

- Uses Nuxt 4 with devtools enabled
- Tailwind CSS configured via `@nuxtjs/tailwindcss` module
- Main app entry point: `app/app.vue`

## Backend (ASP.NET Core)

**Working directory:** `cd backend/` before running backend commands.

The backend directory is currently empty and ready for ASP.NET Core Web API setup via `dotnet new webapi`.

## Docker Setup

The project is intended to be containerized with Docker, using a multi-container approach:
- Separate containers for frontend and backend
- Orchestrated with docker-compose at the root level

## Git Workflow

Current branch: `asp-net-core-setup-Josh`
Main branch: `main`

This is a collaborative project intended to be pushed and pulled with other team members.
