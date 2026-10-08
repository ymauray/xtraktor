using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Xtraktor;

/// <summary>Blank is an intentionally empty paragraph (vertical space in the layout).</summary>
public enum BlockKind { Heading, MidTitle, Paragraph, SceneBreak, Blank }

public sealed record Run(string Text, bool Italic = false, bool Bold = false)
{
	[JsonIgnore] public bool Plain => !Italic && !Bold;
}

public sealed class Block
{
	[JsonIgnore(Condition = JsonIgnoreCondition.Never)] public BlockKind Kind { get; init; }
	public List<Run> Runs { get; init; } = [];
	/// <summary>Where the block starts in its source (PDF page, EPUB file).</summary>
	public string Location { get; init; } = "";

	[JsonIgnore] public string Text => string.Concat(Runs.Select(r => r.Text));

	public static Block Of(BlockKind kind, string location, string text = "") =>
		new() { Kind = kind, Location = location, Runs = text.Length > 0 ? [new Run(text)] : [] };
}

public sealed class Book
{
	public string Source { get; init; } = "";
	public List<Block> Blocks { get; init; } = [];

	static readonly JsonSerializerOptions Json = new()
	{
		WriteIndented = true,
		Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
		Converters = { new JsonStringEnumConverter() },
	};

	public void Save(string path) => File.WriteAllText(path, JsonSerializer.Serialize(this, Json));
	public static Book LoadJson(string path) => JsonSerializer.Deserialize<Book>(File.ReadAllText(path), Json)!;

	public static Book Load(string path) => Path.GetExtension(path).ToLowerInvariant() switch
	{
		".pdf" => PdfBookReader.Read(path),
		".epub" => EpubBookReader.Read(path),
		".docx" => DocxBookReader.Read(path),
		".json" => LoadJson(path),
		var ext => throw new ArgumentException($"Format non pris en charge : {ext}"),
	};

	/// <summary>Keeps the blocks from the "Prologue" heading through the end of the "Épilogue" chapter.</summary>
	public Book SliceStory(string first = "prologue", string last = "épilogue")
	{
		static bool Is(Block b, string title) =>
			b.Kind == BlockKind.Heading && string.Equals(b.Text.Trim(), title, StringComparison.OrdinalIgnoreCase);

		int start = Blocks.FindIndex(b => Is(b, first));
		int lastHeading = Blocks.FindIndex(Math.Max(start, 0), b => Is(b, last));
		if (start < 0 || lastHeading < 0)
			throw new InvalidOperationException($"Titres « {first} » / « {last} » introuvables dans {Source}.");
		int end = Blocks.FindIndex(lastHeading + 1, b => b.Kind == BlockKind.Heading);
		if (end < 0) end = Blocks.Count;
		return new Book { Source = Source, Blocks = Blocks[start..end] };
	}
}

/// <summary>
/// Accumulates styled characters into runs: whitespace collapsed and trimmed, adjacent runs
/// of the same style merged. The canonical form additionally styles whitespace only when it
/// sits between two characters of that same style, so "<em>Rail</em> " and "<em>Rail </em>"
/// compare equal; extraction keeps the source styling so the manuscript can be rebuilt as is.
/// </summary>
public sealed class RunBuilder
{
	readonly StringBuilder _text = new();
	readonly List<(bool Italic, bool Bold)> _style = [];

	public bool IsEmpty => _text.Length == 0;
	public bool EndsWithSpace => _text.Length > 0 && IsSpace(_text[^1]);

	/// <summary>True when the last visible character is a hyphen preceded by a letter.</summary>
	public bool EndsWithHyphen
	{
		get
		{
			int i = _text.Length - 1;
			while (i >= 0 && IsSpace(_text[i])) i--;
			return i > 0 && _text[i] == '-' && char.IsLetter(_text[i - 1]);
		}
	}

	public void TrimEnd()
	{
		while (_text.Length > 0 && IsSpace(_text[^1]))
		{
			_text.Length--;
			_style.RemoveAt(_style.Count - 1);
		}
	}

	public void Append(string text, bool italic = false, bool bold = false)
	{
		foreach (var raw in text)
		{
			var c = raw is '\n' or '\r' or '\t' ? ' ' : raw;
			if (c == ' ' && (_text.Length == 0 || IsSpace(_text[^1]))) continue;
			if (c == ' ' || !IsSpace(c) || _text.Length == 0 || _text[^1] != ' ')
			{
				_text.Append(c);
				_style.Add((italic, bold));
			}
			else
			{
				// A non-breaking space wins over a plain space right before it.
				_text[^1] = c;
			}
		}
	}

	public static List<Run> Canonical(IEnumerable<Run> runs)
	{
		var rb = new RunBuilder();
		foreach (var r in runs) rb.Append(r.Text, r.Italic, r.Bold);
		return rb.Build(canonical: true);
	}

	public List<Run> Build(bool canonical = false)
	{
		int start = 0, end = _text.Length;
		while (start < end && IsSpace(_text[start])) start++;
		while (end > start && IsSpace(_text[end - 1])) end--;

		var style = _style.ToArray();
		for (int i = start; i < end && canonical; i++)
		{
			if (!IsSpace(_text[i])) continue;
			int p = i - 1; while (p >= start && IsSpace(_text[p])) p--;
			int n = i + 1; while (n < end && IsSpace(_text[n])) n++;
			style[i] = p >= start && n < end && style[p] == style[n] ? style[p] : (false, false);
		}

		var runs = new List<Run>();
		var sb = new StringBuilder();
		for (int i = start; i < end; i++)
		{
			if (i > start && style[i] != style[i - 1])
			{
				runs.Add(new Run(sb.ToString(), style[i - 1].Italic, style[i - 1].Bold));
				sb.Clear();
			}
			sb.Append(_text[i]);
		}
		if (sb.Length > 0) runs.Add(new Run(sb.ToString(), style[end - 1].Italic, style[end - 1].Bold));
		return runs;
	}

	public static bool IsSpace(char c) => c is ' ' or '\u00A0' or '\u202F' or '\u2009' or '\u2007';
}
