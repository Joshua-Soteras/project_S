# How It All Connects — Angular Frontend Architecture

This document covers the Angular frontend in `frontend-angular/`. Read it
alongside the source files — each section points to the exact file and what
to look for.

---

## 1. Project Structure

```mermaid
graph TD
    subgraph Root["frontend-angular/"]
        A["angular.json\nProject config, build settings,\ndev server proxy wiring"]
        B["tailwind.config.js\ncontent paths for class scanning"]
        C["src/proxy.conf.json\n/api → http://localhost:5120"]
        D["src/styles.scss\n@tailwind base/components/utilities"]
        E["src/main.ts\nBootstraps App with appConfig"]
        F["src/index.html\n&lt;app-root&gt; mount point"]
    end

    subgraph App["src/app/"]
        G["app.ts\nRoot component\nHolds nav + router-outlet"]
        H["app.html\nNav bar + &lt;router-outlet /&gt;"]
        I["app.config.ts\nprovideRouter + provideHttpClient"]
        J["app.routes.ts\nRoute definitions"]
    end

    subgraph Models["src/app/models/"]
        K["survey.model.ts\nSurvey, SurveyDetail,\nSurveyColumn, UploadResult"]
    end

    subgraph Services["src/app/services/"]
        L["api.service.ts\nHttpClient wrapper\ngetSurveys · getSurvey · uploadSurvey"]
    end

    subgraph Components["src/app/components/"]
        M["survey-list/\nsurvey-list.ts + .html + .scss"]
        N["upload/\nupload.ts + .html + .scss"]
        O["dashboard/\ndashboard.ts + .html + .scss"]
    end

    E --> G
    G --> I
    I --> J
    J --> M & N & O
    M & N & O --> L
    L --> K
```

---

## 2. Bootstrap Chain

How Angular starts from a single entry point to a rendered page:

```mermaid
sequenceDiagram
    participant Browser
    participant Main   as src/main.ts
    participant Config as app.config.ts
    participant App    as app.ts (App component)
    participant Router as Angular Router
    participant Outlet as &lt;router-outlet&gt;

    Browser->>Main: load index.html → main.ts
    Main->>Config: bootstrapApplication(App, appConfig)
    Config->>Config: provideRouter(routes) — registers route table
    Config->>Config: provideHttpClient(withFetch()) — registers HttpClient
    Config-->>Main: ApplicationRef created
    Main->>App: render &lt;app-root&gt; → app.html
    App->>Router: &lt;router-outlet /&gt; activates router
    Router->>Router: match current URL to routes[]
    Router->>Outlet: render matched component into outlet
```

**`src/main.ts`** calls `bootstrapApplication(App, appConfig)` — this is Angular's
standalone bootstrap. There is no `AppModule`. All providers are declared in
`app.config.ts` and passed directly.

---

## 3. Application Configuration (`app.config.ts`)

```mermaid
graph LR
    A["appConfig\nApplicationConfig"] --> B["provideBrowserGlobalErrorListeners()\nCatches unhandled errors globally"]
    A --> C["provideRouter(routes)\nRegisters the route table\nEnables routerLink, RouterOutlet"]
    A --> D["provideHttpClient(withFetch())\nRegisters HttpClient for DI\nwithFetch = uses browser Fetch API\nnot XMLHttpRequest"]
```

`provideHttpClient` must be here — without it, injecting `HttpClient` in
`ApiService` throws a runtime error. `withFetch()` is the modern default
for Angular 17+ and is required for SSR compatibility.

---

## 4. Routing

```mermaid
graph LR
    A["/"] --> B["SurveyList component\nsurvey-list/survey-list.ts"]
    C["/upload"] --> D["Upload component\nupload/upload.ts"]
    E["/surveys/:id"] --> F["Dashboard component\ndashboard/dashboard.ts"]
    G["** (any other)"] --> H["redirect to /"]
```

Routes are defined in `app.routes.ts` and passed to `provideRouter()`.
All three components are **standalone** — they import only what they need
directly in their `imports: []` array. There is no shared module.

---

## 5. Component + Service Class Diagram

```mermaid
classDiagram
    class ApiService {
        -HttpClient http
        -string base = "/api"
        +getSurveys() Observable~Survey[]~
        +getSurvey(id: number) Observable~SurveyDetail~
        +uploadSurvey(file: File, name?: string) Observable~UploadResult~
    }

    class SurveyList {
        -ApiService api
        +surveys: Survey[]
        +loading: boolean
        +error: string | null
        +ngOnInit() void
        +statusClass(status: string) string
    }

    class Upload {
        -ApiService api
        -Router router
        +selectedFile: File | null
        +surveyName: string
        +uploading: boolean
        +error: string | null
        +onFileSelected(event: Event) void
        +onSubmit() void
    }

    class Dashboard {
        -ApiService api
        -ActivatedRoute route
        +survey: SurveyDetail | null
        +loading: boolean
        +error: string | null
        +ngOnInit() void
        +statusClass(status: string) string
        +pct(value: number) string
    }

    class Survey {
        +id: number
        +name: string
        +status: string
        +totalRows: number
        +processedRows: number
        +uploadedBy: string
        +uploadedAt: string
        +completedAt: string | null
    }

    class SurveyDetail {
        +errorMessage: string | null
        +columns: SurveyColumn[]
    }

    class SurveyColumn {
        +id: number
        +columnName: string
        +columnType: string
        +analyzeSentiment: boolean
        +columnIndex: number
    }

    class UploadResult {
        +id: number
        +name: string
        +status: string
        +totalRows: number
        +columnCount: number
        +sentimentAnalyzed: number
    }

    SurveyList   --> ApiService : inject()
    Upload       --> ApiService : inject()
    Upload       --> Router     : inject()
    Dashboard    --> ApiService : inject()
    Dashboard    --> ActivatedRoute : inject()

    ApiService ..> Survey        : returns
    ApiService ..> SurveyDetail  : returns
    ApiService ..> UploadResult  : returns
    SurveyDetail --|> Survey     : extends
    SurveyDetail "1" *-- "0..*" SurveyColumn : columns
```

---

## 6. Imports Per Component (Standalone)

Because components are standalone, each one declares exactly which Angular
features it needs. This replaces the old `NgModule` shared imports approach.

| Component | Imports | Why |
|---|---|---|
| `SurveyList` | `RouterLink`, `DatePipe` | Navigation links, format `uploadedAt` date |
| `Upload` | `FormsModule` | `[(ngModel)]` two-way binding on the name input |
| `Dashboard` | `RouterLink`, `DatePipe` | Back link, format timestamps |
| `App` (root) | `RouterOutlet`, `RouterLink`, `RouterLinkActive` | Nav bar links + active class + page outlet |

---

## 7. Data Flow — Survey List Page

```mermaid
sequenceDiagram
    participant User
    participant SL   as SurveyList\nsurvey-list.ts
    participant API  as ApiService\napi.service.ts
    participant HTTP as HttpClient
    participant BE   as ASP.NET Core\nGET /api/surveys

    User->>SL: navigate to /
    SL->>SL: ngOnInit()
    SL->>SL: loading = true
    SL->>API: getSurveys()
    API->>HTTP: GET /api/surveys
    HTTP->>BE: (proxied from :4200 → :5120)
    BE-->>HTTP: 200 [ { id, name, status, totalRows... } ]
    HTTP-->>API: Observable emits Survey[]
    API-->>SL: surveys[]
    SL->>SL: this.surveys = surveys\nloading = false
    SL->>User: render table rows\none per survey
```

---

## 8. Data Flow — CSV Upload

```mermaid
sequenceDiagram
    participant User
    participant UP   as Upload\nupload.ts
    participant API  as ApiService\napi.service.ts
    participant HTTP as HttpClient
    participant BE   as ASP.NET Core\nPOST /api/surveys
    participant Nav  as Angular Router

    User->>UP: select .csv file → onFileSelected()
    UP->>UP: selectedFile = event.target.files[0]
    User->>UP: click "Upload CSV" → onSubmit()
    UP->>UP: validate selectedFile != null
    UP->>UP: uploading = true
    UP->>API: uploadSurvey(file, name?)
    API->>API: new FormData()\nappend('file', file)\nappend('name', name)
    API->>HTTP: POST /api/surveys  multipart/form-data
    HTTP->>BE: (proxied :4200 → :5120)
    Note over BE: Parse CSV, save rows,\nrun NLP, compute KPIs\n(can take seconds)
    BE-->>HTTP: 201 { id, name, status, sentimentAnalyzed... }
    HTTP-->>API: Observable emits UploadResult
    API-->>UP: result
    UP->>UP: uploading = false
    UP->>Nav: router.navigate(['/surveys', result.id])
    Nav->>User: render Dashboard for new survey
```

---

## 9. Dev Proxy Configuration

In development, Angular runs on port 4200 and the API runs on port 5120.
Without a proxy, every `HttpClient` call would hit a CORS error.

```mermaid
flowchart LR
    A["Angular component\nApiService.getSurveys()\nGET /api/surveys"] --> B["Angular Dev Server\nlocalhost:4200"]
    B --> C{path starts\nwith /api?}
    C -->|yes| D["proxy.conf.json\ntarget: http://localhost:5120\nsecure: false\nchangeOrigin: true"]
    D --> E["ASP.NET Core API\nlocalhost:5120\nGET /api/surveys"]
    C -->|no| F["Serve Angular app files\ndirectly"]
```

**`src/proxy.conf.json`** is registered in `angular.json` under
`architect.serve.options.proxyConfig`. It activates automatically with `ng serve` —
no flags needed.

In production, Angular and the API are served from the same origin, so
`/api/surveys` routes directly to the API without any proxy.

---

## 10. Template Syntax — Angular 17+ Control Flow

All three components use Angular's **built-in control flow** syntax instead of
`NgIf` / `NgFor` directives. This avoids importing `NgIf`/`NgFor` into every
standalone component.

```html
<!-- Old directive syntax (not used) -->
<div *ngIf="loading">Loading...</div>
<tr *ngFor="let survey of surveys">...</tr>

<!-- New built-in control flow (used in this project) -->
@if (loading) {
  <div>Loading...</div>
}

@for (survey of surveys; track survey.id) {
  <tr>...</tr>
}

@if (col.analyzeSentiment) {
  <span>✓ Analyzed</span>
} @else {
  <span>—</span>
}
```

`track survey.id` is required by `@for` — it tells Angular's change detection
algorithm which property uniquely identifies each item. Using `survey.id` (the
DB primary key) is the correct choice here.

---

## 11. Status Badge Pattern

Both `SurveyList` and `Dashboard` use the same `statusClass()` helper to
map API status strings to Tailwind CSS classes:

```
"complete"   → "bg-green-100 text-green-700"
"processing" → "bg-yellow-100 text-yellow-700"
"queued"     → "bg-gray-100 text-gray-600"
"error"      → "bg-red-100 text-red-700"
```

Used in templates as: `class="{{ statusClass(survey.status) }}"`.
The method lives on each component class directly — no shared service needed
since it is pure logic with no dependencies.
