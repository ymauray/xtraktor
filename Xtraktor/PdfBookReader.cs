using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace Xtraktor;

/// <summary>
/// Rebuilds the book structure from the geometry of a Typst-generated PDF:
/// headings are the large-size lines, a paragraph starts on an indented line,
/// scene breaks are the wide, flat vector ornaments, an extra line pitch between
/// two lines is an empty paragraph, page numbers sit in the footer.
/// </summary>
public static class PdfBookReader
{
	const double FooterTop = 45;         // page numbers have their baseline at ~28 pt
	const double HeadingMinSize = 13;    // body text is 11.5 pt, chapter titles 16 pt
	const double IndentMin = 7;          // first-line indent is 14.2 pt
	const double OrnamentMinWidth = 50;  // the scene-break ornament is 124 × 3 pt
	const double OrnamentMaxHeight = 10;
	const double DefaultPitch = 17.5;    // 11.5 pt text, line baselines 17.47 pt apart

	sealed record Line(double Y, double X0, double Size, List<Letter> Letters);

	public static Book Read(string path)
	{
		using var doc = PdfDocument.Open(path);
		var blocks = new List<Block>();
		RunBuilder? para = null;
		string paraLocation = "";
		Block? heading = null;
		int headingPage = 0;

		void Flush()
		{
			if (para is { IsEmpty: false })
				blocks.Add(new Block { Kind = BlockKind.Paragraph, Location = paraLocation, Runs = para.Build() });
			para = null;
		}

		foreach (var page in doc.GetPages())
		{
			var location = $"p. {page.Number}";
			var lines = GroupLines(page);
			var bodyLines = lines.Where(l => l.Size < HeadingMinSize).ToList();
			double margin = bodyLines.Count > 0 ? bodyLines.Min(l => l.X0) : 0;

			// Scene-break ornaments, interleaved with lines by vertical position.
			var items = lines.Select(l => (l.Y, Line: (Line?)l)).ToList();
			foreach (var y in Ornaments(page)) items.Add((y, null));

			// Line pitch of the body text, to recognize the empty lines left on purpose.
			var pitches = bodyLines.Zip(bodyLines.Skip(1), (a, b) => a.Y - b.Y).Where(d => d > 0).Order().ToList();
			double pitch = pitches.Count > 0 ? pitches[pitches.Count / 2] : DefaultPitch;
			double? previousBodyY = null;

			foreach (var (_, line) in items.OrderByDescending(i => i.Y))
			{
				if (line is null)
				{
					Flush();
					heading = null;
					previousBodyY = null;
					blocks.Add(Block.Of(BlockKind.SceneBreak, location));
					continue;
				}

				var text = LineText(line);
				if (line.Size >= HeadingMinSize)
				{
					Flush();
					previousBodyY = null;
					// Consecutive large lines on the same page form a single heading.
					if (heading is not null && headingPage == page.Number)
					{
						heading.Runs[0] = new Run(heading.Runs[0].Text + " " + text.Trim());
						continue;
					}
					heading = Block.Of(BlockKind.Heading, location, text.Trim());
					headingPage = page.Number;
					blocks.Add(heading);
					continue;
				}

				heading = null;
				int blanks = previousBodyY is { } py ? (int)Math.Round((py - line.Y) / pitch) - 1 : 0;
				previousBodyY = line.Y;
				if (blanks > 0)
				{
					Flush();
					for (int k = 0; k < blanks; k++) blocks.Add(Block.Of(BlockKind.Blank, location));
				}
				if (para is null || line.X0 - margin > IndentMin)
				{
					Flush();
					para = new RunBuilder();
					paraLocation = location;
				}
				else if (para.EndsWithHyphen)
				{
					// Hyphenation is off in the layout, so a line-final hyphen is a real one ("avait-|elle").
					para.TrimEnd();
				}
				else if (!para.EndsWithSpace)
				{
					para.Append(" ");
				}
				AppendLine(para, line);
			}
		}
		Flush();

		// A heading standing alone after the last chapter ("FIN") is a mid-title, not a chapter.
		for (int i = 0; i < blocks.Count; i++)
			if (blocks[i].Kind == BlockKind.Heading && string.Equals(blocks[i].Text, "FIN", StringComparison.OrdinalIgnoreCase))
				blocks[i] = new Block { Kind = BlockKind.MidTitle, Location = blocks[i].Location, Runs = blocks[i].Runs };

		return new Book { Source = Path.GetFileName(path), Blocks = blocks };
	}

	static List<Line> GroupLines(Page page)
	{
		var letters = page.Letters.Where(l => l.StartBaseLine.Y > FooterTop).OrderByDescending(l => l.StartBaseLine.Y);
		var lines = new List<List<Letter>>();
		foreach (var l in letters)
		{
			if (lines.Count > 0 && Math.Abs(lines[^1][0].StartBaseLine.Y - l.StartBaseLine.Y) < 2) lines[^1].Add(l);
			else lines.Add([l]);
		}
		return lines.Select(ls =>
		{
			ls.Sort((a, b) => a.StartBaseLine.X.CompareTo(b.StartBaseLine.X));
			var visible = ls.Where(l => !string.IsNullOrWhiteSpace(l.Value)).ToList();
			return new Line(ls[0].StartBaseLine.Y, (visible.Count > 0 ? visible : ls)[0].StartBaseLine.X, ls.Max(l => l.PointSize), ls);
		}).ToList();
	}

	static IEnumerable<double> Ornaments(Page page)
	{
		var ys = new List<double>();
		foreach (var path in page.Paths)
		{
			if (path.GetBoundingRectangle() is not { } r || r.Width < OrnamentMinWidth || r.Height > OrnamentMaxHeight) continue;
			var y = r.Centroid.Y;
			if (!ys.Any(o => Math.Abs(o - y) < 2)) ys.Add(y);  // each ornament is drawn twice (fill + stroke)
		}
		return ys;
	}

	static string LineText(Line line) => string.Concat(line.Letters.Select(l => l.Value));

	static void AppendLine(RunBuilder para, Line line)
	{
		Letter? prev = null;
		foreach (var l in line.Letters)
		{
			// Guard against glyphs placed apart without an explicit space glyph.
			if (prev is not null && prev.Value != " " && l.Value != " " && l.StartBaseLine.X - prev.EndBaseLine.X > l.PointSize * 0.2)
				para.Append(" ");
			var font = l.FontName ?? "";
			para.Append(l.Value, font.Contains("Italic", StringComparison.OrdinalIgnoreCase), font.Contains("Bold", StringComparison.OrdinalIgnoreCase));
			prev = l;
		}
	}
}
