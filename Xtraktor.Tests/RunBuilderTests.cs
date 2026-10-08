namespace Xtraktor.Tests;

public class RunBuilderTests
{
	[Fact]
	public void Build_CollapsesWhitespaceAndTrims()
	{
		var rb = new RunBuilder();
		rb.Append("  Un \n\t texte   ");
		rb.Append("  suivi.  ");

		Assert.Equal([new Run("Un texte suivi.")], rb.Build());
	}

	[Fact]
	public void Build_NonBreakingSpaceWinsOverPlainSpaceBeforeIt()
	{
		var rb = new RunBuilder();
		rb.Append("ça \u00A0?");

		Assert.Equal("ça\u00A0?", rb.Build().Single().Text);
	}

	[Fact]
	public void Build_KeepsSourceStylingOfSpaces()
	{
		var rb = new RunBuilder();
		rb.Append("fantôme, ", italic: true);
		rb.Append("se dit-elle");

		Assert.Equal([new Run("fantôme, ", Italic: true), new Run("se dit-elle")], rb.Build());
	}

	[Fact]
	public void Canonical_MovesSpacesAtStyleBoundariesOutOfTheStyledRun()
	{
		var a = RunBuilder.Canonical([new Run("fantôme, ", Italic: true), new Run("se dit-elle")]);
		var b = RunBuilder.Canonical([new Run("fantôme,", Italic: true), new Run(" se dit-elle")]);

		Assert.Equal(a, b);
		Assert.Equal([new Run("fantôme,", Italic: true), new Run(" se dit-elle")], a);
	}

	[Fact]
	public void Canonical_MergesAdjacentRunsOfTheSameStyle()
	{
		var runs = RunBuilder.Canonical([new Run("Les ", Italic: true), new Run("Petits", Italic: true), new Run(" Chevaux", Italic: true)]);

		Assert.Equal([new Run("Les Petits Chevaux", Italic: true)], runs);
	}

	[Theory]
	[InlineData("avait-", true)]
	[InlineData("avait- ", true)]
	[InlineData("— ", false)]
	[InlineData("10 -", false)]
	public void EndsWithHyphen_OnlyAfterALetter(string text, bool expected)
	{
		var rb = new RunBuilder();
		rb.Append(text);

		Assert.Equal(expected, rb.EndsWithHyphen);
	}
}
