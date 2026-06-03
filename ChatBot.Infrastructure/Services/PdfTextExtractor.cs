using UglyToad.PdfPig;

namespace ChatBot.Infrastructure.Services;

/// <summary>
/// Page-by-page text extraction from PDF via PdfPig.
///
/// .NET: PdfPig is a managed PDF wrapper (no native dependencies,
/// unlike iTextSharp/PDFsharp). Suitable for text-based PDFs; scanned documents
/// require OCR (Tesseract), which is out of scope for this demo.
/// </summary>
public static class PdfTextExtractor
{
    public record PageText(int PageNumber, string Text);

    /// <summary>Reads a PDF from a stream and returns the text of each page.</summary>
    public static IReadOnlyList<PageText> ExtractPages(Stream pdfStream)
    {
        ArgumentNullException.ThrowIfNull(pdfStream);

        // PdfDocument.Open accepts a Stream — no need to save to a temporary file.
        using var document = PdfDocument.Open(pdfStream);

        var pages = new List<PageText>(document.NumberOfPages);
        foreach (var page in document.GetPages())
            pages.Add(new PageText(page.Number, page.Text));

        return pages;
    }
}
