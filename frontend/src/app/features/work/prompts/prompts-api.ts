import { inject, Injectable } from '@angular/core';
import { ApiClient } from '@core/api/api-client';

/** 使用者的提示範本。 */
@Injectable({ providedIn: 'root' })
export class PromptsApi {
  private readonly api = inject(ApiClient);
  prompts = () => this.api.get('/api/v1/prompt-templates');
  savePrompt = (id: string | null, title: string, content: string) =>
    id
      ? this.api.put('/api/v1/prompt-templates/{id}', { path: { id }, body: { title, content } })
      : this.api.post('/api/v1/prompt-templates', { body: { title, content } });
  deletePrompt = (id: string) => this.api.delete('/api/v1/prompt-templates/{id}', { path: { id } });
}
