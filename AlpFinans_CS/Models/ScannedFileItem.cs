using System;
using System.IO;

namespace AlpFinans.Models;

public class ScannedFileItem
{
    public string FullPath { get; set; } = string.Empty;
    public string FileName => Path.GetFileName(FullPath);
    public string Extension => Path.GetExtension(FullPath).ToLowerInvariant();
    public long FileSizeBytes { get; set; }
    public DateTime LastModified { get; set; }

    public string FormattedSize => FileSizeBytes switch
    {
        > 1048576 => $"{(FileSizeBytes / 1048576.0):N1} MB",
        > 1024 => $"{(FileSizeBytes / 1024.0):N1} KB",
        _ => $"{FileSizeBytes} B"
    };

    public string FormattedDate => LastModified.ToString("dd.MM.yyyy HH:mm");
    public string IconEmoji => Extension switch
    {
        ".pdf" => "📕 PDF Fatura",
        ".png" => "🖼️ PNG Fiş/Ekran",
        ".jpg" or ".jpeg" => "📸 JPG Fatura",
        ".xml" => "🧾 e-Arşiv XML",
        _ => "📄 Belge"
    };
}
