using Microsoft.Data.SqlTypes;
using System.Runtime.InteropServices;

namespace AiNexus.Features.Knowledge.Embeddings;

public static class VectorBytes
{
    public static byte[] Write(SqlVector<float> vector) => MemoryMarshal.AsBytes(vector.Memory.Span).ToArray();
    public static SqlVector<float> Read(byte[] value) => new(MemoryMarshal.Cast<byte, float>(value).ToArray());
}
