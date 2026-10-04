import type { components } from './schema';

type Dto<Name extends keyof components['schemas']> = Required<components['schemas'][Name]>;
export type Me = Dto<'MeDto'>;
export type ReadonlyShare = Dto<'ShareDto'>;
export type SharedContent = Dto<'SharedContentDto'> & {
  share: ReadonlyShare;
  snapshot: Dto<'ShareSnapshot'> & {
    messages: (Dto<'SharedMessageDto'> & { attachments: Attachment[] })[];
  };
};
export type Project = Dto<'ProjectDto'> & { resource: Dto<'ResourceDto'> };
export type ProjectTemplate = Dto<'ProjectTemplateDto'>;
export type Preferences = Dto<'PreferencesDto'>;
export type UserSettings = Dto<'UserSettingsDto'> & { appearance: Preferences };
export type PersonalUsage = Dto<'PersonalUsageDto'> & { daily: Dto<'UsageDayDto'>[] };
export type AdminCatalog = Dto<'AdminCatalogDto'>;
export type AdminRole = Dto<'AdminRoleDto'>;
export type AdminGroup = Dto<'AdminGroupDto'>;
export type AdminFeature = Dto<'AdminFeatureDto'>;
export type AdminUser = Dto<'AdminUserDto'>;
export type AdminUsers = Dto<'AdminUsersDto'>;
export type AdminUsage = Dto<'AdminUsageDto'>;
export type AuditEntry = Dto<'AuditDto'>;
export type EffectiveModelPolicy = Dto<'EffectiveModelPolicyDto'>;
export type Access = Dto<'AccessDto'>;
export type Model = Dto<'ModelDto'>;
export type Models = Dto<'ModelsDto'>;
export type Conversation = Dto<'ConversationDto'>;
export type Message = Omit<Dto<'MessageDto'>, 'sources' | 'feedbackRating'> &
  Pick<components['schemas']['MessageDto'], 'sources' | 'feedbackRating'>;
export type Feedback = Dto<'FeedbackDto'>;
export type EvaluationCase = Dto<'EvaluationCase'>;
export type EvaluationVariant = Dto<'EvaluationVariant'>;
export type EvaluationSet = Dto<'EvaluationSetDto'> & {
  resource: Resource;
  cases: EvaluationCase[];
};
export type EvaluationRun = Dto<'EvaluationRunDto'> & { job: Job };
export type EvaluationResult = Dto<'EvaluationResultDto'>;
export type EvaluationDetail = Dto<'EvaluationDetailDto'> & {
  run: EvaluationRun;
  cases: EvaluationCase[];
  variants: EvaluationVariant[];
  results: EvaluationResult[];
};
export type ConversationDetail = Dto<'ConversationDetailDto'>;
export type Run = Dto<'RunDto'>;
export type RunEvent = Dto<'RunEventDto'>;
export type CreateRun = Dto<'CreateRunRequest'>;
export type AuthSession = Dto<'AuthSessionDto'>;
export type ModelPolicy = Dto<'ModelPolicyDto'>;
export type ContextUsage = Omit<Dto<'ContextUsageDto'>, 'reservedKnowledgeTokens'> &
  Pick<components['schemas']['ContextUsageDto'], 'reservedKnowledgeTokens'>;
export type ContextPreview = Dto<'ContextPreviewRequest'>;
export type Attachment = Dto<'AttachmentDto'>;
export type AttachmentPolicy = Dto<'AttachmentPolicyDto'>;
export type PromptTemplate = Dto<'PromptTemplateDto'>;
export type ConversationBackup = Dto<'ConversationBackup'>;
export type ConversationSettings = components['schemas']['ConversationSettingsRequest'];
export type Job = Dto<'JobDto'>;
export type Resource = Dto<'ResourceDto'>;
export type ResourceAcl = Dto<'ResourceAclDto'> & { members: Dto<'ResourceMemberDto'>[] };
export type ResourceAclRequest = Dto<'ResourceAclRequest'>;
export type DirectoryUser = Dto<'DirectoryUserDto'>;
export type DirectoryGroup = Dto<'DirectoryGroupDto'>;
export type Collection = Dto<'CollectionDto'> & { resource: Resource };
export type DocumentInfo = Dto<'DocumentDto'>;
export type DocumentPage = Dto<'DocumentPageDto'>;
export type DocumentJob = Dto<'DocumentJobDto'> & { job: Job };
export type KnowledgeSearch = Dto<'KnowledgeSearchDto'>;
export type Citation = Dto<'CitationDto'>;
export type ArtifactSummary = Dto<'ArtifactSummaryDto'> & { resource: Resource };
export type ArtifactDocument = Dto<'ArtifactDto'> & { resource: Resource };
export type ArtifactRevision = Dto<'ArtifactRevisionDto'>;
export type TransformResult = Dto<'TransformTextDto'>;
export const isActive = (state: string) => state === 'queued' || state === 'running';
