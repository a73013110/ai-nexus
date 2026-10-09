namespace AiNexus.Platform.Diagnostics;

// Internal admission receipt; never serialized and never supplied by a client.
public sealed class DiagnosticDelivery { public bool Accepted { get; internal set; } }
