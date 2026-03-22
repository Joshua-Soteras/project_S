// ============================================
// survey.model.ts — TypeScript interfaces
// ============================================
// These mirror the JSON shapes returned by the ASP.NET Core API.
// Keeping them in a dedicated models file means every component
// gets the same type definitions — no duplicated interfaces.
// ============================================

export interface SurveyColumn {
  id: number;
  columnName: string;
  columnType: string;    // "text" | "numeric" | "date" | "boolean"
  analyzeSentiment: boolean;
  columnIndex: number;
}

/** Summary shape returned by GET /api/surveys (list). No columns included. */
export interface Survey {
  id: number;
  name: string;
  status: string;        // "queued" | "processing" | "complete" | "error"
  totalRows: number;
  processedRows: number;
  uploadedBy: string;
  uploadedAt: string;    // ISO 8601 date string
  completedAt: string | null;
}

/** Detail shape returned by GET /api/surveys/:id. Includes columns. */
export interface SurveyDetail extends Survey {
  errorMessage: string | null;
  columns: SurveyColumn[];
}

/** Shape returned by POST /api/surveys after a successful upload. */
export interface UploadResult {
  id: number;
  name: string;
  status: string;
  totalRows: number;
  columnCount: number;
  sentimentAnalyzed: number;
}
