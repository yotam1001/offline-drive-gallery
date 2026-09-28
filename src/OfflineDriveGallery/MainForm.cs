using System.Diagnostics;
using OfflineDriveGallery.Core;

namespace OfflineDriveGallery;

public sealed class MainForm : Form
{
    private readonly Catalog catalog;
    private readonly TextBox search = new();
    private readonly CheckBox mediaOnly = new();
    private readonly ListBox drives = new();
    private readonly ListView gallery = new();
    private readonly PictureBox preview = new();
    private readonly Label details = new();
    private readonly Button addButton = new();
    private readonly Button rescanButton = new();
    private readonly Button cancelButton = new();
    private readonly Button previousButton = new();
    private readonly Button nextButton = new();
    private readonly Button locateButton = new();
    private readonly ToolStripStatusLabel status = new();
    private readonly System.Windows.Forms.Timer searchDelay = new() { Interval = 250 };
    private CancellationTokenSource? scanCancellation;
    private CatalogItem? selectedItem;
    private int page;

    private sealed record DriveOption(string? Id, string Label)
    {
        public override string ToString() => Label;
    }

    public MainForm(string? databasePath = null)
    {
        Text = "Offline Drive Gallery";
        MinimumSize = new Size(980, 640);
        Size = new Size(1240, 760);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(246, 248, 251);
        Font = new Font("Segoe UI", 10);

        var dataPath = databasePath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OfflineDriveGallery", "catalog.sqlite");
        catalog = new Catalog(dataPath);
        catalog.Initialize();

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(0) };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 106));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        Controls.Add(layout);

        var top = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(25, 39, 61) };
        layout.Controls.Add(top, 0, 0);
        var title = new Label { Text = "OFFLINE DRIVE GALLERY", ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 18, FontStyle.Bold), AutoSize = true, Location = new Point(18, 10) };
        top.Controls.Add(title);
        var subtitle = new Label { Text = "Find your files and media even when the drive is unplugged",
            ForeColor = Color.FromArgb(185, 203, 224), AutoSize = true, Location = new Point(21, 48) };
        top.Controls.Add(subtitle);
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 37, Padding = new Padding(16, 2, 12, 0),
            BackColor = Color.FromArgb(232, 237, 244), WrapContents = false };
        top.Controls.Add(toolbar);
        search.Width = 315;
        search.PlaceholderText = "Search file names and folders";
        search.AccessibleName = "Search file names and folders";
        toolbar.Controls.Add(search);
        mediaOnly.Text = "Media only";
        mediaOnly.Checked = true;
        mediaOnly.AutoSize = true;
        mediaOnly.Margin = new Padding(12, 6, 14, 0);
        toolbar.Controls.Add(mediaOnly);
        ConfigureButton(addButton, "Add folder", toolbar);
        ConfigureButton(rescanButton, "Rescan", toolbar);
        ConfigureButton(cancelButton, "Cancel", toolbar);
        cancelButton.Enabled = false;
        ConfigureButton(previousButton, "‹ Previous", toolbar);
        ConfigureButton(nextButton, "Next ›", toolbar);

        var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1,
            Padding = new Padding(12, 12, 12, 10) };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 282));
        layout.Controls.Add(body, 0, 1);

        var sidebar = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(10) };
        sidebar.Controls.Add(new Label { Text = "CATALOGED DRIVES", Dock = DockStyle.Top, Height = 32,
            Font = new Font("Segoe UI Semibold", 10, FontStyle.Bold), ForeColor = Color.FromArgb(65, 80, 105) });
        drives.Dock = DockStyle.Fill;
        drives.AccessibleName = "Cataloged drives";
        drives.BorderStyle = BorderStyle.None;
        drives.ItemHeight = 28;
        sidebar.Controls.Add(drives);
        drives.BringToFront();
        body.Controls.Add(sidebar, 0, 0);

        var galleryPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(8) };
        gallery.Dock = DockStyle.Fill;
        gallery.AccessibleName = "Cataloged files";
        gallery.View = View.LargeIcon;
        gallery.MultiSelect = false;
        gallery.HideSelection = false;
        gallery.BorderStyle = BorderStyle.None;
        gallery.LargeImageList = NewImageList();
        galleryPanel.Controls.Add(gallery);
        body.Controls.Add(galleryPanel, 1, 0);

        var info = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(12) };
        var infoLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1 };
        infoLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        infoLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 194));
        infoLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        infoLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        infoLayout.Controls.Add(new Label { Text = "DETAILS", Dock = DockStyle.Fill,
            Font = new Font("Segoe UI Semibold", 10, FontStyle.Bold), ForeColor = Color.FromArgb(65, 80, 105) }, 0, 0);
        preview.Dock = DockStyle.Fill;
        preview.SizeMode = PictureBoxSizeMode.Zoom;
        preview.BackColor = Color.FromArgb(239, 243, 248);
        infoLayout.Controls.Add(preview, 0, 1);
        details.Dock = DockStyle.Fill;
        details.Padding = new Padding(0, 14, 0, 0);
        details.AutoEllipsis = true;
        details.Text = "Select a file to see its details.";
        infoLayout.Controls.Add(details, 0, 2);
        ConfigureButton(locateButton, "Show in Explorer", infoLayout);
        locateButton.Dock = DockStyle.Fill;
        locateButton.Enabled = false;
        info.Controls.Add(infoLayout);
        body.Controls.Add(info, 2, 0);

        var statusBar = new StatusStrip { Dock = DockStyle.Fill, SizingGrip = false };
        statusBar.Items.Add(status);
        status.Text = "Ready";
        layout.Controls.Add(statusBar, 0, 2);

        search.TextChanged += (_, _) => { page = 0; searchDelay.Stop(); searchDelay.Start(); };
        searchDelay.Tick += (_, _) => { searchDelay.Stop(); RefreshGallery(); };
        mediaOnly.CheckedChanged += (_, _) => { page = 0; RefreshGallery(); };
        drives.SelectedIndexChanged += (_, _) => { page = 0; UpdateRescanButton(); RefreshGallery(); };
        gallery.SelectedIndexChanged += (_, _) => ShowSelected();
        addButton.Click += async (_, _) => await AddFolder();
        rescanButton.Click += async (_, _) => await RescanSelected();
        cancelButton.Click += (_, _) => scanCancellation?.Cancel();
        previousButton.Click += (_, _) => { if (page > 0) { page--; RefreshGallery(); } };
        nextButton.Click += (_, _) => { page++; RefreshGallery(); };
        locateButton.Click += (_, _) => ShowInExplorer();
        RefreshDrives();
        RefreshGallery();
    }

    private static void ConfigureButton(Button button, string text, Control parent)
    {
        button.Text = text;
        button.AutoSize = true;
        button.FlatStyle = FlatStyle.Flat;
        button.BackColor = Color.White;
        button.ForeColor = Color.FromArgb(31, 55, 85);
        button.Margin = new Padding(3, 1, 3, 0);
        parent.Controls.Add(button);
    }

    private static ImageList NewImageList() => new() { ImageSize = new Size(128, 92), ColorDepth = ColorDepth.Depth32Bit };

    private void RefreshDrives(string? selectId = null)
    {
        var currentId = selectId ?? (drives.SelectedItem as DriveOption)?.Id;
        drives.BeginUpdate();
        drives.Items.Clear();
        drives.Items.Add(new DriveOption(null, "All drives"));
        foreach (var volume in catalog.Volumes())
            drives.Items.Add(new DriveOption(volume.Id, $"{volume.Name}  ·  {volume.FileCount:N0} files" +
                (volume.Warnings > 0 ? "  ⚠" : "")));
        drives.SelectedIndex = Math.Max(0, drives.Items.Cast<DriveOption>().ToList().FindIndex(x => x.Id == currentId));
        drives.EndUpdate();
        UpdateRescanButton();
    }

    private void UpdateRescanButton()
    {
        var id = (drives.SelectedItem as DriveOption)?.Id;
        var volume = id is null ? null : catalog.Volumes().FirstOrDefault(x => x.Id == id);
        rescanButton.Enabled = scanCancellation is null && volume is not null && Directory.Exists(volume.LastPath);
    }

    private void RefreshGallery()
    {
        var id = (drives.SelectedItem as DriveOption)?.Id;
        IReadOnlyList<CatalogItem> items;
        try { items = catalog.Search(search.Text, id, mediaOnly.Checked, 181, page * 180); }
        catch (Exception ex) { status.Text = $"Search failed: {ex.Message}"; return; }
        var hasMore = items.Count > 180;
        var visibleItems = items.Take(180).ToArray();

        gallery.BeginUpdate();
        gallery.Items.Clear();
        var oldImages = gallery.LargeImageList;
        gallery.LargeImageList = NewImageList();
        oldImages?.Dispose();
        int index = 0;
        foreach (var item in visibleItems)
        {
            Image image;
            try
            {
                if (item.Thumbnail is null) image = Placeholder(item.Extension);
                else
                {
                    using var stream = new MemoryStream(item.Thumbnail);
                    using var source = Image.FromStream(stream);
                    image = new Bitmap(source);
                }
            }
            catch { image = Placeholder(item.Extension); }
            gallery.LargeImageList.Images.Add(image);
            image.Dispose();
            gallery.Items.Add(new ListViewItem(item.Name, index++) { Tag = item, ToolTipText = item.RelativePath });
        }
        gallery.EndUpdate();
        selectedItem = null;
        preview.Image?.Dispose();
        preview.Image = null;
        details.Text = visibleItems.Length == 0 ? "No matching files. Add a folder or change the search." :
            "Select a file to see its details.";
        locateButton.Enabled = false;
        previousButton.Enabled = page > 0;
        nextButton.Enabled = hasMore;
        status.Text = visibleItems.Length == 0 ? "No matching files" :
            $"Showing {page * 180 + 1:N0}–{page * 180 + visibleItems.Length:N0}" + (hasMore ? " · more available" : "");
    }

    private static Bitmap Placeholder(string extension)
    {
        var bitmap = new Bitmap(128, 92);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.FromArgb(221, 230, 241));
        using var brush = new SolidBrush(Color.FromArgb(65, 91, 121));
        using var font = new Font("Segoe UI Semibold", 14, FontStyle.Bold);
        var text = string.IsNullOrEmpty(extension) ? "FILE" : extension.TrimStart('.').ToUpperInvariant();
        if (text.Length > 7) text = text[..7];
        var box = new RectangleF(4, 0, 120, 92);
        using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        graphics.DrawString(text, font, brush, box, format);
        return bitmap;
    }

    private void ShowSelected()
    {
        selectedItem = gallery.SelectedItems.Count == 0 ? null : gallery.SelectedItems[0].Tag as CatalogItem;
        preview.Image?.Dispose();
        preview.Image = null;
        if (selectedItem is null) { locateButton.Enabled = false; return; }
        if (selectedItem.Thumbnail is not null)
        {
            try
            {
                using var stream = new MemoryStream(selectedItem.Thumbnail);
                using var source = Image.FromStream(stream);
                preview.Image = new Bitmap(source);
            }
            catch { /* Invalid or unsupported stored image; details still work. */ }
        }
        var lastChanged = new DateTime(selectedItem.ModifiedTicks, DateTimeKind.Utc).ToLocalTime();
        var fullPath = Path.Combine(selectedItem.LastRootPath, selectedItem.RelativePath);
        details.Text = $"{selectedItem.Name}\n\nDrive: {selectedItem.VolumeName}\n" +
            $"Folder: {Path.GetDirectoryName(selectedItem.RelativePath)}\n" +
            $"Size: {selectedItem.Size:N0} bytes\nModified: {lastChanged:g}\n\n" +
            (File.Exists(fullPath) ? "Drive connected" : "Drive unavailable at its last path");
        locateButton.Enabled = File.Exists(fullPath);
    }

    private async Task AddFolder()
    {
        using var picker = new FolderBrowserDialog { Description = "Choose a drive or folder to catalog",
            ShowNewFolderButton = false, UseDescriptionForTitle = true };
        if (picker.ShowDialog(this) != DialogResult.OK) return;
        var path = picker.SelectedPath;
        string name;
        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(path)!);
            name = string.IsNullOrWhiteSpace(drive.VolumeLabel)
                ? (Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar)) is { Length: > 0 } folder ? folder : drive.Name)
                : drive.VolumeLabel;
        }
        catch { name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar)); }
        await ScanFolder(path, VolumeIdentity.ForRoot(path), name);
    }

    private async Task RescanSelected()
    {
        var id = (drives.SelectedItem as DriveOption)?.Id;
        var volume = id is null ? null : catalog.Volumes().FirstOrDefault(x => x.Id == id);
        if (volume is not null && Directory.Exists(volume.LastPath))
            await ScanFolder(volume.LastPath, volume.Id, volume.Name);
    }

    private async Task ScanFolder(string path, string id, string name)
    {
        if (scanCancellation is not null) return;
        scanCancellation = new CancellationTokenSource();
        addButton.Enabled = false;
        rescanButton.Enabled = false;
        cancelButton.Enabled = true;
        status.Text = $"Scanning {name}...";
        var progress = new Progress<ScanProgress>(p =>
        {
            if (scanCancellation is not null)
                status.Text = $"Scanning {name}: {p.FilesSeen:N0} files, {p.ThumbnailsMade:N0} thumbnails";
        });
        var token = scanCancellation.Token;
        string completionStatus = "Scan finished";
        try
        {
            // Shell thumbnail handlers can require STA. Keep extraction off the UI thread.
            var completion = new TaskCompletionSource<ScanResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            var worker = new Thread(() =>
            {
                try { completion.SetResult(catalog.Scan(path, id, name, new ShellThumbnails(), progress, token)); }
                catch (Exception ex) { completion.SetException(ex); }
            }) { IsBackground = true, Name = "Catalog scan" };
            worker.SetApartmentState(ApartmentState.STA);
            worker.Start();
            var result = await completion.Task;
            completionStatus = result.Complete
                ? $"Scan complete: {result.FilesSeen:N0} files, {result.ThumbnailsMade:N0} new thumbnails"
                : $"Scan finished with {result.Warnings} unreadable entries; old catalog entries were kept";
        }
        catch (OperationCanceledException) { completionStatus = "Scan canceled; previous catalog preserved"; }
        catch (Exception ex) { completionStatus = $"Scan failed: {ex.Message}"; }
        finally
        {
            scanCancellation.Dispose();
            scanCancellation = null;
            addButton.Enabled = true;
            cancelButton.Enabled = false;
            RefreshDrives(id);
            RefreshGallery();
            status.Text = completionStatus;
        }
    }

    private void ShowInExplorer()
    {
        if (selectedItem is null) return;
        var fullPath = Path.Combine(selectedItem.LastRootPath, selectedItem.RelativePath);
        if (!File.Exists(fullPath)) return;
        Process.Start(new ProcessStartInfo("explorer.exe") { Arguments = $"/select,\"{fullPath}\"", UseShellExecute = true });
    }
}
