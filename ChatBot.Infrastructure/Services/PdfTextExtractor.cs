using UglyToad.PdfPig;

namespace ChatBot.Infrastructure.Services;

/// <summary>
/// Извлечение текста из PDF постранично через PdfPig.
///
/// .NET: PdfPig — managed-обёртка над PDF-форматом (нет нативных зависимостей,
/// в отличие от iTextSharp/PDFsharp). Подходит для текстовых PDF; для сканов
/// нужен OCR (Tesseract), что выходит за рамки этого демо.
/// </summary>
public static class PdfTextExtractor
{
    public record PageText(int PageNumber, string Text);

    /// <summary>
    /// Читает PDF из потока и возвращает текст каждой страницы.
    /// </summary>
    public static IReadOnlyList<PageText> ExtractPages(Stream pdfStream)
    {
        ArgumentNullException.ThrowIfNull(pdfStream);

        // PdfDocument.Open поддерживает Stream — не нужно сохранять во временный файл
        using var document = PdfDocument.Open(pdfStream);

        var pages = new List<PageText>(document.NumberOfPages);
        foreach (var page in document.GetPages())
            pages.Add(new PageText(page.Number, page.Text));

        return pages;
    }
}
