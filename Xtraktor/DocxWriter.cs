using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace Xtraktor;

/// <summary>
/// Writes the book as a plain Word container: chapter titles in "Titre 1",
/// text in "Normal", scene breaks in a dedicated style, the closing "FIN" in "Titre".
/// Italic/bold are direct formatting.
/// Style ids and names follow what a French-language Word produces.
/// Layout: A4, 2.5 cm margins, Georgia, 1.5 line spacing, 0.5 cm first-line indent.
/// </summary>
public static class DocxWriter
{
	const string Font = "Georgia";
	static readonly string Indent = Twips(0.5).ToString();
	const string OneAndHalfLine = "360";  // in 240ths of a line
	const string SingleLine = "240";

	static int Twips(double cm) => (int)Math.Round(cm / 2.54 * 1440);

	public static void Write(Book book, string path, string sceneStyle = "Ellipse", string sceneText = "***")
	{
		using var doc = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
		var main = doc.AddMainDocumentPart();
		main.AddNewPart<StyleDefinitionsPart>().Styles = Styles(sceneStyle);

		var body = new Body();
		foreach (var block in book.Blocks)
		{
			switch (block.Kind)
			{
				case BlockKind.Heading:
					body.Append(Paragraph("Titre1", block.Runs));
					break;
				case BlockKind.SceneBreak:
					body.Append(Paragraph(StyleId(sceneStyle), [new Run(sceneText)]));
					break;
				case BlockKind.Blank:
					body.Append(Paragraph("Normal", []));
					break;
				case BlockKind.MidTitle:
					body.Append(Paragraph("Titre", block.Runs));
					break;
				default:
					body.Append(Paragraph("Normal", block.Runs));
					break;
			}
		}
		var margin = Twips(2.5);
		body.Append(new SectionProperties(
			new PageSize { Width = 11906, Height = 16838 },  // A4
			new PageMargin { Top = margin, Bottom = margin, Left = (uint)margin, Right = (uint)margin, Header = 708, Footer = 708, Gutter = 0 }));
		main.Document = new Document(body);
		doc.PackageProperties.Title = book.Source;
	}

	static string StyleId(string name) => name.Replace(" ", "");

	static Paragraph Paragraph(string styleId, IEnumerable<Run> runs)
	{
		var p = new Paragraph(new ParagraphProperties(new ParagraphStyleId { Val = styleId }));
		foreach (var r in runs)
		{
			var props = new RunProperties();
			if (r.Bold) props.Append(new Bold());
			if (r.Italic) props.Append(new Italic());
			var run = new DocumentFormat.OpenXml.Wordprocessing.Run();
			if (props.HasChildren) run.Append(props);
			run.Append(new Text(r.Text) { Space = SpaceProcessingModeValues.Preserve });
			p.Append(run);
		}
		return p;
	}

	static RunFonts Georgia() => new() { Ascii = Font, HighAnsi = Font, ComplexScript = Font, EastAsia = Font };

	static Styles Styles(string sceneStyle) => new(
		new DocDefaults(
			new RunPropertiesDefault(new RunPropertiesBaseStyle(
				new Languages { Val = "fr-FR" },
				new FontSize { Val = "24" })),
			new ParagraphPropertiesDefault(new ParagraphPropertiesBaseStyle(
				new SpacingBetweenLines { After = "120" }))),
		new Style(
			new StyleName { Val = "Normal" },
			new PrimaryStyle(),
			new StyleParagraphProperties(
				new SpacingBetweenLines { Line = OneAndHalfLine, LineRule = LineSpacingRuleValues.Auto },
				new Indentation { FirstLine = Indent },
				new Justification { Val = JustificationValues.Both }),
			new StyleRunProperties(Georgia(), new FontSize { Val = "24" })
		)
		{ Type = StyleValues.Paragraph, StyleId = "Normal", Default = true },
		new Style(
			new StyleName { Val = "heading 1" },
			new BasedOn { Val = "Normal" },
			new NextParagraphStyle { Val = "Normal" },
			new PrimaryStyle(),
			new StyleParagraphProperties(
				new KeepNext(),
				new PageBreakBefore(),
				new SpacingBetweenLines { Before = "480", After = "240", Line = OneAndHalfLine, LineRule = LineSpacingRuleValues.Auto },
				new Indentation { FirstLine = Indent },
				new Justification { Val = JustificationValues.Left },
				new OutlineLevel { Val = 0 }),
			new StyleRunProperties(Georgia(), new Bold(), new FontSize { Val = "36" })
		)
		{ Type = StyleValues.Paragraph, StyleId = "Titre1" },
		new Style(
			new StyleName { Val = "Title" },
			new BasedOn { Val = "Normal" },
			new NextParagraphStyle { Val = "Normal" },
			new PrimaryStyle(),
			new StyleParagraphProperties(
				new SpacingBetweenLines { Before = "480", After = "240", Line = SingleLine, LineRule = LineSpacingRuleValues.Auto },
				new Indentation { FirstLine = "0" },
				new Justification { Val = JustificationValues.Center }),
			new StyleRunProperties(new FontSize { Val = "40" })
		)
		{ Type = StyleValues.Paragraph, StyleId = "Titre" },
		new Style(
			new StyleName { Val = sceneStyle },
			new BasedOn { Val = "Normal" },
			new NextParagraphStyle { Val = "Normal" },
			new PrimaryStyle(),
			new StyleParagraphProperties(
				new SpacingBetweenLines { Before = "240", After = "240", Line = SingleLine, LineRule = LineSpacingRuleValues.Auto },
				new Indentation { FirstLine = "0" },
				new Justification { Val = JustificationValues.Center })
		)
		{ Type = StyleValues.Paragraph, StyleId = StyleId(sceneStyle), CustomStyle = true });
}
