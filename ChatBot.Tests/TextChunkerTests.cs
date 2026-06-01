using ChatBot.Infrastructure.Services;
using Xunit;

namespace ChatBot.Tests;

/// <summary>
/// Юнит-тесты чанкера — без сети, без БД, чисто детерминированная логика.
/// </summary>
public class TextChunkerTests
{
    [Fact]
    public void Chunk_EmptyText_ReturnsEmpty()
    {
        Assert.Empty(TextChunker.Chunk(""));
        Assert.Empty(TextChunker.Chunk("   \n  "));
    }

    [Fact]
    public void Chunk_ShortText_ReturnsSingleChunk()
    {
        var result = TextChunker.Chunk("Короткий текст помещается целиком.", maxChunkSize: 1500);
        Assert.Single(result);
        Assert.Equal("Короткий текст помещается целиком.", result[0]);
    }

    [Fact]
    public void Chunk_LongText_SplitsWithOverlap()
    {
        // 1000 символов одним предложением, чанк 300, overlap 50 → должно быть >= 4 чанков
        var text = new string('a', 1000);
        var chunks = TextChunker.Chunk(text, maxChunkSize: 300, overlap: 50);

        Assert.True(chunks.Count >= 4, $"Ожидалось >= 4 чанков, получено {chunks.Count}");
        Assert.All(chunks, c => Assert.True(c.Length <= 300));
    }

    [Fact]
    public void Chunk_PrefersSentenceBoundary()
    {
        var sentence1 = new string('a', 200) + ".";
        var sentence2 = new string('b', 200) + ".";
        var text = sentence1 + " " + sentence2;

        var chunks = TextChunker.Chunk(text, maxChunkSize: 250, overlap: 20);

        // Первый чанк должен заканчиваться точкой первого предложения, а не на середине второго
        Assert.EndsWith(".", chunks[0]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Chunk_InvalidChunkSize_Throws(int size)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TextChunker.Chunk("text", maxChunkSize: size));
    }
}
