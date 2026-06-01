namespace ChatBot.Infrastructure.Services;

/// <summary>
/// Нарезка длинного текста на перекрывающиеся чанки фиксированной длины.
///
/// Использует символы как прокси для токенов (~1 токен ≈ 4 символа для английского,
/// ~2 символа для русского). Точный токенайзер (tiktoken) не нужен для демо —
/// важно лишь не превысить контекст embedding-модели (8192 токенов у text-embedding-3-small).
///
/// .NET: статический хелпер, как <c>string.Split</c>, но с overlap для сохранения контекста
/// между чанками (последние N символов одного чанка дублируются в начале следующего).
/// </summary>
public static class TextChunker
{
    /// <summary>
    /// Разбивает текст на чанки по границам предложений, не превышая <paramref name="maxChunkSize"/>.
    /// Перекрытие <paramref name="overlap"/> сохраняет контекст между чанками для RAG-поиска.
    /// </summary>
    /// <param name="text">Исходный текст (например, страница PDF).</param>
    /// <param name="maxChunkSize">Максимум символов в чанке.</param>
    /// <param name="overlap">Сколько символов из конца предыдущего чанка дублировать в начале следующего.</param>
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

            // Стараемся резать по границе предложения (точка / перенос строки),
            // чтобы чанк не обрывался на середине слова — это улучшает качество эмбеддингов.
            if (end < normalized.Length)
            {
                var lastBreak = FindLastSentenceBoundary(normalized, start, end);
                if (lastBreak > start + maxChunkSize / 2) // не делать слишком короткие чанки
                    end = lastBreak;
            }

            chunks.Add(normalized[start..end].Trim());

            if (end >= normalized.Length) break;
            start = end - overlap; // следующий чанк стартует с перекрытием
        }

        return chunks;
    }

    /// <summary>
    /// Ищет последнюю границу предложения (.!? или \n) в диапазоне [from..to).
    /// Возвращает позицию ПОСЛЕ разделителя, либо <paramref name="to"/> если не найдено.
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
