using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using OfflineDriveGallery.Core;

namespace OfflineDriveGallery;

public sealed class ShellThumbnails : IThumbnailProvider
{
    private static readonly HashSet<string> MediaExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tif", ".tiff", ".webp", ".heic",
        ".mp4", ".mov", ".mkv", ".avi", ".wmv", ".webm", ".m4v"
    };

    [ComImport, Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory
    {
        [PreserveSig]
        int GetImage(ShellSize size, uint flags, out IntPtr bitmap);
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct ShellSize(int width, int height)
    {
        public readonly int Width = width;
        public readonly int Height = height;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int SHCreateItemFromParsingName(string path, IntPtr bindContext,
        ref Guid interfaceId, [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory factory);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr handle);

    public byte[]? MakeThumbnail(string path)
    {
        if (!MediaExtensions.Contains(Path.GetExtension(path))) return null;
        IShellItemImageFactory? factory = null;
        IntPtr handle = IntPtr.Zero;
        try
        {
            var id = typeof(IShellItemImageFactory).GUID;
            if (SHCreateItemFromParsingName(path, IntPtr.Zero, ref id, out factory!) != 0) return null;
            // THUMBNAILONLY prevents a generic file icon from being stored as a media preview.
            if (factory.GetImage(new ShellSize(240, 160), 0x8, out handle) != 0 || handle == IntPtr.Zero) return null;
            using var image = Image.FromHbitmap(handle);
            using var stream = new MemoryStream();
            image.Save(stream, ImageFormat.Jpeg);
            return stream.ToArray();
        }
        catch (COMException) { return null; }
        catch (ExternalException) { return null; }
        catch (ArgumentException) { return null; }
        finally
        {
            if (handle != IntPtr.Zero) DeleteObject(handle);
            if (factory is not null) Marshal.ReleaseComObject(factory);
        }
    }
}
