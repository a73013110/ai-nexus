import { Injectable, inject } from '@angular/core';
import { ApiTransport } from '../../core/api/api-transport';
import type {
  EvaluationDetail,
  EvaluationSet,
  EvaluationRun,
  EvaluationCase,
  Feedback,
} from '../../core/api/types';

@Injectable({ providedIn: 'root' })
export class QualityApi {
  private readonly http = inject(ApiTransport);
  feedback = (id: string, rating: number, reason = '', note = '') =>
    this.http.json<Feedback | null>(`/messages/${id}/feedback`, 'PUT', { rating, reason, note });
  feedbackList = () => this.http.json<Feedback[]>('/quality/feedback');
  feedbackFor = (id: string) => this.http.json<Feedback | null>(`/messages/${id}/feedback`);
  sets = () => this.http.json<EvaluationSet[]>('/quality/sets');
  get = (id: string) => this.http.json<EvaluationSet>(`/quality/sets/${id}`);
  save = (
    id: string | null,
    name: string,
    description: string,
    cases: EvaluationCase[],
    expectedVersion = 1,
  ) =>
    this.http.json<EvaluationSet>(`/quality/sets${id ? '/' + id : ''}`, id ? 'PUT' : 'POST', {
      name,
      description,
      cases,
      expectedVersion,
    });
  runs = (id: string) => this.http.json<EvaluationRun[]>(`/quality/sets/${id}/runs`);
  run = (id: string, variants: { label: string; modelId: string | null; instruction: string }[]) =>
    this.http.json<EvaluationRun>(`/quality/sets/${id}/runs`, 'POST', { variants });
  detail = (id: string) => this.http.json<EvaluationDetail>(`/quality/runs/${id}`);
  review = (id: string, c: number, v: number, score: number | null, note: string) =>
    this.http.json<void>(`/quality/runs/${id}/results/${c}/${v}/review`, 'PUT', { score, note });
}
