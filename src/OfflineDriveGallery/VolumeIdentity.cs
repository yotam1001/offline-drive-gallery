using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace OfflineDriveGallery;

internal static class VolumeIdentity
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool GetVolumeNameForVolumeMountPoint(string mountPoint, StringBuilder volumeName, int bufferLength);

    public static string ForRoot(string root)
    {
        var full = Path.GetFullPath(root);
        var mount = Path.GetPathRoot(full)!;
        if (full.Length > mount.Length) full = full.TrimEnd(Path.DirectorySeparatorChar);
        var buffer = new StringBuilder(100);
        var stable = GetVolumeNameForVolumeMountPoint(mount, buffer, buffer.Capacity)
            ? buffer.ToString().ToUpperInvariant()
            : mount.ToUpperInvariant();
        var withinVolume = Path.GetRelativePath(mount, full).ToUpperInvariant();
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(stable + "|" + withinVolume)));
    }
}
