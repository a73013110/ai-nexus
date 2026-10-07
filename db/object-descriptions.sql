-- Re-runnable descriptions for schema, physical indexes/constraints and non-EF columns.
-- Table/column MS_Description is maintained by the EF model and its migrations.
SET NOCOUNT ON;
DECLARE @nxDescschema sysname, @nxDesctable sysname, @nxDescname sysname, @nxDesckind varchar(20), @nxDescdescription nvarchar(3750);
DECLARE schema_descriptions CURSOR LOCAL FAST_FORWARD FOR
SELECT name, description FROM (VALUES
 (N'identity', N'使用者身分、登入政策、密碼雜湊與個人偏好。'),
 (N'access', N'角色、功能群組、功能授權及模型政策。'),
 (N'conversations', N'私人對話、訊息分支與分類。'),
 (N'inference', N'模型設定、生成、租約、重播、用量與費用。'),
 (N'operations', N'遮罩後診斷日誌、獨立管理稽核、通知、背景工作及持久進度；診斷與稽核採不同保留政策。'),
 (N'attachments', N'站外原檔的 metadata、儲存識別、權限與引用關聯；不含原檔 bytes。'),
 (N'library', N'使用者私人提示詞範本。'),
 (N'collaboration', N'資料資源 ACL 與具名分享。'),
 (N'knowledge', N'知識文件、分頁、片段、向量與引用。'),
 (N'content', N'成果文件版本及外部來源參照。'),
 (N'projects', N'專案與共用指令範本。'),
 (N'quality', N'回答回饋、固定評測及人工覆核。'),
 (N'workspace', N'個人程式庫連線、固定版本 review 與匯入識別。'),
 (N'dbo', N'EF 資料庫版本記錄。')
) descriptions(name, description) WHERE SCHEMA_ID(name) IS NOT NULL;
OPEN schema_descriptions;
FETCH NEXT FROM schema_descriptions INTO @nxDescschema, @nxDescdescription;
WHILE @@FETCH_STATUS = 0
BEGIN
 IF EXISTS (SELECT 1 FROM sys.extended_properties WHERE class = 3 AND major_id = SCHEMA_ID(@nxDescschema) AND name = N'MS_Description')
  EXEC sys.sp_updateextendedproperty @name=N'MS_Description', @value=@nxDescdescription, @level0type=N'SCHEMA', @level0name=@nxDescschema;
 ELSE EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=@nxDescdescription, @level0type=N'SCHEMA', @level0name=@nxDescschema;
 FETCH NEXT FROM schema_descriptions INTO @nxDescschema, @nxDescdescription;
END;
CLOSE schema_descriptions; DEALLOCATE schema_descriptions;

DECLARE object_descriptions CURSOR LOCAL FAST_FORWARD FOR
SELECT s.name, t.name, 'INDEX', i.name,
 LEFT(CONCAT(CASE WHEN i.is_unique = 1 THEN N'唯一索引；避免重複組合：' ELSE N'查詢索引；加速依下列欄位篩選及排序：' END,
  STRING_AGG(CONVERT(nvarchar(max), c.name + CASE WHEN ic.is_descending_key = 1 THEN N' DESC' ELSE N'' END), N'、') WITHIN GROUP (ORDER BY ic.key_ordinal),
  CASE WHEN i.has_filter = 1 THEN N'；篩選條件：' + i.filter_definition ELSE N'' END), 3750) COLLATE DATABASE_DEFAULT
FROM sys.indexes i JOIN sys.tables t ON i.object_id=t.object_id JOIN sys.schemas s ON t.schema_id=s.schema_id
JOIN sys.index_columns ic ON i.object_id=ic.object_id AND i.index_id=ic.index_id AND ic.key_ordinal > 0
JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
WHERE s.name IN (N'identity',N'access',N'conversations',N'inference',N'operations',N'attachments',N'library',N'collaboration',N'knowledge',N'content',N'projects',N'quality',N'workspace',N'dbo')
 AND i.is_primary_key=0 AND i.is_unique_constraint=0
GROUP BY s.name,t.name,i.name,i.is_unique,i.has_filter,i.filter_definition
UNION ALL
SELECT s.name,t.name,'CONSTRAINT',k.name,CONCAT(CASE WHEN k.type='PK' THEN N'主鍵；唯一識別 ' ELSE N'唯一約束；防止重複 ' END,s.name,N'.',t.name,N' 的資料。') COLLATE DATABASE_DEFAULT
FROM sys.key_constraints k JOIN sys.tables t ON k.parent_object_id=t.object_id JOIN sys.schemas s ON t.schema_id=s.schema_id
WHERE s.name IN (N'identity',N'access',N'conversations',N'inference',N'operations',N'attachments',N'library',N'collaboration',N'knowledge',N'content',N'projects',N'quality',N'workspace',N'dbo')
UNION ALL
SELECT s.name,t.name,'CONSTRAINT',f.name,LEFT(CONCAT(N'外鍵；關聯 ',OBJECT_SCHEMA_NAME(f.referenced_object_id),N'.',OBJECT_NAME(f.referenced_object_id),N'；刪除策略：',f.delete_referential_action_desc,N'；更新策略：',f.update_referential_action_desc),3750) COLLATE DATABASE_DEFAULT
FROM sys.foreign_keys f JOIN sys.tables t ON f.parent_object_id=t.object_id JOIN sys.schemas s ON t.schema_id=s.schema_id
WHERE s.name IN (N'identity',N'access',N'conversations',N'inference',N'operations',N'attachments',N'library',N'collaboration',N'knowledge',N'content',N'projects',N'quality',N'workspace')
UNION ALL
SELECT s.name,t.name,'CONSTRAINT',d.name,LEFT(CONCAT(N'預設約束；欄位 ',COL_NAME(d.parent_object_id,d.parent_column_id),N' 未指定值時使用 ',d.definition),3750) COLLATE DATABASE_DEFAULT
FROM sys.default_constraints d JOIN sys.tables t ON d.parent_object_id=t.object_id JOIN sys.schemas s ON t.schema_id=s.schema_id
WHERE s.name IN (N'identity',N'access',N'conversations',N'inference',N'operations',N'attachments',N'library',N'collaboration',N'knowledge',N'content',N'projects',N'quality',N'workspace')
UNION ALL
SELECT s.name,t.name,'CONSTRAINT',c.name,LEFT(CONCAT(N'檢核約束；資料需符合 ',c.definition),3750) COLLATE DATABASE_DEFAULT
FROM sys.check_constraints c JOIN sys.tables t ON c.parent_object_id=t.object_id JOIN sys.schemas s ON t.schema_id=s.schema_id
WHERE s.name IN (N'identity',N'access',N'conversations',N'inference',N'operations',N'attachments',N'library',N'collaboration',N'knowledge',N'content',N'projects',N'quality',N'workspace')
UNION ALL
SELECT s.name,t.name,'COLUMN',c.name,
 CASE WHEN c.name LIKE N'%1024%' THEN N'原生 VECTOR(1024) embedding；SQL Server 2025 支援時建立，須先套用來源 ACL 與相容 profile。'
 ELSE N'原生 VECTOR(768) embedding；SQL Server 2025 支援時建立，須先套用來源 ACL 與相容 profile。' END COLLATE DATABASE_DEFAULT
FROM sys.columns c JOIN sys.tables t ON c.object_id=t.object_id JOIN sys.schemas s ON t.schema_id=s.schema_id
WHERE s.name=N'knowledge' AND t.name=N'Chunks' AND c.name LIKE N'EmbeddingVector%'
UNION ALL
SELECT N'dbo',N'__EFMigrationsHistory',NULL,NULL,N'EF 已套用版本記錄；不可手動刪除以重跑 migration。' WHERE OBJECT_ID(N'dbo.__EFMigrationsHistory') IS NOT NULL
UNION ALL
SELECT N'dbo',N'__EFMigrationsHistory','COLUMN',N'MigrationId',N'已套用的 migration 版本識別碼。' WHERE OBJECT_ID(N'dbo.__EFMigrationsHistory') IS NOT NULL
UNION ALL
SELECT N'dbo',N'__EFMigrationsHistory','COLUMN',N'ProductVersion',N'套用 migration 的 Entity Framework Core 版本。' WHERE OBJECT_ID(N'dbo.__EFMigrationsHistory') IS NOT NULL;
OPEN object_descriptions;
FETCH NEXT FROM object_descriptions INTO @nxDescschema,@nxDesctable,@nxDesckind,@nxDescname,@nxDescdescription;
WHILE @@FETCH_STATUS = 0
BEGIN
 IF EXISTS (SELECT 1 FROM sys.fn_listextendedproperty(N'MS_Description',N'SCHEMA',@nxDescschema,N'TABLE',@nxDesctable,@nxDesckind,@nxDescname))
  EXEC sys.sp_updateextendedproperty @name=N'MS_Description',@value=@nxDescdescription,@level0type=N'SCHEMA',@level0name=@nxDescschema,@level1type=N'TABLE',@level1name=@nxDesctable,@level2type=@nxDesckind,@level2name=@nxDescname;
 ELSE EXEC sys.sp_addextendedproperty @name=N'MS_Description',@value=@nxDescdescription,@level0type=N'SCHEMA',@level0name=@nxDescschema,@level1type=N'TABLE',@level1name=@nxDesctable,@level2type=@nxDesckind,@level2name=@nxDescname;
 FETCH NEXT FROM object_descriptions INTO @nxDescschema,@nxDesctable,@nxDesckind,@nxDescname,@nxDescdescription;
END;
CLOSE object_descriptions; DEALLOCATE object_descriptions;
