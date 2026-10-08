using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using DiffPlex;
using DiffPlex.Chunkers;

namespace Xtraktor;

public enum DiffKind
{
	/// <summary>The words differ.</summary>
	Text,
	/// <summary>Same characters, different italic/bold.</summary>
	Style,
	/// <summary>Only typography differs: kind of space, apostrophe, ellipsis, heading case.</summary>
	Typography,
	/// <summary>An empty paragraph present on one side only.</summary>
	Blank,
}

public sealed record BlockDiff(DiffKind Kind, Block? A, Block? B, string Chapter);

public static partial class BookDiff
{
	public static List<BlockDiff> Compare(Book a, Book b)
	{
		var keysA = a.Blocks.Select(Key).ToArray();
		var keysB = b.Blocks.Select(Key).ToArray();
		var result = Differ.Instance.CreateDiffs(string.Join("\n", keysA), string.Join("\n", keysB), false, false, new LineChunker());

		var diffs = new List<BlockDiff>();
		string chapter = "";
		int ia = 0, ib = 0;

		void Track(Block blk) { if (blk.Kind == BlockKind.Heading) chapter = blk.Text; }

		void Equal(int count)
		{
			for (int k = 0; k < count; k++, ia++, ib++)
			{
				var (x, y) = (a.Blocks[ia], b.Blocks[ib]);
				Track(x);
				if (Same(x.Runs, y.Runs)) continue;
				var kind = x.Text == y.Text ? DiffKind.Style : SameStyleIgnoringTypography(x, y) ? DiffKind.Typography : DiffKind.Style;
				diffs.Add(new BlockDiff(kind, x, y, chapter));
			}
		}

		void OneSided(Block? x, Block? y) =>
			diffs.Add(new BlockDiff((x ?? y)!.Kind == BlockKind.Blank ? DiffKind.Blank : DiffKind.Text, x, y, chapter));

		foreach (var block in result.DiffBlocks)
		{
			Equal(block.DeleteStartA - ia);
			var deleted = a.Blocks.GetRange(ia, block.DeleteCountA);
			var inserted = b.Blocks.GetRange(ib, block.InsertCountB);
			ia += block.DeleteCountA;
			ib += block.InsertCountB;

			// Pair each deleted block with the next inserted block of the same kind, so that an
			// added blank line does not get matched against the edited paragraph that follows it.
			int next = 0;
			foreach (var x in deleted)
			{
				Track(x);
				int j = inserted.FindIndex(next, y => y.Kind == x.Kind);
				if (j < 0) { OneSided(x, null); continue; }
				for (; next < j; next++) OneSided(null, inserted[next]);
				diffs.Add(new BlockDiff(DiffKind.Text, x, inserted[j], chapter));
				next = j + 1;
			}
			for (; next < inserted.Count; next++) OneSided(null, inserted[next]);
		}
		Equal(a.Blocks.Count - ia);
		return diffs;
	}

	static bool Same(List<Run> x, List<Run> y) => RunBuilder.Canonical(x).SequenceEqual(RunBuilder.Canonical(y));

	/// <summary>Alignment key: the block kind plus its typographically normalized text.</summary>
	static string Key(Block b) => b.Kind + "|" + Normalize(b.Text, b.Kind is BlockKind.Heading or BlockKind.MidTitle);

	public static string Normalize(string text, bool foldCase = false)
	{
		var s = text.Replace('\u00A0', ' ').Replace('\u202F', ' ').Replace('\u2009', ' ').Replace('\u2007', ' ')
			.Replace('\'', '’').Replace("...", "…").Replace('–', '—');
		s = Spaces().Replace(s, " ").Trim();
		return foldCase ? s.ToUpperInvariant() : s;
	}

	static bool SameStyleIgnoringTypography(Block x, Block y)
	{
		static string Styled(Block b) => string.Concat(RunBuilder.Canonical(b.Runs).Select(r => r.Plain ? Normalize(r.Text) : $"[{Normalize(r.Text)}]"));
		return Styled(x).Equals(Styled(y), StringComparison.OrdinalIgnoreCase) || (!x.Runs.Any(r => !r.Plain) && !y.Runs.Any(r => !r.Plain));
	}

	[GeneratedRegex(@"\s+")] private static partial Regex Spaces();
	[GeneratedRegex(@"\w+|[\s\u00A0\u202F]|[^\w\s]", RegexOptions.None)] private static partial Regex Tokens();

	/// <summary>Word-level diff of two strings, as (text, -1 deleted / 0 same / +1 inserted) segments.</summary>
	public static List<(string Text, int Op)> WordDiff(string x, string y)
	{
		var tx = Tokens().Matches(x).Select(m => m.Value).ToArray();
		var ty = Tokens().Matches(y).Select(m => m.Value).ToArray();
		var r = Differ.Instance.CreateDiffs(string.Join("\n", tx), string.Join("\n", ty), false, false, new LineChunker());
		var segs = new List<(string, int)>();
		int ia = 0, ib = 0;
		void Add(string t, int op) { if (segs.Count > 0 && segs[^1].Item2 == op) segs[^1] = (segs[^1].Item1 + t, op); else segs.Add((t, op)); }
		foreach (var blk in r.DiffBlocks)
		{
			while (ia < blk.DeleteStartA) { Add(tx[ia++], 0); ib++; }
			for (int k = 0; k < blk.DeleteCountA; k++) Add(tx[ia++], -1);
			for (int k = 0; k < blk.InsertCountB; k++) Add(ty[ib++], +1);
		}
		while (ia < tx.Length) Add(tx[ia++], 0);
		return segs;
	}

	/// <summary>Short description of what changed typographically, e.g. "espace insécable → espace".</summary>
	public static string DescribeTypography(Block a, Block b)
	{
		var labels = new SortedSet<string>();
		var (na, nb) = (Normalize(a.Text), Normalize(b.Text));
		if (na != nb && string.Equals(na, nb, StringComparison.OrdinalIgnoreCase)) labels.Add("casse");
		foreach (var (t, op) in WordDiff(a.Text.ToUpperInvariant(), b.Text.ToUpperInvariant()).Where(s => s.Op != 0))
			foreach (var c in t.Distinct())
				labels.Add((op < 0 ? "− " : "+ ") + CharName(c));
		return labels.Count > 0 ? string.Join(", ", labels) : "casse";
	}

	static string CharName(char c) => c switch
	{
		' ' => "espace",
		'\u00A0' => "espace insécable",
		'\u202F' => "espace fine insécable",
		'\'' => "apostrophe droite",
		'’' => "apostrophe courbe",
		'…' => "points de suspension (…)",
		'.' => "point",
		_ => $"« {c} »",
	};

	public static string Summary(Book a, Book b, List<BlockDiff> diffs)
	{
		var sb = new StringBuilder();
		sb.AppendLine($"A : {a.Source} — {a.Blocks.Count} blocs");
		sb.AppendLine($"B : {b.Source} — {b.Blocks.Count} blocs");
		foreach (var k in Enum.GetValues<BlockKind>())
			sb.AppendLine($"  {k,-10} A={a.Blocks.Count(x => x.Kind == k),5}  B={b.Blocks.Count(x => x.Kind == k),5}");
		sb.AppendLine($"Différences de texte        : {diffs.Count(d => d.Kind == DiffKind.Text)}");
		sb.AppendLine($"Différences de mise en forme : {diffs.Count(d => d.Kind == DiffKind.Style)}");
		sb.AppendLine($"Différences typographiques  : {diffs.Count(d => d.Kind == DiffKind.Typography)}");
		sb.AppendLine($"Lignes vides d'un seul côté : {diffs.Count(d => d.Kind == DiffKind.Blank)}");
		foreach (var g in diffs.Where(d => d.Kind == DiffKind.Typography).GroupBy(d => DescribeTypography(d.A!, d.B!)).OrderByDescending(g => g.Count()))
			sb.AppendLine($"    {g.Count(),5} × {g.Key}");
		return sb.ToString();
	}

	public static string Html(Book a, Book b, List<BlockDiff> diffs)
	{
		// HtmlEncode turns non-breaking spaces into numeric entities; make them visible.
		static string E(string s) => WebUtility.HtmlEncode(s).Replace("&#160;", "<span class=nb>⍽</span>").Replace("&#8239;", "<span class=nb>⍽</span>");
		static string Runs(Block? blk) => blk is null ? "<i class=none>(absent)</i>"
			: blk.Kind == BlockKind.SceneBreak ? "<span class=kind>[séparateur de scène]</span>"
			: blk.Kind == BlockKind.Blank ? "<span class=kind>[ligne vide]</span>"
			: string.Concat(blk.Runs.Select(r => (r.Italic, r.Bold) switch
			{
				(true, true) => $"<strong><em class=st>{E(r.Text)}</em></strong>",
				(true, _) => $"<em class=st>{E(r.Text)}</em>",
				(_, true) => $"<strong class=st>{E(r.Text)}</strong>",
				_ => E(r.Text),
			}));
		static string Inline(Block x, Block y) => string.Concat(WordDiff(x.Text, y.Text).Select(s => s.Op switch
		{
			< 0 => $"<del>{E(s.Text)}</del>",
			> 0 => $"<ins>{E(s.Text)}</ins>",
			_ => E(s.Text),
		}));

		var sb = new StringBuilder();
		sb.Append($$$"""
            <!doctype html><html lang=fr><head><meta charset=utf-8><meta name=viewport content="width=device-width,initial-scale=1">
            <title>Comparaison</title><style>
            :root{--bg:#fbfaf7;--fg:#222;--mut:#777;--card:#fff;--line:#e4e1da;--del:#fbd5d5;--ins:#cdeccd;--st:#fff0b3}
            @media (prefers-color-scheme:dark){:root{--bg:#1b1b1d;--fg:#e6e4df;--mut:#999;--card:#242427;--line:#3a3a3f;--del:#6b2a2a;--ins:#24502a;--st:#5a4a12}}
            body{background:var(--bg);color:var(--fg);font:16px/1.55 Georgia,serif;margin:0 auto;max-width:960px;padding:24px 16px}
            h1,h2{font-family:system-ui,sans-serif}h2{margin-top:2em;border-bottom:1px solid var(--line)}
            pre{background:var(--card);border:1px solid var(--line);padding:12px;overflow:auto;font-size:13px}
            .d{background:var(--card);border:1px solid var(--line);border-radius:6px;padding:10px 14px;margin:10px 0}
            .meta{font:12px system-ui,sans-serif;color:var(--mut);margin-bottom:4px}
            del{background:var(--del);text-decoration:line-through}ins{background:var(--ins);text-decoration:none}
            .st{background:var(--st)}.nb{color:var(--mut);font-size:.8em}.none,.kind{color:var(--mut)}
            .ab{display:grid;grid-template-columns:2em 1fr;gap:4px}.ab b{font-family:system-ui;color:var(--mut)}
            </style></head><body><h1>Comparaison</h1><pre>{{{E(Summary(a, b, diffs))}}}</pre>
            """);

		void Section(DiffKind kind, string title, bool collapsed)
		{
			var list = diffs.Where(d => d.Kind == kind).ToList();
			sb.Append($"<h2>{title} ({list.Count})</h2>");
			if (list.Count == 0) { sb.Append("<p class=none>Aucune.</p>"); return; }
			if (collapsed) sb.Append("<details><summary>Afficher</summary>");
			foreach (var d in list)
			{
				var where = $"{E(d.Chapter)} · A {E(d.A?.Location ?? "—")} · B {E(d.B?.Location ?? "—")}";
				if (kind == DiffKind.Typography) where += " · " + E(DescribeTypography(d.A!, d.B!));
				sb.Append($"<div class=d><div class=meta>{where}</div>");
				if (d is { A: not null, B: not null } && kind != DiffKind.Style)
					sb.Append(Inline(d.A, d.B));
				else
					sb.Append($"<div class=ab><b>A</b><div>{Runs(d.A)}</div><b>B</b><div>{Runs(d.B)}</div></div>");
				sb.Append("</div>");
			}
			if (collapsed) sb.Append("</details>");
		}

		Section(DiffKind.Text, "Différences de texte", false);
		Section(DiffKind.Style, "Différences de mise en forme (italique / gras surlignés)", false);
		Section(DiffKind.Blank, "Lignes vides présentes d'un seul côté", false);
		Section(DiffKind.Typography, "Différences typographiques (⍽ = espace insécable)", true);
		sb.Append("</body></html>");
		return sb.ToString();
	}
}
