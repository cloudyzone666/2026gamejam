param(
    [string]$MonsterSource = 'C:\Users\21712\Desktop\AA762B01F20F9B8C8B6169AFF6F3D30A.png',
    [string]$HeroSource = 'C:\Users\21712\Desktop\角色1【防御+近战】.jpg'
)

# Deterministic crop and edge-connected white removal; the source files stay intact.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$drawingReferences = @([System.Drawing.Bitmap].Assembly.Location, [System.Drawing.Color].Assembly.Location)
if (Test-Path (Join-Path $PSHOME 'System.Private.Windows.GdiPlus.dll')) {
    $drawingReferences += (Join-Path $PSHOME 'System.Private.Windows.GdiPlus.dll')
    $drawingReferences += (Join-Path $PSHOME 'System.Private.Windows.Core.dll')
}
Add-Type -ReferencedAssemblies $drawingReferences -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

public static class CardPortraitPreparation
{
    public static void Prepare(string sourcePath, string outputPath, Rectangle crop, int maxHeight)
    {
        using (var source = new Bitmap(sourcePath))
        using (var image = new Bitmap(crop.Width, crop.Height, PixelFormat.Format32bppArgb))
        {
            using (Graphics graphics = Graphics.FromImage(image))
                graphics.DrawImage(source, new Rectangle(0, 0, crop.Width, crop.Height), crop, GraphicsUnit.Pixel);
            BitmapData locked = image.LockBits(new Rectangle(0, 0, image.Width, image.Height), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            int width = image.Width, height = image.Height;
            byte[] pixels = new byte[locked.Stride * height];
            Marshal.Copy(locked.Scan0, pixels, 0, pixels.Length);
            bool[] visited = new bool[width * height];
            int[] queue = new int[visited.Length];
            int head = 0, tail = 0;
            Action<int, int> visit = (x, y) =>
            {
                if (x < 0 || y < 0 || x >= width || y >= height) return;
                int cell = y * width + x, offset = y * locked.Stride + x * 4;
                if (visited[cell] || pixels[offset] < 242 || pixels[offset + 1] < 242 || pixels[offset + 2] < 242) return;
                visited[cell] = true;
                queue[tail++] = cell;
            };
            for (int x = 0; x < width; x++) { visit(x, 0); visit(x, height - 1); }
            for (int y = 0; y < height; y++) { visit(0, y); visit(width - 1, y); }
            while (head < tail)
            {
                int cell = queue[head++], x = cell % width, y = cell / width;
                int offset = y * locked.Stride + x * 4;
                pixels[offset + 3] = 0;
                visit(x - 1, y); visit(x + 1, y); visit(x, y - 1); visit(x, y + 1);
            }
            Marshal.Copy(pixels, 0, locked.Scan0, pixels.Length);
            image.UnlockBits(locked);
            int outputHeight = Math.Min(height, maxHeight);
            int outputWidth = (int)Math.Round(width * (double)outputHeight / height);
            using (var output = new Bitmap(outputWidth, outputHeight, PixelFormat.Format32bppArgb))
            {
                using (Graphics graphics = Graphics.FromImage(output))
                {
                    graphics.CompositingMode = CompositingMode.SourceCopy;
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    graphics.DrawImage(image, new Rectangle(0, 0, outputWidth, outputHeight));
                }
                output.Save(outputPath, ImageFormat.Png);
            }
        }
    }
}
'@

$artRoot = Join-Path $PSScriptRoot '../Assets/Art/Characters'
[System.IO.Directory]::CreateDirectory($artRoot) | Out-Null
[CardPortraitPreparation]::Prepare($MonsterSource, (Join-Path $artRoot 'SkullSpider.png'), [System.Drawing.Rectangle]::new(80, 94, 550, 356), 1024)
$heroImage = [System.Drawing.Image]::FromFile($HeroSource)
try { $heroRect = [System.Drawing.Rectangle]::new(0, 0, $heroImage.Width, $heroImage.Height) }
finally { $heroImage.Dispose() }
[CardPortraitPreparation]::Prepare($HeroSource, (Join-Path $artRoot 'NamelessSwordsman.png'), $heroRect, 1024)
Write-Output 'Saved SkullSpider.png (550x356) and NamelessSwordsman.png (604x1024).'
