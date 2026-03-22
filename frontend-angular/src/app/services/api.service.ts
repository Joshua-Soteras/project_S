// ============================================
// api.service.ts — HTTP client wrapper
// ============================================
//
// RESPONSIBILITY:
// Single place for all HTTP calls to the ASP.NET Core API.
// Components inject this service and call typed methods —
// they never construct URLs or touch HttpClient directly.
//
// The /api prefix is proxied to http://localhost:5120 in
// development via src/proxy.conf.json. In production the
// Angular app is served from the same origin as the API.
// ============================================

import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Survey, SurveyDetail, UploadResult } from '../models/survey.model';

@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api';

  /** GET /api/surveys — returns all surveys, newest first. */
  getSurveys(): Observable<Survey[]> {
    return this.http.get<Survey[]>(`${this.base}/surveys`);
  }

  /** GET /api/surveys/:id — returns survey metadata + detected columns. */
  getSurvey(id: number): Observable<SurveyDetail> {
    return this.http.get<SurveyDetail>(`${this.base}/surveys/${id}`);
  }

  /**
   * POST /api/surveys — uploads a CSV file for processing.
   *
   * Angular automatically sets Content-Type: multipart/form-data
   * with the correct boundary when the body is a FormData instance.
   * Do NOT set Content-Type manually — it will break the boundary.
   *
   * The optional name parameter overrides the default (filename without extension).
   */
  uploadSurvey(file: File, name?: string): Observable<UploadResult> {
    const form = new FormData();
    form.append('file', file);
    if (name) form.append('name', name);
    return this.http.post<UploadResult>(`${this.base}/surveys`, form);
  }
}
