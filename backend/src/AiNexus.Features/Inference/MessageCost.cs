using System.Text;

namespace AiNexus.Features.Inference;

/// <summary>Conservative token cost of provider messages: UTF-8 bytes plus framing, and each image at its estimated tokens.</summary>
public static class MessageCost
{
    public static long Estimate(IReadOnlyList<InferenceMessage> messages) => messages.Sum(Of) + 128;
    public static long Of(InferenceMessage message) => Of(message.Content) + (message.Images ?? []).Sum(x => (long)x.EstimatedTokens);
    public static int Of(string value) => Encoding.UTF8.GetByteCount(value) + 32;
}
