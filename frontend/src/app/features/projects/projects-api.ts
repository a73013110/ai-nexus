import { inject, Injectable } from '@angular/core';
import { ApiClient } from '../../core/api/api-client';

@Injectable({ providedIn: 'root' })
export class ProjectsApi {
  private readonly api = inject(ApiClient);
  list = () => this.api.get('/api/v1/projects');
  get = (id: string) => this.api.get('/api/v1/projects/{id}', { path: { id } });
  remove = (id: string) => this.api.delete('/api/v1/projects/{id}', { path: { id } });
  save = (
    id: string | null,
    name: string,
    description: string,
    instructions: string,
    expectedVersion = 1,
    isArchived = false,
  ) => {
    const body = { name, description, instructions, expectedVersion, isArchived };
    return id
      ? this.api.put('/api/v1/projects/{id}', { path: { id }, body })
      : this.api.post('/api/v1/projects', { body });
  };
  files = (id: string) => this.api.get('/api/v1/projects/{id}/files', { path: { id } });
  addFile = (id: string, attachmentId: string) =>
    this.api.post('/api/v1/projects/{id}/files', { path: { id }, body: { attachmentId } });
  templates = (id: string) => this.api.get('/api/v1/projects/{id}/templates', { path: { id } });
  saveTemplate = (id: string, key: string | null, title: string, content: string) =>
    key
      ? this.api.put('/api/v1/projects/{id}/templates/{key}', {
          path: { id, key },
          body: { title, content },
        })
      : this.api.post('/api/v1/projects/{id}/templates', {
          path: { id },
          body: { title, content },
        });
  removeTemplate = (id: string, key: string) =>
    this.api.delete('/api/v1/projects/{id}/templates/{key}', { path: { id, key } });
  conversations = (id: string) =>
    this.api.get('/api/v1/projects/{id}/conversations', { path: { id } });
  start = (id: string, templateId?: string) =>
    this.api.post('/api/v1/projects/{id}/conversations', {
      path: { id },
      body: { templateId: templateId ?? null },
    });
  assign = (id: string, projectId: string | null) =>
    this.api.put('/api/v1/conversations/{id}/project', { path: { id }, body: { projectId } });
}
