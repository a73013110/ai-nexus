namespace AiNexus.Features.Persistence.ReleaseScripts;

/// <summary>Which DBA handles a release SQL file: structure (DDL) under schema/, rows (DML) under data/.</summary>
public enum ReleaseScriptKind
{
    Schema,
    Data,
}
