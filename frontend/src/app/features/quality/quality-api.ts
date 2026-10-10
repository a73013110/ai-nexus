import { Injectable, inject } from '@angular/core';
import { ApiClient } from '../../core/api/api-client';
import type {
  EvaluationCase,
  EvaluationVariantRequest,
  RetrievalEvaluationRequest,
} from '../../core/api/schema';

@Injectable({ providedIn: 'root' })
export class QualityApi {
  private readonly api = inject(ApiClient);
  feedback = (id: string, rating: number, reason = '', note = '') =>
    this.api.put('/api/v1/messages/{id}/feedback', {
      path: { id },
      body: { rating, reason, note },
    });
  feedbackList = () => this.api.get('/api/v1/quality/feedback');
  feedbackFor = (id: string) => this.api.get('/api/v1/messages/{id}/feedback', { path: { id } });
  sets = () => this.api.get('/api/v1/quality/sets');
  get = (id: string) => this.api.get('/api/v1/quality/sets/{id}', { path: { id } });
  remove = (id: string) => this.api.delete('/api/v1/quality/sets/{id}', { path: { id } });
  save = (
    id: string | null,
    name: string,
    description: string,
    cases: EvaluationCase[],
    expectedVersion = 1,
  ) => {
    const body = { name, description, cases, expectedVersion };
    return id
      ? this.api.put('/api/v1/quality/sets/{id}', { path: { id }, body })
      : this.api.post('/api/v1/quality/sets', { body });
  };
  runs = (id: string) => this.api.get('/api/v1/quality/sets/{id}/runs', { path: { id } });
  run = (id: string, variants: EvaluationVariantRequest[]) =>
    this.api.post('/api/v1/quality/sets/{id}/runs', { path: { id }, body: { variants } });
  detail = (id: string) => this.api.get('/api/v1/quality/runs/{id}', { path: { id } });
  retrievalEvaluations = () => this.api.get('/api/v1/quality/retrieval-evals');
  retrievalEvaluation = (id: string) =>
    this.api.get('/api/v1/quality/retrieval-evals/{id}', { path: { id } });
  retrievalReport = (id: string) =>
    this.api.get('/api/v1/quality/retrieval-evals/{id}/report', { path: { id } });
  startRetrievalEvaluation = (body: RetrievalEvaluationRequest) =>
    this.api.post('/api/v1/quality/retrieval-evals', { body });
  review = (
    id: string,
    caseIndex: number,
    variantIndex: number,
    score: number | null,
    note: string,
  ) =>
    this.api.put('/api/v1/quality/runs/{id}/results/{caseIndex}/{variantIndex}/review', {
      path: { id, caseIndex, variantIndex },
      body: { score, note },
    });
}
