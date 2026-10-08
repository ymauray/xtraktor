namespace Xtraktor.Tests;

/// <summary>Small helpers to build books and scratch files in tests.</summary>
internal static class TestBooks
{
	public static Block H(string text) => Block.Of(BlockKind.Heading, "", text);
	public static Block P(string text) => Block.Of(BlockKind.Paragraph, "", text);
	public static Block P(params Run[] runs) => new() { Kind = BlockKind.Paragraph, Runs = [.. runs] };
	public static Block Scene() => Block.Of(BlockKind.SceneBreak, "");
	public static Block Blank() => Block.Of(BlockKind.Blank, "");
	public static Block Fin() => Block.Of(BlockKind.MidTitle, "", "FIN");

	public static Book Book(params Block[] blocks) => new() { Source = "test", Blocks = [.. blocks] };

	public static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

	/// <summary>Kind and canonical runs of each block, for structural comparisons.</summary>
	public static List<string> Shape(Book book) =>
		book.Blocks.Select(b => b.Kind + ":" + string.Concat(RunBuilder.Canonical(b.Runs).Select(r => r.Italic ? $"_{r.Text}_" : r.Text))).ToList();
}

/// <summary>A temporary directory removed at the end of the test.</summary>
internal sealed class TempDir : IDisposable
{
	public string Path { get; } = Directory.CreateTempSubdirectory("xtraktor-tests-").FullName;
	public string File(string name) => System.IO.Path.Combine(Path, name);
	public void Dispose() => Directory.Delete(Path, recursive: true);
}
