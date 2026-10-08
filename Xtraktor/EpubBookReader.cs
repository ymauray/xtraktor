using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Xtraktor;

/// <summary>Reads the spine documents of an EPUB in reading order.</summary>
public static class EpubBookReader
{
	static readonly XNamespace Opf = "http://www.idpf.org/2007/opf";
	static readonly XNamespace Container = "urn:oasis:names:tc:opendocument:xmlns:container";

	public static Book Read(string path)
	{
		using var zip = ZipFile.OpenRead(path);
		var entries = zip.Entries.ToDictionary(e => e.FullName.Normalize(NormalizationForm.FormC));

		var opfPath = LoadXml(entries["META-INF/container.xml"]).Descendants(Container + "rootfile").First().Attribute("full-path")!.Value;
		var opf = LoadXml(entries[opfPath]);
		var baseDir = Path.GetDirectoryName(opfPath)?.Replace('\\', '/') is { Length: > 0 } d ? d + "/" : "";
		var manifest = opf.Descendants(Opf + "item").ToDictionary(i => i.Attribute("id")!.Value, i => i.Attribute("href")!.Value);

		var blocks = new List<Block>();
		foreach (var itemref in opf.Descendants(Opf + "itemref"))
		{
			var href = Uri.UnescapeDataString(manifest[itemref.Attribute("idref")!.Value]);
			var name = (baseDir + href).Normalize(NormalizationForm.FormC);
			if (!entries.TryGetValue(name, out var entry) || !name.EndsWith("html", StringComparison.OrdinalIgnoreCase)) continue;
			var body = LoadXml(entry).Descendants().FirstOrDefault(e => e.Name.LocalName == "body");
			if (body is not null) ReadBlocks(body, href, blocks);
		}
		return new Book { Source = Path.GetFileName(path), Blocks = blocks };
	}

	static XDocument LoadXml(ZipArchiveEntry entry)
	{
		using var stream = entry.Open();
		using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore });
		return XDocument.Load(reader, LoadOptions.PreserveWhitespace);
	}

	static void ReadBlocks(XElement container, string location, List<Block> blocks)
	{
		foreach (var el in container.Elements())
		{
			var cls = (string?)el.Attribute("class") ?? "";
			switch (el.Name.LocalName)
			{
				case "h1" or "h2" or "h3" or "h4" or "h5" or "h6":
					var kind = cls.Contains("mid-title") ? BlockKind.MidTitle : BlockKind.Heading;
					blocks.Add(new Block { Kind = kind, Location = location, Runs = Inline(el) });
					break;
				case "p" when cls.Contains("ellipse"):
					blocks.Add(Block.Of(BlockKind.SceneBreak, location));
					break;
				case "p" or "blockquote":
					var runs = Inline(el);
					if (runs.Count > 0) blocks.Add(new Block { Kind = BlockKind.Paragraph, Location = location, Runs = runs });
					else if (el.Name.LocalName == "p") blocks.Add(Block.Of(BlockKind.Blank, location));
					break;
				case "hr":
					blocks.Add(Block.Of(BlockKind.SceneBreak, location));
					break;
				default:
					ReadBlocks(el, location, blocks);
					break;
			}
		}
	}

	static List<Run> Inline(XElement el)
	{
		var rb = new RunBuilder();
		void Walk(XNode node, bool italic, bool bold)
		{
			switch (node)
			{
				case XText t: rb.Append(t.Value, italic, bold); break;
				case XElement e when e.Name.LocalName == "br": rb.Append(" ", italic, bold); break;
				case XElement e:
					var n = e.Name.LocalName;
					bool i = italic || n is "em" or "i" or "cite", b = bold || n is "strong" or "b";
					foreach (var child in e.Nodes()) Walk(child, i, b);
					break;
			}
		}
		foreach (var child in el.Nodes()) Walk(child, false, false);
		return rb.Build();
	}
}
