namespace Xtraktor.Tests;

/// <summary>Runs against Fixtures/sample.pdf, compiled from Fixtures/sample.typ.</summary>
public class PdfBookReaderTests
{
	static readonly Book Story = PdfBookReader.Read(TestBooks.Fixture("sample.pdf")).SliceStory();

	[Fact]
	public void Read_RebuildsTheStoryStructure()
	{
		Assert.Equal(
		[
			"Heading:PROLOGUE",
			"Paragraph:Le train entra en gare avec vingt minutes de retard, sous une pluie fine qui brouillait les lumières du quai et les silhouettes pressées des voyageurs.",
			"Paragraph:— Tu as pensé à prendre les billets ? demanda quelqu’un.",
			"Paragraph:Ils passèrent la soirée à jouer aux _Petits Chevaux_ sur la table de la cuisine, pendant que la radio diffusait une vieille chanson.",
			"Heading:CHAPITRE 1",
			"Paragraph:Le facteur était passé tôt, comme chaque mardi, et le courrier attendait sur la table. Pourquoi avait-elle l’impression que cette lettre-là ne lui apportait rien de bon ?",
			"SceneBreak:",
			"Paragraph:Le lendemain, elle se rendit à la bibliothèque de bonne heure, bien décidée à terminer ses recherches avant midi.",
			"Blank:",
			"Paragraph:Pour toujours",
			"Paragraph:Elle resta un long moment sans bouger.",
			"Heading:ÉPILOGUE",
			"Paragraph:Le jardinier rangea ses outils et referma la grille derrière lui.",
			"MidTitle:FIN",
		], TestBooks.Shape(Story));
	}

	[Fact]
	public void Read_JoinsALineEndingWithAHyphenWithoutASpace()
	{
		Assert.Contains(Story.Blocks, b => b.Text.Contains("avait-elle"));
	}

	[Fact]
	public void Read_IgnoresPageNumbers()
	{
		Assert.DoesNotContain(Story.Blocks, b => b.Text.Trim() is "1" or "2" or "3" or "4");
	}

	[Fact]
	public void Read_RecordsThePageOfEachBlock()
	{
		Assert.Equal("p. 2", Story.Blocks[0].Location);
		Assert.Equal("p. 3", Story.Blocks.Single(b => b.Kind == BlockKind.SceneBreak).Location);
	}
}
