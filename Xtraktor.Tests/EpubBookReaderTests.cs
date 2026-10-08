using System.IO.Compression;
using System.Text;

namespace Xtraktor.Tests;

public class EpubBookReaderTests
{
	static string Xhtml(string body) => $"""
        <?xml version="1.0" encoding="UTF-8"?>
        <!DOCTYPE html>
        <html xmlns="http://www.w3.org/1999/xhtml"><head><title>t</title></head>
        <body class="chapter">{body}</body></html>
        """;

	/// <summary>Writes a minimal EPUB whose spine order differs from the manifest order.</summary>
	static void WriteEpub(string path)
	{
		using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
		void Add(string name, string content)
		{
			using var w = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false));
			w.Write(content);
		}
		Add("mimetype", "application/epub+zip");
		Add("META-INF/container.xml", """
            <?xml version="1.0"?>
            <container version="1.0" xmlns="urn:oasis:names:tc:opendocument:xmlns:container">
              <rootfiles><rootfile full-path="OEBPS/content.opf" media-type="application/oebps-package+xml"/></rootfiles>
            </container>
            """);
		Add("OEBPS/content.opf", """
            <?xml version="1.0" encoding="UTF-8"?>
            <package xmlns="http://www.idpf.org/2007/opf" version="3.0">
              <manifest>
                <item id="epilogue" href="%C3%A9pilogue.xhtml" media-type="application/xhtml+xml"/>
                <item id="title" href="title.xhtml" media-type="application/xhtml+xml"/>
                <item id="prologue" href="prologue.xhtml" media-type="application/xhtml+xml"/>
                <item id="thanks" href="thanks.xhtml" media-type="application/xhtml+xml"/>
              </manifest>
              <spine><itemref idref="title"/><itemref idref="prologue"/><itemref idref="epilogue"/><itemref idref="thanks"/></spine>
            </package>
            """);
		Add("OEBPS/title.xhtml", Xhtml("<h1>Titre du livre</h1><p>Page de titre</p>"));
		Add("OEBPS/prologue.xhtml", Xhtml("""
            <h1>Prologue</h1>
            <p>&#x2014;&#160;Ça va&#160;?</p>
            <p>Ils jouaient aux <em>Petits</em><em> Chevaux</em>.</p>
            <p class="ellipse">***</p>
            <p></p>
            <p>Pour <strong>toujours</strong>.</p>
            """));
		Add("OEBPS/épilogue.xhtml", Xhtml("""<h1>Épilogue</h1><div><p>Dernière phrase.</p></div><h1 class="mid-title">FIN</h1>"""));
		Add("OEBPS/thanks.xhtml", Xhtml("<h1>Remerciements</h1><p>Merci.</p>"));
	}

	[Fact]
	public void Read_FollowsTheSpineAndMapsTheMarkup()
	{
		using var tmp = new TempDir();
		var path = tmp.File("book.epub");
		WriteEpub(path);

		var story = EpubBookReader.Read(path).SliceStory();

		Assert.Equal(
		[
			"Heading:Prologue",
			"Paragraph:—\u00A0Ça va\u00A0?",
			"Paragraph:Ils jouaient aux _Petits Chevaux_.",
			"SceneBreak:",
			"Blank:",
			"Paragraph:Pour toujours.",
			"Heading:Épilogue",
			"Paragraph:Dernière phrase.",
			"MidTitle:FIN",
		], TestBooks.Shape(story));
		Assert.True(story.Blocks[5].Runs.Single(r => r.Text == "toujours").Bold);
		Assert.Equal("prologue.xhtml", story.Blocks[0].Location);
	}

	[Fact]
	public void ReadAll_KeepsFrontAndBackMatter()
	{
		using var tmp = new TempDir();
		var path = tmp.File("book.epub");
		WriteEpub(path);

		var book = Book.Load(path);

		Assert.Equal("Titre du livre", book.Blocks[0].Text);
		Assert.Equal("Merci.", book.Blocks[^1].Text);
	}
}
