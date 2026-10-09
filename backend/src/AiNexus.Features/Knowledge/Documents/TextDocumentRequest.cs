namespace AiNexus.Features.Knowledge.Documents;

// The body is checked only after the collection or document access check (and, for an edit, after the text version
// check), so this request has no validator; TextDocuments.Clean keeps the established codes and order.
public sealed record TextDocumentRequest(string Title, string Text, int? ExpectedVersion = null);
