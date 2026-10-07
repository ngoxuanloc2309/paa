using System.IO;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

public class TagCsvServiceTests
{
    [Fact]
    public void ExportAndImportTags_Roundtrip_PreservesData()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"tags_{Guid.NewGuid():N}.csv");
        try
        {
            var catalog = new TagCatalogViewModel();
            catalog.AllTags.Clear();

            catalog.AllTags.Add(new TagModel { Index = 1, Name = "DI0", Alias = "Cảm biến cửa lùa", Kind = TagKind.DiscreteInput, Channel = 1, Group = "DI", Value = 1 });
            catalog.AllTags.Add(new TagModel { Index = 9, Name = "DO0", Alias = "Đèn cảnh báo \"Cấp 1\"", Kind = TagKind.DiscreteOutput, Channel = 1, Group = "DO", Value = 0 });

            // Act: Export
            TagCsvService.ExportCsv(tempFile, catalog.AllTags);
            Assert.True(File.Exists(tempFile));

            // Act: Import into a new catalog
            var newCatalog = new TagCatalogViewModel();
            newCatalog.AllTags.Clear();

            var (updated, added, errors) = TagCsvService.ImportCsv(tempFile, newCatalog);

            // Assert
            Assert.Empty(errors);
            Assert.Equal(2, added);
            Assert.Equal(2, newCatalog.AllTags.Count);

            var di = newCatalog.AllTags.First(t => t.Name == "DI0");
            Assert.Equal("Cảm biến cửa lùa", di.Alias);
            Assert.Equal(TagKind.DiscreteInput, di.Kind);
            Assert.Equal(1, di.Channel);
            Assert.Equal(1, di.Value);

            var @do = newCatalog.AllTags.First(t => t.Name == "DO0");
            Assert.Equal("Đèn cảnh báo \"Cấp 1\"", @do.Alias);
            Assert.Equal(TagKind.DiscreteOutput, @do.Kind);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }
}
