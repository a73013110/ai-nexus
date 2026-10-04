import { inject, Injectable } from '@angular/core';
import { ApiTransport } from '../../core/api/api-transport';
import type { Conversation, DocumentInfo, Project, ProjectTemplate } from '../../core/api/types';
@Injectable({ providedIn: 'root' })
export class ProjectsApi {
  private readonly http = inject(ApiTransport);
  list = () => this.http.json<Project[]>('/projects');
  get = (id: string) => this.http.json<Project>(`/projects/${id}`);
  remove = (id: string) => this.http.json<void>(`/projects/${id}`, 'DELETE');
  save = (
    id: string | null,
    name: string,
    description: string,
    instructions: string,
    expectedVersion = 1,
    isArchived = false,
  ) =>
    this.http.json<Project>(`/projects${id ? '/' + id : ''}`, id ? 'PUT' : 'POST', {
      name,
      description,
      instructions,
      expectedVersion,
      isArchived,
    });
  files = (id: string) => this.http.json<DocumentInfo[]>(`/projects/${id}/files`);
  addFile = (id: string, attachmentId: string) =>
    this.http.json<DocumentInfo>(`/projects/${id}/files`, 'POST', { attachmentId });
  templates = (id: string) => this.http.json<ProjectTemplate[]>(`/projects/${id}/templates`);
  saveTemplate = (id: string, key: string | null, title: string, content: string) =>
    this.http.json<ProjectTemplate>(
      `/projects/${id}/templates${key ? '/' + key : ''}`,
      key ? 'PUT' : 'POST',
      { title, content },
    );
  removeTemplate = (id: string, key: string) =>
    this.http.json<void>(`/projects/${id}/templates/${key}`, 'DELETE');
  conversations = (id: string) => this.http.json<Conversation[]>(`/projects/${id}/conversations`);
  start = (id: string, templateId?: string) =>
    this.http.json<{ conversation: Conversation; prompt: string }>(
      `/projects/${id}/conversations`,
      'POST',
      { templateId: templateId ?? null },
    );
  assign = (id: string, projectId: string | null) =>
    this.http.json<Conversation>(`/conversations/${id}/project`, 'PUT', { projectId });
}
