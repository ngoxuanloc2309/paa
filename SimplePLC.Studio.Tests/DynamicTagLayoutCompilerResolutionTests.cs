using System.Linq;
using SimplePLC.Domain.Builders;
using SimplePLC.Domain.Enums;
using SimplePLC.Domain.Models;
using SimplePLC.Protocol.Models;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

public class DynamicTagLayoutCompilerResolutionTests
{
    [Fact]
    public void Compile_WithDynamicLayout4Di4Do_CorrectlyMapsDO0WithoutReadOnlyError()
    {
        // Layout: 4 DI (0..3), 4 DO (4..7)
        var layout = new TagLayoutMap(
            diCount: 4,
            doCount: 4,
            aiCount: 2,
            vflagCount: 8,
            vregCount: 8,
            vregRetainCount: 4,
            counterCount: 2);

        var catalog = new TagCatalogViewModel();
        catalog.RebuildFromLayout(layout);

        var di0 = catalog.AllTags.First(t => t.Name == "DI0");
        var do0 = catalog.AllTags.First(t => t.Name == "DO0");

        // Xác nhận DO0 có index = 4 trong layout 4DI/4DO
        Assert.Equal(4, do0.Index);

        var input = new InputNodeViewModel(di0);
        var trigger = new TriggerNodeViewModel();
        var action = new ActionNodeViewModel(do0);

        var connections = new[]
        {
            new ConnectionViewModel(input.OutputConnectors[0], trigger.InputConnectors[0]),
            new ConnectionViewModel(trigger.OutputConnectors[0], action.InputConnectors[0])
        };

        var compiler = new RuleCompiler();
        var result = compiler.Compile(new GraphNodeViewModel[] { input, trigger, action }, connections, catalog);

        // Biên dịch phải thành công, không báo lỗi Cannot write to read-only tag 'DI4'
        Assert.True(result.IsValid, string.Join("; ", result.Diagnostics.Select(d => d.Message)));
        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.Single(result.Rules);
        Assert.Equal(4, result.Rules[0].ActionTag?.Index);
    }

    [Fact]
    public void Compile_WithMismatchedProductOffset_ResolvesTagByNameSafely()
    {
        // Giả sử catalog đang ở layout 4DI/4DO (DO0 = index 4)
        var layout = new TagLayoutMap(
            diCount: 4,
            doCount: 4,
            aiCount: 2,
            vflagCount: 8,
            vregCount: 8,
            vregRetainCount: 4,
            counterCount: 2);

        var catalog = new TagCatalogViewModel();
        catalog.RebuildFromLayout(layout);

        var di0 = catalog.AllTags.First(t => t.Name == "DI0");
        var do0 = catalog.AllTags.First(t => t.Name == "DO0");

        var input = new InputNodeViewModel(di0);
        var trigger = new TriggerNodeViewModel();
        var action = new ActionNodeViewModel(do0);

        var connections = new[]
        {
            new ConnectionViewModel(input.OutputConnectors[0], trigger.InputConnectors[0]),
            new ConnectionViewModel(trigger.OutputConnectors[0], action.InputConnectors[0])
        };

        // Nhưng thiết bị đích thực tế là RemoteIO 8DI/8DO (trong đó index 4 là DI4 read-only, DO0 là index 8)
        var product8Di = ProductDefinition.CreateRemoteIo8Di8Do4Ai();

        var compiler = new RuleCompiler();
        var result = compiler.Compile(
            new GraphNodeViewModel[] { input, trigger, action },
            connections,
            catalog,
            product: product8Di);

        // Nhờ ResolveTagIndex, tag DO0 được tự động đối chiếu sang index 8 của product8Di
        Assert.True(result.IsValid, string.Join("; ", result.Diagnostics.Select(d => d.Message)));
        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.Single(result.Rules);
        Assert.Equal(8, result.Rules[0].ActionTag?.Index);
    }
}
