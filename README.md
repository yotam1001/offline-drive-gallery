# Offline Drive Gallery

Search files on disconnected drives and browse stored media thumbnails in a local Windows app. Add a drive or folder once, then search its catalog after unplugging it. The app reads source files; it does not move, rename, or delete them.

**[Project page](https://yotam1001.github.io/offline-drive-gallery/)** · **[Latest Windows download](https://github.com/yotam1001/offline-drive-gallery/releases/latest/download/OfflineDriveGallery-win-x64.zip)**

## Why this exists

People with shelves of drives want to find a file without plugging in each drive. A [2026 request](https://www.reddit.com/r/DataHoarder/comments/1vhgdhs/tool_to_read_disks_and_create_a_catalog/) described searching five drives one at a time. A [2023 request](https://www.reddit.com/r/DataHoarder/comments/18rex5c/pc_media_cataloging_tool_used_to_be_simple/) specifically asked for video thumbnails to identify poorly named media on offline drives.

Software already exists for this job. [VVV](https://www.fsoft.it/VVV/) is a free, capable offline drive cataloger; people in the second discussion pointed out its lack of thumbnails. [WinCatalog](https://www.wincatalog.com/disk-catalog.html) includes thumbnails and has a paid license after its trial. Offline Drive Gallery focuses on a simple, free Windows gallery with locally stored thumbnails.

## Use

1. Download the release ZIP and extract it. Run `OfflineDriveGallery.exe`. No .NET installation, account, or server is needed.
2. Select **Add folder** and choose a drive or folder. The scan records file names, relative paths, sizes, modification times, and available thumbnails.
3. Unplug the drive. Search by file name or folder path, filter to media, and page through results. Select an item for its saved thumbnail and details.
4. Reconnect the drive and select **Rescan** to refresh the catalog. If its drive letter changed, use **Add folder** on the drive again; Windows' volume identity lets the app update the same catalog on ordinary local volumes.

The catalog database is stored at `%LOCALAPPDATA%\OfflineDriveGallery\catalog.sqlite`. It contains full former drive paths and thumbnails. Keep that database private if your file names or images are sensitive. Delete the database to remove all catalogs.

## What it handles

- Scans regular files recursively and skips directory junctions and other reparse points, so it cannot wander outside the selected folder through links.
- Uses Windows Shell thumbnails for common image and video types. A format without a Windows thumbnail handler gets a labeled placeholder. The app does not download codecs.
- Runs scans in the background. **Cancel** rolls back the in-progress scan. If entries are unreadable, the app retains previously indexed entries rather than removing them as missing.
- Shows up to 180 results per page. Search is over file and folder names; it does not inspect media contents, OCR images, or index document text.

## Limits

- A catalog is a snapshot. Search results can be stale until the drive is rescanned, and **Show in Explorer** works only while the file is reachable at its last scanned path.
- The app identifies ordinary local volumes across drive-letter changes. A network path without a stable Windows volume identity is identified by its path.
- A video thumbnail depends on the codecs and thumbnail handlers installed in Windows. MP4 thumbnail extraction was verified on the development machine; other codecs can show placeholders.
- Scanning very large drives, especially while extracting many thumbnails, can take time and consume catalog disk space. Search currently scans file and path text in SQLite, so very large catalogs may search more slowly.
- The executable is unsigned; Windows may show an unrecognized-app warning.

## Build and verify

Requires the .NET 10 SDK on Windows:

```powershell
dotnet test OfflineDriveGallery.slnx -c Release
dotnet publish src/OfflineDriveGallery/OfflineDriveGallery.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:DebugType=None -p:DebugSymbols=false -o artifacts/publish
```

The tests verify offline search, rescan cleanup, cancellation rollback, and a real Windows Shell thumbnail for PNG. The MP4 test generates a short local fixture with FFmpeg and checks its thumbnail when the test machine has both FFmpeg and an MP4 thumbnail handler; otherwise it is inconclusive. FFmpeg is not needed to run the app.

## Privacy and safety

The app makes no network requests and needs no administrator privileges. It reads the chosen folder, writes its own SQLite catalog under your Windows user profile, and launches Explorer only when you press **Show in Explorer**. The catalog is not encrypted. See [SECURITY.md](SECURITY.md) for reporting a security issue.

Contributions are welcome; see [CONTRIBUTING.md](CONTRIBUTING.md). Licensed under [MIT](LICENSE).
