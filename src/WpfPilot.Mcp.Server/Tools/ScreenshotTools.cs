using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text.Json;
using ModelContextProtocol.Server;
using WpfPilot.Mcp.Core.Models;
using WpfPilot.Mcp.Server.Services;

namespace WpfPilot.Mcp.Server.Tools;

[McpServerToolType]
public sealed class ScreenshotTools
{
    private readonly ScreenshotService _screenshots;
    private readonly UiaAdapter _uia;
    private readonly AuditLog _audit;

    public ScreenshotTools(ScreenshotService screenshots, UiaAdapter uia, AuditLog audit)
    {
        _screenshots = screenshots;
        _uia = uia;
        _audit = audit;
    }

    [McpServerTool(Name = "wpf_screenshot"), Description("Capture current window/app screenshot. Returns base64 PNG.")]
    public string Screenshot()
    {
        _audit.Record("wpf_screenshot");
        try
        {
            var bytes = _screenshots.CaptureWindow();
            var base64 = Convert.ToBase64String(bytes);
            return JsonSerializer.Serialize(new
            {
                format = "png",
                encoding = "base64",
                data = base64,
                sizeBytes = bytes.Length
            }, JsonOptions.Default);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions.Default);
        }
    }

    [McpServerTool(Name = "wpf_screenshot_element"), Description("Capture screenshot of selected element. Returns base64 PNG.")]
    public string ScreenshotElement(string? automationId = null, string? name = null)
    {
        _audit.Record("wpf_screenshot_element");
        try
        {
            var criteria = new ElementCriteria { AutomationId = automationId, Name = name };
            var element = _uia.FindElement(criteria);
            if (element is null)
                return JsonSerializer.Serialize(new { error = "Element not found." }, JsonOptions.Default);

            var bytes = _screenshots.CaptureElement(element);
            var base64 = Convert.ToBase64String(bytes);
            return JsonSerializer.Serialize(new
            {
                format = "png",
                encoding = "base64",
                data = base64,
                sizeBytes = bytes.Length
            }, JsonOptions.Default);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions.Default);
        }
    }

    [McpServerTool(Name = "wpf_capture_failure_artifacts"), Description("Capture screenshot, snapshot, and diagnostics after a failure.")]
    public string CaptureFailureArtifacts(string? failureDescription = null)
    {
        _audit.Record("wpf_capture_failure_artifacts");
        try
        {
            string? screenshotBase64 = null;
            try
            {
                var bytes = _screenshots.CaptureWindow();
                screenshotBase64 = Convert.ToBase64String(bytes);
            }
            catch { }

            UiSnapshot? snapshot = null;
            try
            {
                snapshot = _uia.CaptureSnapshot(maxDepth: 4);
            }
            catch { }

            var artifacts = new
            {
                capturedAtUtc = DateTime.UtcNow,
                failureDescription,
                screenshot = screenshotBase64 is not null ? new { format = "png", encoding = "base64", data = screenshotBase64 } : null,
                uiSnapshot = snapshot,
            };

            return JsonSerializer.Serialize(artifacts, JsonOptions.Default);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions.Default);
        }
    }

    [McpServerTool(Name = "wpf_annotate_screenshot"), Description("Capture screenshot with overlay annotations highlighting specific elements.")]
    public string AnnotateScreenshot(string[] automationIds)
    {
        _audit.Record("wpf_annotate_screenshot");
        try
        {
            var bytes = _screenshots.CaptureWindow();
            using var ms = new MemoryStream(bytes);
            using var bitmap = new Bitmap(ms);
            using var graphics = Graphics.FromImage(bitmap);
            using var pen = new Pen(Color.Red, 3);
            using var font = new Font("Arial", 10, FontStyle.Bold);
            using var brush = new SolidBrush(Color.Red);

            int index = 0;
            foreach (var id in automationIds)
            {
                var element = _uia.FindElement(new ElementCriteria { AutomationId = id });
                if (element is not null)
                {
                    var rect = element.BoundingRectangle;
                    var windowBounds = _screenshots.GetWindowBounds();
                    var relRect = new Rectangle(
                        rect.X - windowBounds.X, rect.Y - windowBounds.Y,
                        rect.Width, rect.Height);
                    graphics.DrawRectangle(pen, relRect);
                    graphics.DrawString($"[{index}] {id}", font, brush, relRect.X, relRect.Y - 14);
                }
                index++;
            }

            using var output = new MemoryStream();
            bitmap.Save(output, ImageFormat.Png);
            var base64 = Convert.ToBase64String(output.ToArray());
            return JsonSerializer.Serialize(new { format = "png", encoding = "base64", annotatedElements = automationIds.Length, data = base64 }, JsonOptions.Default);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions.Default);
        }
    }

    [McpServerTool(Name = "wpf_get_cursor_position"), Description("Get current mouse cursor position (absolute screen coordinates).")]
    public string GetCursorPosition()
    {
        _audit.Record("wpf_get_cursor_position");
        var pos = System.Windows.Forms.Cursor.Position;
        return JsonSerializer.Serialize(new { x = pos.X, y = pos.Y }, JsonOptions.Default);
    }

    [McpServerTool(Name = "wpf_highlight_element"), Description("Flash-highlight an element for visual debugging. Returns bounds info.")]
    public string HighlightElement(string? automationId = null, string? name = null)
    {
        _audit.Record("wpf_highlight_element");
        var criteria = new ElementCriteria { AutomationId = automationId, Name = name };
        var element = _uia.FindElement(criteria);
        if (element is null)
            return JsonSerializer.Serialize(new { error = "Element not found." }, JsonOptions.Default);

        var rect = element.BoundingRectangle;
        // Focus the element to visually highlight it
        try { element.Focus(); } catch { }

        return JsonSerializer.Serialize(new
        {
            result = "highlighted",
            bounds = new { x = rect.X, y = rect.Y, width = rect.Width, height = rect.Height },
            automationId = element.Properties.AutomationId.ValueOrDefault,
            name = element.Properties.Name.ValueOrDefault
        }, JsonOptions.Default);
    }

    [McpServerTool(Name = "wpf_compare_screenshot"), Description("Pixel-compare two base64 PNG images and return difference percentage.")]
    public string CompareScreenshot(string baselineBase64, string currentBase64)
    {
        _audit.Record("wpf_compare_screenshot");
        try
        {
            using var baselineMs = new MemoryStream(Convert.FromBase64String(baselineBase64));
            using var currentMs = new MemoryStream(Convert.FromBase64String(currentBase64));
            using var baseline = new Bitmap(baselineMs);
            using var current = new Bitmap(currentMs);

            if (baseline.Width != current.Width || baseline.Height != current.Height)
                return JsonSerializer.Serialize(new { error = "Images have different dimensions.", baselineSize = $"{baseline.Width}x{baseline.Height}", currentSize = $"{current.Width}x{current.Height}" }, JsonOptions.Default);

            int totalPixels = baseline.Width * baseline.Height;
            int diffPixels = 0;

            var rect = new Rectangle(0, 0, baseline.Width, baseline.Height);
            var baselineData = baseline.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            var currentData = current.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                int bytes = Math.Abs(baselineData.Stride) * baseline.Height;
                var baselineBytes = new byte[bytes];
                var currentBytes = new byte[bytes];
                Marshal.Copy(baselineData.Scan0, baselineBytes, 0, bytes);
                Marshal.Copy(currentData.Scan0, currentBytes, 0, bytes);

                int stride = baselineData.Stride;
                for (int y = 0; y < baseline.Height; y++)
                {
                    int rowOffset = y * stride;
                    for (int x = 0; x < baseline.Width; x++)
                    {
                        int offset = rowOffset + x * 4;
                        if (baselineBytes[offset] != currentBytes[offset] ||
                            baselineBytes[offset + 1] != currentBytes[offset + 1] ||
                            baselineBytes[offset + 2] != currentBytes[offset + 2] ||
                            baselineBytes[offset + 3] != currentBytes[offset + 3])
                        {
                            diffPixels++;
                        }
                    }
                }
            }
            finally
            {
                baseline.UnlockBits(baselineData);
                current.UnlockBits(currentData);
            }

            double diffPercent = (double)diffPixels / totalPixels * 100.0;
            return JsonSerializer.Serialize(new
            {
                match = diffPercent < 1.0,
                diffPercent = Math.Round(diffPercent, 2),
                diffPixels,
                totalPixels
            }, JsonOptions.Default);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions.Default);
        }
    }
}
