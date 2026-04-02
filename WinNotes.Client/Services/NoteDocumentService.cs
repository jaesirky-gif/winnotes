using System.IO;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;

namespace WinNotes.Client.Services;

public static class NoteDocumentService
{
    public static FlowDocument CreateBlankDocument()
    {
        var document = CreateBaseDocument();
        document.Blocks.Add(new Paragraph(new Run(string.Empty)));
        return document;
    }

    public static FlowDocument CreatePlainDocument(string text)
    {
        var document = CreateBaseDocument();
        var paragraphs = text.Replace("\r\n", "\n").Split("\n\n", StringSplitOptions.None);

        foreach (var paragraphText in paragraphs)
        {
            var paragraph = new Paragraph();
            var lines = paragraphText.Split('\n');

            for (var index = 0; index < lines.Length; index++)
            {
                if (index > 0)
                {
                    paragraph.Inlines.Add(new LineBreak());
                }

                paragraph.Inlines.Add(new Run(lines[index]));
            }

            document.Blocks.Add(paragraph);
        }

        if (document.Blocks.Count == 0)
        {
            document.Blocks.Add(new Paragraph(new Run(string.Empty)));
        }

        return document;
    }

    public static string CreateDocumentPayloadFromPlainText(string text)
    {
        return Serialize(CreatePlainDocument(text));
    }

    public static string Serialize(FlowDocument document)
    {
        var clone = XamlReader.Parse(XamlWriter.Save(document)) as FlowDocument ?? CreateBlankDocument();
        ApplyDocumentDefaults(clone);

        using var stream = new MemoryStream();
        var range = new TextRange(clone.ContentStart, clone.ContentEnd);
        range.Save(stream, DataFormats.XamlPackage);
        return Convert.ToBase64String(stream.ToArray());
    }

    public static FlowDocument Deserialize(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return CreateBlankDocument();
        }

        try
        {
            var bytes = Convert.FromBase64String(payload);
            using var stream = new MemoryStream(bytes);
            var document = CreateBaseDocument();
            var range = new TextRange(document.ContentStart, document.ContentEnd);
            range.Load(stream, DataFormats.XamlPackage);
            ApplyDocumentDefaults(document);

            if (document.Blocks.Count == 0)
            {
                document.Blocks.Add(new Paragraph(new Run(string.Empty)));
            }

            return document;
        }
        catch
        {
            return CreateBlankDocument();
        }
    }

    public static string ExtractPlainText(FlowDocument document)
    {
        var text = new TextRange(document.ContentStart, document.ContentEnd).Text;
        return text.Replace("\r\n", "\n").Trim();
    }

    public static void ApplyDocumentDefaults(FlowDocument document)
    {
        document.FontFamily = new FontFamily("Georgia");
        document.FontSize = 18;
        document.LineHeight = 28;
        document.TextAlignment = TextAlignment.Left;
        document.PagePadding = new Thickness(26, 20, 26, 26);
        document.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2F261B"));

        foreach (var block in document.Blocks)
        {
            if (block is Paragraph paragraph)
            {
                paragraph.Margin = new Thickness(0, 8, 0, 8);
            }
        }
    }

    private static FlowDocument CreateBaseDocument()
    {
        return new FlowDocument();
    }
}
