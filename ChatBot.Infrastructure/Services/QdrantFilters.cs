using Qdrant.Client.Grpc;

namespace ChatBot.Infrastructure.Services;

/// <summary>
/// Helpers for building Qdrant gRPC filters.
/// Extracted to avoid duplicating boilerplate in Ingestion and Rag services.
/// </summary>
internal static class QdrantFilters
{
    /// <summary>Exact match on a string payload field.</summary>
    public static Condition MatchKeyword(string key, string value) => new()
    {
        Field = new FieldCondition { Key = key, Match = new Match { Keyword = value } }
    };

    /// <summary>
    /// Access filter for the current session: <c>scope = shared</c> OR
    /// (<c>scope = private</c> AND <c>sessionId = X</c>).
    /// If sessionId is absent — returns shared only.
    /// </summary>
    public static Filter ForSession(string? sessionId)
    {
        var filter = new Filter();

        // shared branch
        filter.Should.Add(new Condition { Filter = new Filter
        {
            Must = { MatchKeyword("scope", "shared") }
        } });

        // private branch — only when sessionId is present
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

    /// <summary>Filter targeting a specific private document of a specific session (used for deletion).</summary>
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
