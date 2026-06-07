using System.IO.Compression;
using System.Text;
using Thermalith.Core.Model;
using Thermalith.Core.Serialization;
using Xunit;

namespace Thermalith.Core.Tests;

public class NlblPackageTests
{
    private static LabelPackage SamplePackage()
    {
        var doc = LabelDocumentSerializer.FromJson(TestSupport.KitchenSinkJson());
        return new LabelPackage
        {
            Manifest = new Manifest { Id = "com.evilgeniuslabs.test", Name = "kitchen-sink", Version = "1.0.0", License = "GPL-3.0-or-later" },
            Document = doc,
            Data = [new Dictionary<string, object?> { ["name"] = "Widget Pro", ["product_sku"] = "WP-001", ["price"] = "$9.99" }],
            Assets = new Dictionary<string, byte[]> { ["image_0001"] = TestSupport.SamplePng() },
        };
    }

    [Fact]
    public void Save_then_load_round_trips_document_data_and_assets()
    {
        var package = SamplePackage();

        using var ms = new MemoryStream();
        LabelPackageIo.Save(package, ms);
        ms.Position = 0;
        var loaded = LabelPackageIo.Load(ms);

        Assert.Equal(package.Document.Elements.Count, loaded.Document.Elements.Count);
        Assert.Equal("kitchen-sink", loaded.Manifest.Name);
        Assert.NotNull(loaded.Data);
        Assert.Single(loaded.Data!);
        Assert.Equal("Widget Pro", loaded.Data![0]["name"]);
        Assert.True(loaded.Assets.ContainsKey("image_0001"));
        Assert.Equal(package.Assets["image_0001"], loaded.Assets["image_0001"]);
    }

    [Fact]
    public void Fingerprint_is_written_and_verifies_on_load()
    {
        var package = SamplePackage();

        using var ms = new MemoryStream();
        LabelPackageIo.Save(package, ms);
        ms.Position = 0;
        var loaded = LabelPackageIo.Load(ms);

        Assert.False(string.IsNullOrEmpty(loaded.Manifest.Fingerprint));
        Assert.True(loaded.FingerprintValid);
    }

    [Fact]
    public void Extension_data_in_label_json_survives_the_package_round_trip()
    {
        const string json = """
        { "schemaVersion": "1.0", "metadata": { "name": "x" },
          "canvas": { "widthMm": 40, "heightMm": 30, "futureField": 7 },
          "elements": [] }
        """;
        var package = new LabelPackage
        {
            Manifest = new Manifest { Id = "id", Name = "x" },
            Document = LabelDocumentSerializer.FromJson(json),
        };

        using var ms = new MemoryStream();
        LabelPackageIo.Save(package, ms);
        ms.Position = 0;
        var loaded = LabelPackageIo.Load(ms);

        Assert.True(loaded.Document.Canvas.Extensions!.ContainsKey("futureField"));
    }

    [Fact]
    public void Refuses_a_newer_manifest_version()
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteText(zip, "manifest.json", """{ "manifestVersion": 999, "id": "x", "name": "future" }""");
            WriteText(zip, "label.json", """{ "schemaVersion": "1.0", "metadata": { "name": "x" }, "canvas": { "widthMm": 40, "heightMm": 30 }, "elements": [] }""");
        }
        ms.Position = 0;

        var ex = Assert.Throws<LabelPackageException>(() => LabelPackageIo.Load(ms));
        Assert.Contains("manifestVersion", ex.Message);
    }

    [Fact]
    public void Refuses_a_newer_schema_major()
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteText(zip, "manifest.json", """{ "manifestVersion": 1, "id": "x", "name": "future" }""");
            WriteText(zip, "label.json", """{ "schemaVersion": "2.0", "metadata": { "name": "x" }, "canvas": { "widthMm": 40, "heightMm": 30 }, "elements": [] }""");
        }
        ms.Position = 0;

        var ex = Assert.Throws<LabelPackageException>(() => LabelPackageIo.Load(ms));
        Assert.Contains("schemaVersion", ex.Message);
    }

    private static void WriteText(ZipArchive zip, string name, string content)
    {
        var entry = zip.CreateEntry(name);
        using var s = entry.Open();
        s.Write(Encoding.UTF8.GetBytes(content));
    }
}
