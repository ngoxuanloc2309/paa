using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using SimplePLC.Studio.Controls;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

/// <summary>
/// Bộ kiểm thử TDD tái hiện lỗi hiệu năng và tác dụng phụ khi chuyển Tab (PLAN-PERF-TAB-01).
/// Tuân thủ nguyên tắc Red -> Green -> Refactor theo học thuyết /synaptix-qa-tdd.
/// </summary>
public class TabPerformanceAndSideEffectReproductionTests
{
    private sealed class TestViewModelA { }
    private sealed class TestViewModelB { }

    private static void RunOnSta(Action action)
    {
        var tcs = new TaskCompletionSource<bool>();
        var thread = new Thread(() =>
        {
            try
            {
                action();
                tcs.SetResult(true);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        tcs.Task.GetAwaiter().GetResult();
    }

    [Fact]
    public void SelectTab_WhenSwitchingBetweenNonDeviceTabs_DoesNotMarkProjectDirty()
    {
        // Arrange
        var mainVM = new MainViewModel();
        mainVM.SelectTab(AppTabIndices.LogicGraph);
        mainVM.IsProjectDirty = false;

        // Act 1: Chuyển từ Logic Graph (1) sang Tag Catalog (2)
        mainVM.SelectTab(AppTabIndices.TagCatalog);

        // Assert 1: Dự án KHÔNG được phép bị đánh dấu Dirty khi chỉ chuyển xem Tab
        Assert.False(mainVM.IsProjectDirty, "Chuyển từ LogicGraph sang TagCatalog không được làm bẩn dự án (IsProjectDirty)");

        // Act 2: Chuyển từ Tag Catalog (2) sang Rule Table (3)
        mainVM.SelectTab(AppTabIndices.RuleTable);

        // Assert 2: Dự án vẫn KHÔNG được phép bị đánh dấu Dirty
        Assert.False(mainVM.IsProjectDirty, "Chuyển từ TagCatalog sang RuleTable không được làm bẩn dự án (IsProjectDirty)");

        // Act 3: Chuyển sang Blueprints (4) rồi Deploy (5)
        mainVM.SelectTab(AppTabIndices.Blueprints);
        Assert.False(mainVM.IsProjectDirty, "Chuyển sang Blueprints không được làm bẩn dự án");

        mainVM.SelectTab(AppTabIndices.Deploy);
        Assert.False(mainVM.IsProjectDirty, "Chuyển sang Deploy không được làm bẩn dự án");
    }

    [Fact]
    public void TagCatalog_FindTagByIndex_ReturnsCorrectTagDirectly()
    {
        // Arrange
        var tagCatalogVM = new TagCatalogViewModel();
        var targetTag = tagCatalogVM.AllTags[5];

        // Act
        var foundTag = tagCatalogVM.FindTagByIndex(targetTag.Index);

        // Assert
        Assert.NotNull(foundTag);
        Assert.Same(targetTag, foundTag);
    }

    [Fact]
    public void MainViewModel_ProjectAliasProvider_ResolvesAliasesFastWithoutLinearScan()
    {
        // Arrange
        var mainVM = new MainViewModel();
        var testTag = mainVM.TagCatalogVM.AllTags[1];
        testTag.Alias = "Sensor_Temp_01";

        // Act
        var alias = mainVM.LiveWatchVM.ProjectAliasProvider?.Invoke((ushort)testTag.Index);

        // Assert
        Assert.Equal("Sensor_Temp_01", alias);
    }

    [Fact]
    public void CachedContentPresenter_WhenSwitchingContent_PersistsAndReusesViewInstances()
    {
        RunOnSta(() =>
        {
            var presenter = new CachedContentPresenter();
            presenter.CustomViewFactory = (content, type) => type switch
            {
                _ when type == typeof(TestViewModelA) => new Border { Name = "ViewA" },
                _ when type == typeof(TestViewModelB) => new Grid { Name = "ViewB" },
                _ => new ContentControl()
            };

            var vmA = new TestViewModelA();
            var vmB = new TestViewModelB();

            // 1. Kích hoạt View lần đầu: khởi tạo lazy 1 child duy nhất
            presenter.Content = vmA;
            Assert.Single(presenter.Children);
            var view1 = presenter.Children[0];
            Assert.Equal(Visibility.Visible, view1.Visibility);

            // 2. Kích hoạt View thứ hai: ẩn view1, tạo view2
            presenter.Content = vmB;
            Assert.Equal(2, presenter.Children.Count);
            Assert.Equal(Visibility.Collapsed, view1.Visibility);
            var view2 = presenter.Children[1];
            Assert.Equal(Visibility.Visible, view2.Visibility);

            // 3. Chuyển NGƯỢC LẠI về View thứ nhất:
            // Phải tái sử dụng 100% instance cũ, KHÔNG sinh thêm instance mới (Children.Count giữ nguyên 2)
            presenter.Content = vmA;
            Assert.Equal(2, presenter.Children.Count);
            Assert.Same(view1, presenter.Children[0]);
            Assert.Equal(Visibility.Visible, view1.Visibility);
            Assert.Equal(Visibility.Collapsed, view2.Visibility);
        });
    }
}
