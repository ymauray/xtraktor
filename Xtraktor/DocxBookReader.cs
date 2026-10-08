using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace Xtraktor;

/// <summary>
/// Reads a manuscript using the conventions of the publishing script:
/// "Titre 1" → chapter heading, "Titre" → mid-title ("FIN"), "Ellipse" → scene break, anything else → paragraph.
/// Only direct italic/bold formatting on runs is taken into account.
/// </summary>
public static class DocxBookReader
{
	public static Book Read(string path, string sceneStyle = "Ellipse")
	{
		using var doc = WordprocessingDocument.Open(path, false);
		var body = doc.MainDocumentPart?.Document?.Body ?? throw new InvalidOperationException($"Document vide : {path}");

		// Style ids are localized ("Titre1"), names are not ("heading 1"): match on the name.
		var names = doc.MainDocumentPart!.StyleDefinitionsPart?.Styles?.Elements<Style>()
			.Where(s => s.StyleId?.Value is not null)
			.ToDictionary(s => s.StyleId!.Value!, s => s.StyleName?.Val?.Value ?? s.StyleId!.Value!) ?? [];

		var blocks = new List<Block>();
		int index = 0;
		foreach (var p in body.Descendants<Paragraph>())
		{
			index++;
			var styleId = p.ParagraphProperties?.ParagraphStyleId?.Val?.Value ?? "Normal";
			var style = names.GetValueOrDefault(styleId, styleId).ToLowerInvariant();
			var location = $"§ {index}";

			if (style == sceneStyle.ToLowerInvariant() || styleId.Equals(sceneStyle, StringComparison.OrdinalIgnoreCase))
			{
				blocks.Add(Block.Of(BlockKind.SceneBreak, location));
				continue;
			}

			var rb = new RunBuilder();
			foreach (var r in p.Descendants<DocumentFormat.OpenXml.Wordprocessing.Run>())
			{
				var props = r.RunProperties;
				bool italic = props?.Italic is { } i && (i.Val?.Value ?? true);
				bool bold = props?.Bold is { } b && (b.Val?.Value ?? true);
				foreach (var child in r.ChildElements)
				{
					switch (child)
					{
						case Text t: rb.Append(t.Text, italic, bold); break;
						case TabChar or Break: rb.Append(" ", italic, bold); break;
						case NoBreakHyphen: rb.Append("-", italic, bold); break;
					}
				}
			}
			var runs = rb.Build();
			if (runs.Count == 0)
			{
				blocks.Add(Block.Of(BlockKind.Blank, location));
				continue;
			}

			var kind = style switch
			{
				"heading 1" => BlockKind.Heading,
				"title" => BlockKind.MidTitle,
				_ => BlockKind.Paragraph,
			};
			blocks.Add(new Block { Kind = kind, Location = location, Runs = runs });
		}
		return new Book { Source = Path.GetFileName(path), Blocks = blocks };
	}
}
