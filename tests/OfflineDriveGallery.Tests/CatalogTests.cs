using OfflineDriveGallery.Core;

namespace OfflineDriveGallery.Tests;

[TestClass]
public sealed class CatalogTests
{
    private sealed class FakeThumbnails : IThumbnailProvider
    {
        public int Calls { get; private set; }
        public byte[]? MakeThumbnail(string path)
        {
            Calls++;
            return Path.GetExtension(path) == ".png" ? [1, 2, 3] : null;
        }
    }

    [TestMethod]
    public void Catalog_Remains_Searchable_When_Source_Is_Unavailable_And_Rescan_Prunes_Missing()
    {
        var temp = Path.Combine(Path.GetTempPath(), "odg-test-" + Guid.NewGuid().ToString("N"));
        var root = Path.Combine(temp, "drive");
        Directory.CreateDirectory(Path.Combine(root, "photos"));
        try
        {
            File.WriteAllText(Path.Combine(root, "photos", "holiday.png"), "image bytes");
            File.WriteAllText(Path.Combine(root, "notes.txt"), "notes");
            var catalog = new Catalog(Path.Combine(temp, "catalog.sqlite"));
            catalog.Initialize();
            var thumbnails = new FakeThumbnails();

            var first = catalog.Scan(root, "drive-1", "Holiday disk", thumbnails);
            Assert.IsTrue(first.Complete);
            Assert.AreEqual(2, first.FilesSeen);
            Assert.AreEqual(1, first.ThumbnailsMade);
            Assert.HasCount(1, catalog.Search("holiday", mediaOnly: true));
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, catalog.Search("holiday")[0].Thumbnail);

            var callsBefore = thumbnails.Calls;
            catalog.Scan(root, "drive-1", "Holiday disk", thumbnails);
            Assert.AreEqual(callsBefore + 1, thumbnails.Calls); // Non-media file has no cached thumbnail.

            Directory.Delete(root, true);
            Assert.HasCount(2, catalog.Search(""));
            Assert.AreEqual(2, catalog.Volumes()[0].FileCount);

            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "new.txt"), "new");
            catalog.Scan(root, "drive-1", "Holiday disk", thumbnails);
            Assert.HasCount(1, catalog.Search(""));
            Assert.AreEqual("new.txt", catalog.Search("")[0].Name);
        }
        finally { Directory.Delete(temp, true); }
    }

    [TestMethod]
    public void Cancelled_Scan_Rolls_Back_Previous_Catalog()
    {
        var temp = Path.Combine(Path.GetTempPath(), "odg-test-" + Guid.NewGuid().ToString("N"));
        var root = Path.Combine(temp, "drive");
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "original.txt"), "a");
            var catalog = new Catalog(Path.Combine(temp, "catalog.sqlite"));
            catalog.Initialize();
            catalog.Scan(root, "drive-1", "Disk", new FakeThumbnails());
            File.WriteAllText(Path.Combine(root, "added.png"), "b");
            using var cancel = new CancellationTokenSource();
            var provider = new CancelOnThumbnail(cancel);

            Assert.ThrowsExactly<OperationCanceledException>(() =>
                catalog.Scan(root, "drive-1", "Disk", provider, cancellationToken: cancel.Token));
            var results = catalog.Search("");
            Assert.HasCount(1, results);
            Assert.AreEqual("original.txt", results[0].Name);
        }
        finally { Directory.Delete(temp, true); }
    }

    private sealed class CancelOnThumbnail(CancellationTokenSource cancellation) : IThumbnailProvider
    {
        public byte[]? MakeThumbnail(string path)
        {
            if (Path.GetExtension(path) == ".png") cancellation.Cancel();
            return null;
        }
    }
}
