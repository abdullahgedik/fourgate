using Fourgate.Core.Data;

namespace Fourgate.Tests;

public class DataLoaderTests
{
    private const string File = "game.json";

    private const string ValidJson = """
        {
          "virtualWidth": 1920,
          "virtualHeight": 1080,
          "arena": { "x": 0, "y": 0, "size": 2000, "doorWidth": 280, "wallThickness": 24 },
          "camera": { "zoom": 1.15, "leadFraction": 0.35, "leadMaxDistance": 260, "smoothing": 10, "edgeMargin": 120 },
          "player": { "moveSpeed": 300, "radius": 20 }
        }
        """;

    [Fact]
    public void ValidFile_Loads()
    {
        var config = DataLoader.ParseGameConfig(ValidJson, File);

        Assert.Equal(1920, config.VirtualWidth);
        Assert.Equal(2000f, config.Arena.Size);
        Assert.Equal(1.15f, config.Camera.Zoom);
        Assert.Equal(300f, config.Player.MoveSpeed);
    }

    [Fact]
    public void MissingField_NamesTheFile()
    {
        var json = ValidJson.Replace("\"virtualHeight\": 1080,", string.Empty);

        var error = Assert.Throws<DataLoadException>(() => DataLoader.ParseGameConfig(json, File));

        Assert.Equal(File, error.File);
        Assert.Contains("virtualHeight", error.Message);
    }

    [Theory]
    [InlineData("\"doorWidth\": 280", "\"doorWidth\": 2000", "arena.doorWidth")]
    [InlineData("\"zoom\": 1.15", "\"zoom\": 0", "camera.zoom")]
    [InlineData("\"leadFraction\": 0.35", "\"leadFraction\": 1.5", "camera.leadFraction")]
    [InlineData("\"moveSpeed\": 300", "\"moveSpeed\": -1", "player.moveSpeed")]
    public void OutOfRangeValue_NamesTheField(string original, string replacement, string expectedField)
    {
        var json = ValidJson.Replace(original, replacement);

        var error = Assert.Throws<DataLoadException>(() => DataLoader.ParseGameConfig(json, File));

        Assert.Equal(expectedField, error.Field);
    }

    [Fact]
    public void MalformedJson_IsReportedAsDataLoadException()
    {
        Assert.Throws<DataLoadException>(() => DataLoader.ParseGameConfig("{ not json", File));
    }
}
