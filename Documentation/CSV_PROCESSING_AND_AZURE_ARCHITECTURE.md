# CSV Processing & Azure Architecture Guide

This document covers strategies for processing CSV data efficiently in a Nuxt + ASP.NET Core stack, and how to deploy the application to Azure.

---

## Table of Contents

1. [CSV Processing Options](#1-csv-processing-options)
2. [Frontend Processing with Papa Parse](#2-frontend-processing-with-papa-parse)
3. [Azure Hosting Architecture](#3-azure-hosting-architecture)
4. [CSV Processing Pipeline](#4-csv-processing-pipeline)
5. [Recommendations](#5-recommendations)
6. [Sources & References](#6-sources--references)

---

## 1. CSV Processing Options

### Backend Libraries (ASP.NET Core)

#### CsvHelper (Recommended for Parsing)

The most popular and efficient CSV parsing library for .NET.

```bash
dotnet add package CsvHelper
```

**Features:**
- Fast parsing with streaming support
- Automatic mapping to C# classes
- Handles edge cases (quotes, escapes, different delimiters)

#### Bulk Insert Methods (Database Performance)

For PostgreSQL, three options ranked by speed:

| Method | Speed | Best For | Package |
|--------|-------|----------|---------|
| **Npgsql COPY** | Fastest | Large datasets (10k+ rows) | `Npgsql` |
| **EFCore.BulkExtensions** | Fast | Medium datasets, EF Core integration | `EFCore.BulkExtensions` |
| **EF Core AddRange** | Slowest | Small datasets (<1000 rows) | Built-in |

##### Npgsql COPY (Fastest)

PostgreSQL's native bulk import using binary `COPY` protocol.

```bash
dotnet add package Npgsql
```

- Streams data directly to PostgreSQL
- Bypasses EF Core overhead
- Can insert 100k+ rows in seconds

##### EFCore.BulkExtensions (Good Balance)

```bash
dotnet add package EFCore.BulkExtensions
```

- Extends EF Core with `BulkInsert()`, `BulkUpdate()`
- Uses optimized SQL under the hood
- Maintains EF Core patterns

##### EF Core AddRange (Simple)

Built-in, no extra packages. Suitable for small files (<1000 rows).

### Performance Tips

1. **Stream the file** - Don't load entire CSV into memory
2. **Batch inserts** - Process 1000-5000 rows at a time
3. **Disable change tracking** - `context.ChangeTracker.AutoDetectChangesEnabled = false`
4. **Use transactions** - Wrap batches in a transaction
5. **Validate before insert** - Catch errors early

---

## 2. Frontend Processing with Papa Parse

Papa Parse is a fast, in-browser CSV parser for JavaScript.

### When to Use Papa Parse

| Pros | Cons |
|------|------|
| Fast parsing in browser | Large files can freeze browser |
| Instant preview/validation for user | Browser memory limits |
| User can map columns before upload | Still need backend validation |
| Reduces server parsing load | JSON payload can be larger than raw CSV |

### Architecture Pattern

```
Frontend (Nuxt + Papa Parse)
    │
    ├─► Parse CSV
    ├─► Validate & show preview to user
    ├─► Let user confirm / map columns
    │
    │  POST /api/data/import  (JSON array)
    ▼
Backend (ASP.NET Core)
    │
    ├─► Validate again (never trust client)
    └─► Bulk insert to PostgreSQL
```

### File Size Recommendations

| File Size | Recommendation |
|-----------|----------------|
| < 10,000 rows | Papa Parse on frontend → JSON to backend |
| 10,000 - 50,000 rows | Papa Parse with **Web Worker** → JSON to backend |
| 50,000+ rows | Upload raw CSV → process on backend |

### Papa Parse Configuration Tips

**Use Web Workers for large files:**
```js
Papa.parse(file, {
  worker: true,  // Uses Web Worker - prevents UI freezing
  complete: (results) => { ... }
})
```

**Stream for very large files:**
```js
Papa.parse(file, {
  step: (row) => { /* process row by row */ },
  complete: () => { /* done */ }
})
```

**Chunk the upload:**
- Don't send 50k rows in one request
- Send in batches of 1000-5000 rows
- Show progress to user

---

## 3. Azure Hosting Architecture

### Architecture Overview

```
                         ┌─────────────────────────────────────────────────────────┐
                         │                      AZURE                              │
                         │                                                         │
   Users                 │   ┌─────────────────┐      ┌─────────────────┐         │
     │                   │   │  Azure Static   │      │ Azure Container │         │
     │                   │   │   Web Apps      │      │     Apps        │         │
     ▼                   │   │                 │      │                 │         │
┌─────────┐              │   │   (Frontend)    │ ───► │   (Backend)     │         │
│ Browser │─────────────►│   │    Nuxt 4       │ API  │  ASP.NET Core   │         │
└─────────┘              │   │                 │      │                 │         │
                         │   └─────────────────┘      └────────┬────────┘         │
                         │                                     │                  │
                         │                                     ▼                  │
                         │                            ┌─────────────────┐         │
                         │                            │ Azure Database  │         │
                         │                            │ for PostgreSQL  │         │
                         │                            │ (Flexible)      │         │
                         │                            └─────────────────┘         │
                         │                                                         │
                         └─────────────────────────────────────────────────────────┘
```

### Core Azure Services

| Component | Azure Service | Containerized? | Why This Choice |
|-----------|---------------|----------------|-----------------|
| **Frontend** | Azure Static Web Apps | No | Built for Nuxt/Vue, global CDN, free SSL |
| **Backend** | Azure Container Apps | Yes | Serverless containers, auto-scaling, cost-effective |
| **Database** | Azure Database for PostgreSQL (Flexible Server) | No (managed) | Fully managed, backups, HA built-in |

### Supporting Services

| Service | Purpose |
|---------|---------|
| **Azure Container Registry** | Store your backend Docker images |
| **Azure Blob Storage** | Store uploaded CSV files |
| **Azure Key Vault** | Store secrets (DB passwords, API keys) |
| **Application Insights** | Monitoring, logging, performance tracking |

### What Gets Containerized?

| Component | Containerized? | Reason |
|-----------|----------------|--------|
| **Backend (ASP.NET Core)** | ✅ Yes | Consistent deployments, easy scaling |
| **Frontend (Nuxt)** | ❌ No | Static Web Apps handles build/deploy |
| **PostgreSQL** | ❌ No | Use managed service (don't manage DB yourself) |

### Cost Estimate (Small/Medium App)

| Service | Tier | Estimated Monthly Cost |
|---------|------|------------------------|
| Static Web Apps | Free | $0 |
| Container Apps | Consumption | $0-20 (scales to zero) |
| PostgreSQL Flexible | Burstable B1ms | ~$15-25 |
| Container Registry | Basic | ~$5 |
| Blob Storage | Hot | ~$1-5 |
| **Total** | | **~$20-55/month** |

### Alternative: All-Container Approach

If you want everything containerized (e.g., for SSR with Nuxt):

```
┌─────────────────────────────────────────────────────────────────┐
│              Azure Container Apps Environment                   │
│                                                                 │
│   ┌─────────────┐         ┌─────────────┐                      │
│   │  Frontend   │         │  Backend    │                      │
│   │  Container  │ ──────► │  Container  │                      │
│   │  (Nuxt SSR) │         │ (ASP.NET)   │                      │
│   └─────────────┘         └──────┬──────┘                      │
│                                  │                              │
└──────────────────────────────────┼──────────────────────────────┘
                                   │
                                   ▼
                          ┌─────────────────┐
                          │   PostgreSQL    │
                          │   (Managed)     │
                          └─────────────────┘
```

---

## 4. CSV Processing Pipeline

### Option A: Client-Side Processing (User's Machine)

Processing happens **in the user's browser** using Papa Parse.

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           USER'S MACHINE (Browser)                          │
│                                                                             │
│   ┌─────────┐      ┌─────────────┐      ┌─────────────┐                    │
│   │  CSV    │ ───► │ Papa Parse  │ ───► │  Validated  │                    │
│   │  File   │      │ (parsing)   │      │  JSON Data  │                    │
│   └─────────┘      └─────────────┘      └──────┬──────┘                    │
│                                                │                            │
│                    CPU + RAM = User's Computer │                            │
└────────────────────────────────────────────────┼────────────────────────────┘
                                                 │
                                                 │  POST /api/import (JSON)
                                                 ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                                  AZURE                                      │
│                                                                             │
│   ┌─────────────────┐      ┌─────────────────┐                             │
│   │  Container Apps │ ───► │   PostgreSQL    │                             │
│   │  (Validate +    │      │   (Store)       │                             │
│   │   Bulk Insert)  │      │                 │                             │
│   └─────────────────┘      └─────────────────┘                             │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

| What | Where |
|------|-------|
| CSV Parsing | User's browser (Papa Parse) |
| Validation | User's browser + Backend |
| Bulk Insert | Azure (Container Apps) |
| **Cost to you** | Lower (user's CPU does the work) |

### Option B: Server-Side Processing (Azure's Machines)

Processing happens **on Azure** using Functions or Container Apps.

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           USER'S MACHINE (Browser)                          │
│                                                                             │
│   ┌─────────┐                                                              │
│   │  CSV    │ ───────────────────────────────────────────────────────────► │
│   │  File   │   Upload raw file (no processing)                            │
│   └─────────┘                                                              │
│                                                                             │
│                    User just uploads, no CPU work                           │
└─────────────────────────────────────────────────────────────────────────────┘
                                                 │
                                                 │  POST file to Blob Storage
                                                 ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                                  AZURE                                      │
│                                                                             │
│   ┌──────────────┐      ┌─────────────────┐      ┌─────────────────┐       │
│   │ Blob Storage │ ───► │ Azure Functions │ ───► │   PostgreSQL    │       │
│   │ (Store CSV)  │      │ (Parse + Insert)│      │   (Store)       │       │
│   └──────────────┘      └─────────────────┘      └─────────────────┘       │
│                                │                                            │
│                                │                                            │
│                    CPU + RAM = Azure (you pay for it)                       │
└─────────────────────────────────────────────────────────────────────────────┘
```

| What | Where |
|------|-------|
| CSV Parsing | Azure Functions |
| Validation | Azure Functions |
| Bulk Insert | Azure Functions |
| **Cost to you** | Higher (Azure CPU does the work) |

### Comparison

| Factor | Client-Side (Papa Parse) | Server-Side (Azure) |
|--------|--------------------------|---------------------|
| **Processing cost** | Free (user's device) | You pay (Azure compute) |
| **Large files** | Can freeze browser | Handles easily |
| **User experience** | Instant preview | "Processing..." wait |
| **Weak devices** | Slow on old phones/laptops | Same speed for all |
| **Security** | Must re-validate on server | Single validation point |
| **Offline capable** | Yes (preview only) | No |

### Recommended: Hybrid Approach

Use **client-side for preview**, **server-side for large files**:

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              USER'S BROWSER                                 │
│                                                                             │
│   ┌─────────┐      ┌─────────────┐      ┌─────────────────┐                │
│   │  CSV    │ ───► │ Papa Parse  │ ───► │ Preview first   │                │
│   │  File   │      │             │      │ 100 rows        │                │
│   └─────────┘      └─────────────┘      └────────┬────────┘                │
│                                                  │                          │
│                           ┌──────────────────────┼──────────────────────┐   │
│                           │                      │                      │   │
│                           ▼                      ▼                      │   │
│                    ┌─────────────┐        ┌─────────────┐               │   │
│                    │ < 5,000     │        │ > 5,000     │               │   │
│                    │ rows        │        │ rows        │               │   │
│                    └──────┬──────┘        └──────┬──────┘               │   │
│                           │                      │                      │   │
└───────────────────────────┼──────────────────────┼──────────────────────────┘
                            │                      │
              ──────────────┘                      └──────────────
             │                                                   │
             ▼                                                   ▼
┌────────────────────────┐                        ┌────────────────────────┐
│ Send JSON to API       │                        │ Upload to Blob Storage │
│ (Client processed)     │                        │ (Server processes)     │
└────────────────────────┘                        └────────────────────────┘
```

**Logic:**
```javascript
if (rowCount < 5000) {
  // Client processes, sends JSON to API
} else {
  // Upload raw file to Blob Storage, let Azure process
}
```

### For Large-Scale CSV Processing

When handling very large files (100k+ rows):

```
┌──────────┐     ┌──────────────┐     ┌─────────────────┐     ┌──────────┐
│ Frontend │────►│ Blob Storage │────►│ Azure Functions │────►│ Database │
└──────────┘     │ (CSV files)  │     │ (Process CSV)   │     └──────────┘
                 └──────────────┘     └─────────────────┘
                                              │
                                              ▼
                                      ┌─────────────────┐
                                      │ Service Bus     │
                                      │ (Job Queue)     │
                                      └─────────────────┘
```

| Service | Purpose |
|---------|---------|
| **Blob Storage** | Upload CSV directly (bypasses API size limits) |
| **Azure Functions** | Serverless CSV processing (pay per execution) |
| **Service Bus** | Queue large jobs, retry failed imports |

### Cost Impact

For 100 users uploading 10,000-row CSVs daily:

| Approach | Monthly Cost |
|----------|--------------|
| **Client-side (Papa Parse)** | ~$5-10 (just API + DB) |
| **Server-side (Azure Functions)** | ~$20-50 (compute + storage) |
| **Hybrid** | ~$10-20 (best of both) |

---

## 5. Recommendations

### CSV Processing by File Size

| File Size | Process Where | Method |
|-----------|---------------|--------|
| < 1,000 rows | Client | Papa Parse → EF Core AddRange |
| 1,000 - 5,000 rows | Client | Papa Parse → EFCore.BulkExtensions |
| 5,000 - 50,000 rows | Client (Web Worker) | Papa Parse → EFCore.BulkExtensions |
| 50,000 - 100,000 rows | Server | Blob Storage → Azure Functions → Npgsql COPY |
| 100,000+ rows | Server (Queued) | Blob Storage → Service Bus → Azure Functions |

### Azure Services by Use Case

| Use Case | Recommended Setup |
|----------|-------------------|
| **Simple app, low traffic** | Static Web Apps + Container Apps + PostgreSQL Flexible |
| **Need SSR** | Container Apps (both frontend + backend) + PostgreSQL |
| **Heavy CSV processing** | Add Azure Functions + Blob Storage + Service Bus |
| **Enterprise scale** | Consider Azure Kubernetes Service (AKS) |

---

## 6. Sources & References

### CSV Processing Libraries

| Library | Documentation |
|---------|---------------|
| **CsvHelper** (.NET) | https://joshclose.github.io/CsvHelper/ |
| **Papa Parse** (JavaScript) | https://www.papaparse.com/ |
| **EFCore.BulkExtensions** | https://github.com/borber/EFCore.BulkExtensions |
| **Npgsql** | https://www.npgsql.org/doc/copy.html |

### Azure Services

| Service | Documentation |
|---------|---------------|
| **Azure Static Web Apps** | https://learn.microsoft.com/en-us/azure/static-web-apps/ |
| **Azure Container Apps** | https://learn.microsoft.com/en-us/azure/container-apps/ |
| **Azure Database for PostgreSQL** | https://learn.microsoft.com/en-us/azure/postgresql/ |
| **Azure Functions** | https://learn.microsoft.com/en-us/azure/azure-functions/ |
| **Azure Blob Storage** | https://learn.microsoft.com/en-us/azure/storage/blobs/ |
| **Azure Service Bus** | https://learn.microsoft.com/en-us/azure/service-bus-messaging/ |
| **Azure Container Registry** | https://learn.microsoft.com/en-us/azure/container-registry/ |
| **Azure Key Vault** | https://learn.microsoft.com/en-us/azure/key-vault/ |
| **Application Insights** | https://learn.microsoft.com/en-us/azure/azure-monitor/app/app-insights-overview |

### Pricing Calculators

| Resource | Link |
|----------|------|
| **Azure Pricing Calculator** | https://azure.microsoft.com/en-us/pricing/calculator/ |
| **Azure Container Apps Pricing** | https://azure.microsoft.com/en-us/pricing/details/container-apps/ |
| **PostgreSQL Flexible Server Pricing** | https://azure.microsoft.com/en-us/pricing/details/postgresql/flexible-server/ |

### Additional Resources

| Topic | Link |
|-------|------|
| **Nuxt Deployment to Azure** | https://nuxt.com/deploy/azure |
| **ASP.NET Core with PostgreSQL** | https://learn.microsoft.com/en-us/aspnet/core/data/ef-rp/intro |
| **Web Workers API** | https://developer.mozilla.org/en-US/docs/Web/API/Web_Workers_API |
