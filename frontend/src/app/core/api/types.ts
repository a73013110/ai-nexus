import type { components } from './schema';

type Dto<Name extends keyof components['schemas']> = Omit<Required<components['schemas'][Name]>, 'issueCode'> & { issueCode?: string | null };
// Older snapshots may omit the label; rendering always uses a safe display-name fallback.
type ModelReference<Name extends keyof components['schemas']> = Omit<
  Dto<Name>,
  'modelDisplayName'
> & {
  modelDisplayName?: string | null;
};
export type Me = Dto<'MeDto'>;
export type NotificationTarget = Dto<'NotificationTargetDto'>;
export type NotificationItem = Dto<'NotificationDto'> & { target: NotificationTarget };
export type NotificationPage = Dto<'NotificationPageDto'> & { items: NotificationItem[] };
export type LibraryFile = Dto<'LibraryFileDto'> & {
  file: Attachment;
  usages: Dto<'FileUsageDto'>[];
};
export type FileLibraryPage = Dto<'FileLibraryPageDto'> & { items: LibraryFile[] };
export type ReadonlyShare = Dto<'ShareDto'>;
export type SharedFilePreview = Dto<'SharedFilePreviewDto'> & {
  file: Attachment;
  pages: DocumentPage[];
};
export type SharedContent = Dto<'SharedContentDto'> & {
  share: ReadonlyShare;
  snapshot: Dto<'ShareSnapshot'> & {
    messages: (ModelReference<'SharedMessageDto'> & { attachments: Attachment[] })[];
  };
};
export type Project = Dto<'ProjectDto'> & { resource: Dto<'ResourceDto'> };
export type ProjectTemplate = Dto<'ProjectTemplateDto'>;
export type Preferences = Dto<'PreferencesDto'>;
export type UserSettings = Dto<'UserSettingsDto'> & { appearance: Preferences };
export type PersonalUsage = Omit<Dto<'PersonalUsageDto'>, 'tokens'> & {
  tokens?: TokenUsage | null;
} & { daily: Dto<'UsageDayDto'>[] };
export type AdminCatalog = Dto<'AdminCatalogDto'>;
export type AdminRole = Dto<'AdminRoleDto'>;
export type AdminGroup = Dto<'AdminGroupDto'>;
export type AdminFeature = Dto<'AdminFeatureDto'>;
export type Feature = Dto<'FeatureDto'>;
export type UsageTotals = Dto<'UsageTotalsDto'>;
export type AdminUser = Omit<
  Dto<'AdminUserDto'>,
  'activity' | 'enabled' | 'authentication' | 'storage'
> &
  Pick<components['schemas']['AdminUserDto'], 'enabled' | 'authentication' | 'storage'> & {
    activity?: (Dto<'AdminUserActivityDto'> & { usage: UsageTotals }) | null;
  };
export type AdminUserDetail = Dto<'AdminUserDetailDto'> & {
  user: AdminUser;
  usage: PersonalUsage;
  kinds: Dto<'UsageKindDto'>[];
};
export type AdminConversation = Dto<'AdminConversationDto'>;
export type AdminConversationPage = Dto<'AdminConversationPageDto'> & {
  items: AdminConversation[];
};
export type AdminConversationDetail = Dto<'AdminConversationDetailDto'> & {
  conversation: AdminConversation;
  messages: (ModelReference<'AdminMessageDto'> & { attachments: Attachment[] })[];
};
export type AdminUsers = Dto<'AdminUsersDto'>;
export type AdminUsage = Dto<'AdminUsageDto'>;
export type AuditEntry = Dto<'AuditDto'>;
export type EffectiveModelPolicy = Dto<'EffectiveModelPolicyDto'>;
export type Access = Dto<'AccessDto'>;
export type Model = Omit<Dto<'ModelDto'>, 'provider'> &
  Pick<components['schemas']['ModelDto'], 'provider'>;
export type Models = Dto<'ModelsDto'>;
export type Conversation = Dto<'ConversationDto'>;
export type Message = Omit<
  ModelReference<'MessageDto'>,
  'sources' | 'feedbackRating' | 'charge' | 'webSources' | 'webSearchCharge' | 'timing'
> &
  Pick<
    components['schemas']['MessageDto'],
    'sources' | 'feedbackRating' | 'charge' | 'webSources' | 'webSearchCharge' | 'timing'
  >;
export type Feedback = Dto<'FeedbackDto'>;
export type ExternalSource = Dto<'SourceDto'>;
export type SourceRecord = Dto<'SourceRecordDto'>;
export type SourceDetail = Dto<'SourceDetailDto'> & {
  record: SourceRecord;
  history: Dto<'SourceHistoryDto'>[];
};
export type SourceChat = Dto<'SourceChatDto'> & { conversation: Conversation };
export type EvaluationCase = Dto<'EvaluationCase'>;
export type RetrievalEvaluationCase = Dto<'RetrievalEvaluationCase'>;
export type RetrievalEvaluationRun = Omit<Dto<'RetrievalEvaluationDto'>, 'job'> & { job: Job };
export type RetrievalEvaluationSummary = Dto<'RetrievalSummaryDto'> & {
  rewrite: Dto<'RetrievalLatencyDto'>;
  embed: Dto<'RetrievalLatencyDto'>;
  search: Dto<'RetrievalLatencyDto'>;
  rerank: Dto<'RetrievalLatencyDto'>;
  total: Dto<'RetrievalLatencyDto'>;
};
export type RetrievalReport = Omit<Dto<'RetrievalReportDto'>, 'run' | 'summary' | 'results'> & {
  run: RetrievalEvaluationRun;
  summary: RetrievalEvaluationSummary[];
  results: Dto<'RetrievalMetricDto'>[];
};
export type EvaluationVariant = ModelReference<'EvaluationVariant'>;
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
export type Run = Omit<ModelReference<'RunDto'>, 'timing'> &
  Pick<components['schemas']['RunDto'], 'timing'>;
export type RunEvent = Dto<'RunEventDto'>;
export type CreateRun = Omit<Dto<'CreateRunRequest'>, 'webSearch'> &
  Pick<components['schemas']['CreateRunRequest'], 'webSearch'>;

export type Charge = Dto<'ChargeDto'>;
export type MoneyTotal = Dto<'MoneyTotalDto'>;
export type SpendBucket = Dto<'SpendBucketDto'>;
export type SpendUser = Dto<'SpendUserDto'>;
export type SpendReport = Dto<'SpendReportDto'> & {
  totals: MoneyTotal[];
  daily: SpendBucket[];
  models: SpendBucket[];
  users: SpendUser[];
};
export type ConversationSpend = Dto<'ConversationSpendDto'> & {
  totals: MoneyTotal[];
  models: SpendBucket[];
};
export type ModelPrice = ModelReference<'PriceDto'>;
export type PriceTarget = Dto<'PriceTargetDto'>;
export type PriceRequest = Dto<'PriceRequest'>;
export type Dashboard = Omit<Dto<'DashboardDto'>, 'tokens'> & { tokens?: TokenUsage | null } & {
  counts: Dto<'DashboardCountsDto'>;
  spend: SpendReport;
  recent: Dto<'RecentWorkDto'>[];
};
export type WebSearchStatus = Dto<'WebSearchStatusDto'>;
export type RepositoryStatus = Dto<'RepositoryStatusDto'>;
export type Repository = Dto<'RepositoryDto'>;
export type RepositoryPage = Dto<'RepositoryPageDto'> & { items: Repository[] };
export type RepositoryTree = Dto<'RepositoryTreeDto'> & { entries: Dto<'RepositoryEntryDto'>[] };
export type RepositoryFile = Dto<'RepositoryFileDto'>;
export type RepositoryIssue = Dto<'RepositoryIssueDto'>;
export type AuthSession = Omit<Dto<'AuthSessionDto'>, 'methods' | 'method' | 'userId' | 'testing'> &
  Pick<components['schemas']['AuthSessionDto'], 'methods' | 'method' | 'userId' | 'testing'>;
export type ModelPolicy = Dto<'ModelPolicyDto'>;
export type ContextUsage = Omit<
  Dto<'ContextUsageDto'>,
  'reservedKnowledgeTokens' | 'reservedWebSearchTokens'
> &
  Pick<
    components['schemas']['ContextUsageDto'],
    'reservedKnowledgeTokens' | 'reservedWebSearchTokens'
  >;
export type ContextPreview = Omit<Dto<'ContextPreviewRequest'>, 'webSearch'> &
  Pick<components['schemas']['ContextPreviewRequest'], 'webSearch'>;
export type Attachment = Dto<'AttachmentDto'>;
export type AttachmentPolicy = Dto<'AttachmentPolicyDto'>;
export type AttachmentStorage = Dto<'AttachmentStorageDto'>;
export type RunTiming = Dto<'RunTimingDto'>;
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
export type EmbeddingProfile = Omit<Dto<'EmbeddingProfileDto'>, 'coverage' | 'job'> & {
  coverage: Dto<'EmbeddingCoverageDto'>;
  job: Job | null;
};
export type RetrievalCapabilities = Omit<
  Dto<'RetrievalCapabilitiesDto'>,
  'embedding' | 'rerank'
> & {
  embedding: Dto<'RetrievalConnectionDto'>;
  rerank: Dto<'RetrievalConnectionDto'>;
};
export type ArtifactSummary = Dto<'ArtifactSummaryDto'> & { resource: Resource };
export type ArtifactDocument = Dto<'ArtifactDto'> & { resource: Resource };
export type ArtifactRevision = Dto<'ArtifactRevisionDto'>;
export type TransformResult = Dto<'TransformTextDto'>;
export const isActive = (state: string) => state === 'queued' || state === 'running';

export type TextDocument = Dto<'TextDocumentDto'>;
export type RepositoryCommit = Dto<'RepositoryCommitDto'>;
export type RepositoryReview = ModelReference<'RepositoryReviewDto'> & { job: Job };
export type RepositoryReviewDetail = Dto<'RepositoryReviewDetailDto'> & {
  review: RepositoryReview;
  sections: Dto<'RepositoryReviewSectionDto'>[];
};

export type TokenUsage = Dto<'TokenUsageDto'> & { daily: ModelReference<'TokenDayDto'>[] };
