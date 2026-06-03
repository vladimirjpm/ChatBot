namespace ChatBot.Infrastructure.Services;

/// <summary>
/// Splits long text into overlapping fixed-length chunks.
///
/// Uses characters as a token proxy (~1 token ≈ 4 chars for English,
/// ~2 chars for Russian). An exact tokenizer (tiktoken) is not needed for the demo —
/// what matters is staying within the embedding model's context limit (8192 tokens for text-embedding-3-small).
///
/// .NET: a static helper like <c>string.Split</c>, but with overlap to preserve context
/// between chunks (the last N characters of one chunk are duplicated at the start of the next).
/// </summary>
public static class TextChunker
{
    /// <summary>
    /// Splits text into chunks at sentence boundaries, not exceeding <paramref name="maxChunkSize"/>.
    /// The <paramref name="overlap"/> preserves context between chunks for RAG retrieval.
    /// </summary>
    /// <param name="text">Source text (e.g. a PDF page).</param>
    /// <param name="maxChunkSize">Maximum characters per chunk.</param>
    /// <param name="overlap">How many characters from the end of the previous chunk to duplicate at the start of the next.</param>
    public static IReadOnlyList<string> Chunk(string text, int maxChunkSize = 1500, int overlap = 200)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (maxChunkSize <= 0) throw new ArgumentOutOfRangeException(nameof(maxChunkSize));
        if (overlap < 0 || overlap >= maxChunkSize) throw new ArgumentOutOfRangeException(nameof(overlap));

        var normalized = text.Trim();
        if (normalized.Length == 0) return [];
        if (normalized.Length <= maxChunkSize) return [normalized];

        var chunks = new List<string>();
        var start = 0;

        while (start < normalized.Length)
        {
            var end = Math.Min(start + maxChunkSize, normalized.Length);

            // Try to cut at a sentence boundary (period / newline)
            // so the chunk does not break mid-word — this improves embedding quality.
            if (end < normalized.Length)
            {
                var lastBreak = FindLastSentenceBoundary(normalized, start, end);
                if (lastBreak > start + maxChunkSize / 2) // avoid excessively short chunks
                    end = lastBreak;
            }

            chunks.Add(normalized[start..end].Trim());

            if (end >= normalized.Length) break;
            start = end - overlap; // next chunk starts with overlap
        }

        return chunks;
    }

    /// <summary>
    /// Finds the last sentence boundary (.!? or \n) in the range [from..to).
    /// Returns the position AFTER the delimiter, or <paramref name="to"/> if none found.
    /// </summary>
    private static int FindLastSentenceBoundary(string text, int from, int to)
    {
        for (var i = to - 1; i > from; i--)
        {
            var c = text[i];
            if (c == '.' || c == '!' || c == '?' || c == '\n')
                return i + 1;
        }
        return to;
    }
}
