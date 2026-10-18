-- Run only in a DBA-selected SOURCE database, never through AI Nexus migrations.
-- This scaffold intentionally returns no rows. Replace both WHERE 1=0 views with
-- source-authoritative row ACLs after validating full AD SID/account mappings.
-- A menu/function grant alone is not a document/student-row grant.
IF SCHEMA_ID(N'ainexus') IS NULL EXEC(N'CREATE SCHEMA ainexus AUTHORIZATION dbo');
GO
CREATE OR ALTER VIEW ainexus.AuthorizedRecords AS
SELECT
  CAST(NULL AS nvarchar(184)) AS ActorSid,
  CAST(NULL AS nvarchar(256)) AS ActorAccount,
  CAST(NULL AS nvarchar(160)) AS RecordId,
  CAST(NULL AS nvarchar(32)) AS RecordKind,
  CAST(NULL AS nvarchar(120)) AS Title,
  CAST(NULL AS nvarchar(80)) AS Status,
  CAST(NULL AS nvarchar(160)) AS Revision,
  CAST(NULL AS datetimeoffset(7)) AS ModifiedAt,
  CAST(NULL AS nvarchar(max)) AS Body
WHERE 1=0;
GO
CREATE OR ALTER VIEW ainexus.AuthorizedRecordHistory AS
SELECT
  CAST(NULL AS nvarchar(184)) AS ActorSid,
  CAST(NULL AS nvarchar(256)) AS ActorAccount,
  CAST(NULL AS nvarchar(160)) AS RecordId,
  CAST(NULL AS bigint) AS EventId,
  CAST(NULL AS datetimeoffset(7)) AS At,
  CAST(NULL AS nvarchar(32)) AS Kind,
  CAST(NULL AS nvarchar(120)) AS Actor,
  CAST(NULL AS nvarchar(2000)) AS Description,
  CAST(NULL AS nvarchar(160)) AS Revision
WHERE 1=0;
GO
IF DATABASE_PRINCIPAL_ID(N'ainexus_reader') IS NULL CREATE ROLE ainexus_reader;
GRANT SELECT ON OBJECT::ainexus.AuthorizedRecords TO ainexus_reader;
GRANT SELECT ON OBJECT::ainexus.AuthorizedRecordHistory TO ainexus_reader;
-- DBA creates a dedicated SQL user/login and adds only that user to ainexus_reader.
-- Do not grant db_datareader, source-wide SELECT, writes, or original signing procedures.
