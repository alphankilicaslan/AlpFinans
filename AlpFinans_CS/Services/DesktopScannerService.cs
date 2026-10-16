using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using AlpFinans.Models;

namespace AlpFinans.Services;

public class DesktopScannerService
{
    private static readonly HashSet<string> ValidExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".png", ".jpg", ".jpeg", ".xml"
    };

    public static async Task<List<ScannedFileItem>> ScanDirectoriesAsync(string[] paths, string searchQuery = "")
    {
        return await Task.Run(() =>
        {
            var results = new List<ScannedFileItem>();

            foreach (var path in paths)
            {
                if (!Directory.Exists(path)) continue;

                try
                {
                    var dirInfo = new DirectoryInfo(path);
                    var files = dirInfo.EnumerateFiles("*.*", SearchOption.TopDirectoryOnly);

                    foreach (var f in files)
                    {
                        if (!ValidExtensions.Contains(f.Extension)) continue;

                        if (!string.IsNullOrWhiteSpace(searchQuery))
                        {
                            if (!f.Name.Contains(searchQuery, StringComparison.OrdinalIgnoreCase))
                                continue;
                        }

                        results.Add(new ScannedFileItem
                        {
                            FullPath = f.FullName,
                            FileSizeBytes = f.Length,
                            LastModified = f.LastWriteTime
                        });
                    }
                }
                catch
                {
                    // Ignore inaccessible folders
                }
            }

            results.Sort((a, b) => b.LastModified.CompareTo(a.LastModified));
            return results;
        });
    }
}
