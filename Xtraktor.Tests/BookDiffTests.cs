using static Xtraktor.Tests.TestBooks;

namespace Xtraktor.Tests;

public class BookDiffTests
{
	static List<BlockDiff> Diff(Book a, Book b) => BookDiff.Compare(a, b);

	[Fact]
	public void Compare_IdenticalBooks_HasNoDifference()
	{
		Assert.Empty(Diff(Book(H("Prologue"), P("Texte."), Scene()), Book(H("Prologue"), P("Texte."), Scene())));
	}

	[Fact]
	public void Compare_KindOfSpaceOnly_IsTypography()
	{
		var d = Assert.Single(Diff(Book(P("Ça va ?")), Book(P("Ça va\u00A0?"))));

		Assert.Equal(DiffKind.Typography, d.Kind);
		Assert.Contains("espace insécable", BookDiff.DescribeTypography(d.A!, d.B!));
	}

	[Fact]
	public void Compare_HeadingCaseOnly_IsTypography()
	{
		var d = Assert.Single(Diff(Book(H("CHAPITRE 1")), Book(H("Chapitre 1"))));

		Assert.Equal(DiffKind.Typography, d.Kind);
		Assert.Equal("casse", BookDiff.DescribeTypography(d.A!, d.B!));
	}

	[Fact]
	public void Compare_ParagraphCaseChange_IsText()
	{
		Assert.Equal(DiffKind.Text, Assert.Single(Diff(Book(P("Bonjour.")), Book(P("BONJOUR.")))).Kind);
	}

	[Fact]
	public void Compare_ItalicChange_IsStyle()
	{
		var a = Book(P(new Run("Il lit "), new Run("Le Rouge", Italic: true), new Run(".")));
		var b = Book(P("Il lit Le Rouge."));

		Assert.Equal(DiffKind.Style, Assert.Single(Diff(a, b)).Kind);
	}

	[Fact]
	public void Compare_SpaceInsideOrOutsideItalic_IsNotADifference()
	{
		var a = Book(P(new Run("fantôme, ", Italic: true), new Run("dit-elle")));
		var b = Book(P(new Run("fantôme,", Italic: true), new Run(" dit-elle")));

		Assert.Empty(Diff(a, b));
	}

	[Fact]
	public void Compare_ChangedWord_IsTextWithWordLevelDiff()
	{
		var d = Assert.Single(Diff(Book(P("Elle partit tôt.")), Book(P("Elle partit tard."))));

		Assert.Equal(DiffKind.Text, d.Kind);
		Assert.Equal([("Elle partit ", 0), ("tôt", -1), ("tard", 1), (".", 0)], BookDiff.WordDiff(d.A!.Text, d.B!.Text));
	}

	[Fact]
	public void Compare_MissingParagraph_IsReportedOnce_AndRestStaysAligned()
	{
		var a = Book(H("Prologue"), P("Un."), P("Deux."), P("Trois."));
		var b = Book(H("Prologue"), P("Un."), P("Trois."));

		var d = Assert.Single(Diff(a, b));
		Assert.Equal((DiffKind.Text, "Deux.", (Block?)null), (d.Kind, d.A!.Text, d.B));
		Assert.Equal("Prologue", d.Chapter);
	}

	[Fact]
	public void Compare_BlankLineOnOneSide_IsBlank()
	{
		var d = Assert.Single(Diff(Book(P("Un."), P("Deux.")), Book(P("Un."), Blank(), P("Deux."))));

		Assert.Equal(DiffKind.Blank, d.Kind);
	}

	[Fact]
	public void Html_RendersEveryCategory()
	{
		var a = Book(H("Prologue"), P("Ça va ?"), P("Elle partit le matin."));
		var b = Book(H("Prologue"), P("Ça va\u00A0?"), Blank(), P("Elle partit le soir."));

		var html = BookDiff.Html(a, b, Diff(a, b));

		Assert.Contains("<del>matin</del>", html);
		Assert.Contains("<ins>soir</ins>", html);
		Assert.Contains("<span class=nb>⍽</span>", html);
		Assert.Contains("[ligne vide]", html);
	}
}
