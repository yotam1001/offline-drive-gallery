using System.Drawing.Imaging;
using System.Diagnostics;
using System.ComponentModel;

namespace OfflineDriveGallery.Tests;

[TestClass]
public sealed class ThumbnailTests
{
    [TestMethod]
    public void WindowsShell_Extracts_A_Real_Image_Thumbnail()
    {
        var file = Path.Combine(Path.GetTempPath(), "odg-thumb-" + Guid.NewGuid().ToString("N") + ".png");
        try
        {
            using (var bitmap = new Bitmap(320, 200))
            {
                using var graphics = Graphics.FromImage(bitmap);
                graphics.Clear(Color.CornflowerBlue);
                bitmap.Save(file, ImageFormat.Png);
            }
            var bytes = new ShellThumbnails().MakeThumbnail(file);
            Assert.IsNotNull(bytes);
            using var stream = new MemoryStream(bytes);
            using var extracted = Image.FromStream(stream);
            Assert.IsGreaterThan(0, extracted.Width);
            Assert.IsGreaterThan(0, extracted.Height);
        }
        finally { File.Delete(file); }
    }

    [TestMethod]
    public void WindowsShell_Extracts_A_Video_Thumbnail_When_Ffmpeg_Is_Available()
    {
        var file = Path.Combine(Path.GetTempPath(), "odg-video-" + Guid.NewGuid().ToString("N") + ".mp4");
        try
        {
            var start = new ProcessStartInfo("ffmpeg")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true
            };
            foreach (var argument in new[] { "-hide_banner", "-loglevel", "error", "-f", "lavfi", "-i",
                         "testsrc=size=320x200:rate=5", "-frames:v", "5", "-c:v", "mpeg4", "-y", file })
                start.ArgumentList.Add(argument);
            Process process;
            try { process = Process.Start(start)!; }
            catch (Win32Exception) { Assert.Inconclusive("ffmpeg is not installed for the video thumbnail test."); return; }
            using (process)
            {
                var error = process.StandardError.ReadToEnd();
                process.WaitForExit();
                Assert.AreEqual(0, process.ExitCode, error);
            }
            var bytes = new ShellThumbnails().MakeThumbnail(file);
            if (bytes is null)
            {
                Assert.Inconclusive("This Windows installation has no MP4 thumbnail handler.");
                return;
            }
            using var stream = new MemoryStream(bytes);
            using var extracted = Image.FromStream(stream);
            Assert.IsGreaterThan(0, extracted.Width);
        }
        finally { File.Delete(file); }
    }
}
