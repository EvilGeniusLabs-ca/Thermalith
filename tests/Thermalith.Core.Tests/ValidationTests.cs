using Thermalith.Core.Fonts;
using Thermalith.Core.Model;
using Thermalith.Core.Validation;
using Xunit;

namespace Thermalith.Core.Tests;

public class ValidationTests
{
    private static readonly LabelValidator Validator = new();

    private static LabelDocument Doc(params LabelElement[] elements) => new()
    {
        Metadata = new LabelMetadata { Name = "v" },
        Canvas = new Canvas { WidthMm = 40, HeightMm = 30, Dpi = 203 },
        Elements = [.. elements],
    };

    [Fact]
    public void Flags_duplicate_ids_as_errors()
    {
        var doc = Doc(
            new TextElement { Id = "dup", X = 1, Y = 1, W = 5, H = 5, Props = new TextProps { Content = "a" } },
            new TextElement { Id = "dup", X = 1, Y = 8, W = 5, H = 5, Props = new TextProps { Content = "b" } });

        var result = Validator.Validate(doc);
        Assert.True(result.HasErrors);
        Assert.Contains(result.Diagnostics, d => d.Code == ValidationCodes.DuplicateId);
    }

    [Fact]
    public void Flags_element_outside_canvas_as_error()
    {
        var doc = Doc(new TextElement { Id = "x", X = 35, Y = 1, W = 20, H = 5, Props = new TextProps { Content = "a" } });

        var result = Validator.Validate(doc);
        Assert.Contains(result.Diagnostics, d => d.Code == ValidationCodes.ElementOutsideCanvas && d.Severity == ValidationSeverity.Error);
    }

    [Fact]
    public void Flags_content_outside_safe_area_as_warning_not_error()
    {
        var doc = Doc(new TextElement { Id = "x", X = 0.5, Y = 0.5, W = 10, H = 5, Props = new TextProps { Content = "a" } })
            with { Canvas = new Canvas { WidthMm = 40, HeightMm = 30, Dpi = 203, SafeAreaInsetMm = 2 } };

        var result = Validator.Validate(doc);
        Assert.Contains(result.Diagnostics, d => d.Code == ValidationCodes.ElementOutsideSafeArea && d.Severity == ValidationSeverity.Warning);
        Assert.False(result.HasErrors);
    }

    [Fact]
    public void Flags_barcode_module_too_small_as_warning()
    {
        // 0.1 mm × 7.99 px/mm ≈ 0.8 px < 1 px.
        var doc = Doc(new BarcodeElement { Id = "bc", X = 1, Y = 1, W = 30, H = 8,
            Props = new BarcodeProps { Symbology = "code128", Value = "ABC", ModuleWidthMm = 0.1 } });

        var result = Validator.Validate(doc);
        Assert.Contains(result.Diagnostics, d => d.Code == ValidationCodes.BarcodeModuleTooSmall && d.Severity == ValidationSeverity.Warning);
    }

    [Fact]
    public void Flags_undeclared_token_and_required_unresolved()
    {
        var doc = Doc(new TextElement { Id = "t", X = 1, Y = 1, W = 30, H = 5, Props = new TextProps { Content = "{name}" } })
            with { Tokens = [new TokenDecl { Name = "name", Required = true }] };

        // No data supplied → required "name" cannot resolve, and it IS declared (so no undeclared warning).
        var result = Validator.Validate(doc);
        Assert.Contains(result.Diagnostics, d => d.Code == ValidationCodes.RequiredTokenUnresolved && d.Severity == ValidationSeverity.Error);
        Assert.DoesNotContain(result.Diagnostics, d => d.Code == ValidationCodes.UndeclaredToken);
    }

    [Fact]
    public void Required_token_resolves_when_data_is_supplied()
    {
        var doc = Doc(new TextElement { Id = "t", X = 1, Y = 1, W = 30, H = 5, Props = new TextProps { Content = "{name}" } })
            with { Tokens = [new TokenDecl { Name = "name", Required = true }] };

        var ctx = new ValidationContext { Data = new Dictionary<string, object?> { ["name"] = "Widget" } };
        var result = Validator.Validate(doc, ctx);
        Assert.DoesNotContain(result.Diagnostics, d => d.Code == ValidationCodes.RequiredTokenUnresolved);
    }

    [Fact]
    public void Flags_undeclared_token_as_warning()
    {
        var doc = Doc(new TextElement { Id = "t", X = 1, Y = 1, W = 30, H = 5, Props = new TextProps { Content = "{ghost}" } });

        var result = Validator.Validate(doc);
        Assert.Contains(result.Diagnostics, d => d.Code == ValidationCodes.UndeclaredToken && d.Severity == ValidationSeverity.Warning);
    }

    [Fact]
    public void Flags_missing_asset_when_asset_set_is_supplied()
    {
        var doc = Doc(new ImageElement { Id = "img", X = 1, Y = 1, W = 8, H = 8, Props = new ImageProps { AssetId = "missing" } });

        var ctx = new ValidationContext { Assets = new Dictionary<string, byte[]>() };
        var result = Validator.Validate(doc, ctx);
        Assert.Contains(result.Diagnostics, d => d.Code == ValidationCodes.MissingAsset && d.Severity == ValidationSeverity.Error);
    }

    [Fact]
    public void Clean_label_produces_no_errors()
    {
        var doc = Doc(
            new TextElement { Id = "t", X = 2, Y = 2, W = 30, H = 6, Props = new TextProps { Content = "Hello", FontFamily = FontService.BundledFamily } },
            new ShapeElement { Id = "s", X = 2, Y = 10, W = 30, H = 0, Props = new ShapeProps { ShapeType = "line" } });

        using var fonts = new FontService();
        var result = Validator.Validate(doc, new ValidationContext { Fonts = fonts });
        Assert.False(result.HasErrors);
    }
}
