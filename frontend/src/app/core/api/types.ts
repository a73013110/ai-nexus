import type { components } from './schema';

type Dto<Name extends keyof components['schemas']> = Required<components['schemas'][Name]>;
export type Me = Dto<'MeDto'>;
export type Preferences = Dto<'PreferencesDto'>;
export type Model = Dto<'ModelDto'>;
export type Models = Dto<'ModelsDto'>;
export type Conversation = Dto<'ConversationDto'>;
export type Message = Dto<'MessageDto'>;
export type ConversationDetail = Dto<'ConversationDetailDto'>;
export type Run = Dto<'RunDto'>;
export type RunEvent = Dto<'RunEventDto'>;
export type CreateRun = Dto<'CreateRunRequest'>;
export type AuthSession = Dto<'AuthSessionDto'>;
export type ModelPolicy = Dto<'ModelPolicyDto'>;
export type ContextUsage = Dto<'ContextUsageDto'>;
export type ContextPreview = Dto<'ContextPreviewRequest'>;
export const isActive = (state: string) => state === 'queued' || state === 'running';
