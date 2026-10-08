using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using static Xtraktor.Tests.TestBooks;

namespace Xtraktor.Tests;

public class DocxTests
{
	static readonly Book Sample = Book(
		H("Prologue"),
		P(new Run("Il lisait "), new Run("Le Comte de Monte-Cristo, ", Italic: true), new Run("comme chaque soir.")),
		P("—\u00A0Encore\u00A0?"),
		Scene(),
		Blank(),
		P("Pour toujours."),
		H("Épilogue"),
		P("Fin de l’histoire."),
		Fin());

	[Fact]
	public void WriteThenRead_RoundTripsTheBook()
	{
		using var tmp = new TempDir();
		var path = tmp.File("book.docx");

		DocxWriter.Write(Sample, path);
		var read = DocxBookReader.Read(path);

		Assert.Equal(
			Sample.Blocks.Select(b => (b.Kind, Runs: string.Join("|", b.Runs))),
			read.Blocks.Select(b => (b.Kind, Runs: string.Join("|", b.Runs))));
		Assert.Empty(BookDiff.Compare(Sample, read));
	}

	[Fact]
	public void Write_UsesTheStylesOfTheManuscript()
	{
		using var tmp = new TempDir();
		var path = tmp.File("book.docx");
		DocxWriter.Write(Sample, path);

		using var doc = WordprocessingDocument.Open(path, false);
		var styles = doc.MainDocumentPart!.StyleDefinitionsPart!.Styles!.Elements<Style>().ToDictionary(s => s.StyleId!.Value!);
		var used = doc.MainDocumentPart.Document!.Body!.Elements<Paragraph>()
			.Select(p => p.ParagraphProperties?.ParagraphStyleId?.Val?.Value).ToList();

		Assert.Equal(["Titre1", "Normal", "Normal", "Ellipse", "Normal", "Normal", "Titre1", "Normal", "Titre"], used);
		Assert.Equal("heading 1", styles["Titre1"].StyleName!.Val!.Value);
		Assert.Equal("Title", styles["Titre"].StyleName!.Val!.Value);
		Assert.NotNull(styles["Titre1"].StyleParagraphProperties!.PageBreakBefore);
	}

	[Fact]
	public void Write_LaysOutA4PagesWith25cmMargins()
	{
		using var tmp = new TempDir();
		var path = tmp.File("book.docx");
		DocxWriter.Write(Sample, path);

		using var doc = WordprocessingDocument.Open(path, false);
		var section = doc.MainDocumentPart!.Document!.Body!.Elements<SectionProperties>().Single();
		var size = section.GetFirstChild<PageSize>()!;
		var margin = section.GetFirstChild<PageMargin>()!;

		Assert.Equal((11906u, 16838u), (size.Width!.Value, size.Height!.Value));
		Assert.Equal([1417, 1417, 1417, 1417], new[] { margin.Top!.Value, margin.Bottom!.Value, (int)margin.Left!.Value, (int)margin.Right!.Value });
	}

	[Fact]
	public void Write_SceneStyleCanBeRenamed()
	{
		using var tmp = new TempDir();
		var path = tmp.File("book.docx");
		DocxWriter.Write(Sample, path, sceneStyle: "Elipse");

		var read = DocxBookReader.Read(path, sceneStyle: "Elipse");

		Assert.Single(read.Blocks, b => b.Kind == BlockKind.SceneBreak);
	}
}
