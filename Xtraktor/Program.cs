using System.Reflection;
using Xtraktor;

const string Usage = """
    Usage :
      xtraktor extract <source> [-o livre.json]               Extrait le texte (prologue → épilogue) en JSON
      xtraktor diff <a> <b> [-o rapport.html]                 Compare deux sources
      xtraktor docx <source> -o livre.docx [--scene-style Ellipse] [--no-fin]
      (sources acceptées partout : .pdf, .epub, .docx, .json)
    Option commune : --all  garde tout le livre au lieu de la plage prologue → épilogue
      xtraktor --version | --help
    """;

if (args is ["--version"] or ["-v"])
{
	var version = typeof(Book).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "?";
	Console.WriteLine($"xtraktor {version.Split('+')[0]}");
	return 0;
}
if (args is ["--help"] or ["-h"]) { Console.WriteLine(Usage); return 0; }
if (args.Length < 2) { Console.Error.WriteLine(Usage); return 1; }

string? Option(string name) => Array.IndexOf(args, name) is var i and >= 0 && i + 1 < args.Length ? args[i + 1] : null;
bool Flag(string name) => args.Contains(name);
Book Load(string path) { var book = Book.Load(path); return Flag("--all") ? book : book.SliceStory(); }

try
{
	switch (args[0])
	{
		case "extract":
			{
				var book = Load(args[1]);
				var output = Option("-o") ?? Path.ChangeExtension(args[1], ".json");
				book.Save(output);
				Console.WriteLine($"{book.Blocks.Count} blocs → {output}");
				break;
			}
		case "diff" when args.Length >= 3:
			{
				var (a, b) = (Load(args[1]), Load(args[2]));
				var diffs = BookDiff.Compare(a, b);
				Console.Write(BookDiff.Summary(a, b, diffs));
				if (Option("-o") is { } report)
				{
					File.WriteAllText(report, BookDiff.Html(a, b, diffs));
					Console.WriteLine($"Rapport → {report}");
				}
				break;
			}
		case "docx":
			{
				var book = Load(args[1]);
				if (Flag("--no-fin")) book.Blocks.RemoveAll(b => b.Kind == BlockKind.MidTitle);
				var output = Option("-o") ?? Path.ChangeExtension(args[1], ".docx");
				DocxWriter.Write(book, output, Option("--scene-style") ?? "Ellipse");
				Console.WriteLine($"{book.Blocks.Count} blocs → {output}");
				break;
			}
		default:
			Console.Error.WriteLine(Usage);
			return 1;
	}
	return 0;
}
catch (Exception e) when (e is InvalidOperationException or ArgumentException or IOException)
{
	Console.Error.WriteLine($"Erreur : {e.Message}");
	return 2;
}
