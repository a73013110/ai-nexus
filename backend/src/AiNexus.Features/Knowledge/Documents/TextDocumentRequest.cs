using AiNexus.Platform.Validation;
namespace AiNexus.Features.Knowledge.Documents;

[ValidatedInHandler("Checked only after the collection or document access check (and, for an edit, the text version check); TextDocuments.Clean keeps the codes and order.")]
public sealed record TextDocumentRequest(string Title, string Text, int? ExpectedVersion = null);
