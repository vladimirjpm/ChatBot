using Qdrant.Client.Grpc;

namespace ChatBot.Infrastructure.Services;

/// <summary>
/// Хелперы для построения Qdrant gRPC-фильтров.
/// Вынесено отдельно, чтобы не дублировать boilerplate в Ingestion и Rag сервисах.
/// </summary>
internal static class QdrantFilters
{
    /// <summary>
    /// Точное совпадение строкового payload-поля.
    /// </summary>
    public static Condition MatchKeyword(string key, string value) => new()
    {
        Field = new FieldCondition { Key = key, Match = new Match { Keyword = value } }
    };

    /// <summary>
    /// Фильтр доступа для текущей сессии: <c>scope = shared</c> ИЛИ
    /// (<c>scope = private</c> И <c>sessionId = X</c>).
    /// Если sessionId не задан — возвращает только shared.
    /// </summary>
    public static Filter ForSession(string? sessionId)
    {
        var filter = new Filter();

        // Ветка shared
        filter.Should.Add(new Condition { Filter = new Filter
        {
            Must = { MatchKeyword("scope", "shared") }
        } });

        // Ветка private — только если есть sessionId
        if (!string.IsNullOrEmpty(sessionId))
        {
            filter.Should.Add(new Condition { Filter = new Filter
            {
                Must =
                {
                    MatchKeyword("scope", "private"),
                    MatchKeyword("sessionId", sessionId)
                }
            } });
        }

        return filter;
    }

    /// <summary>
    /// Фильтр на конкретный приватный документ конкретной сессии (для удаления).
    /// </summary>
    public static Filter OwnedPrivateDocument(string documentName, string sessionId) => new()
    {
        Must =
        {
            MatchKeyword("scope", "private"),
            MatchKeyword("sessionId", sessionId),
            MatchKeyword("documentName", documentName),
        }
    };
}
