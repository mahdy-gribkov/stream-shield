using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

// Generates a multi-resolution .ico file with an amber shield on transparent background.
// Run once: dotnet run --project tools/IconGenerator
// Output: src/StreamShield/Resources/app.ico

string repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
string outputPath = Path.Combine(repoRoot, "src", "StreamShield", "Resources", "app.ico");

int[] sizes = [256, 48, 32, 16];
byte[][] imageData = new byte[sizes.Length][];

for (int i = 0; i < sizes.Length; i++)
{
    using Bitmap bmp = RenderShield(sizes[i]);

    if (sizes[i] == 256)
    {
        // 256x256 stored as PNG inside .ico
        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        imageData[i] = ms.ToArray();
    }
    else
    {
        // Smaller sizes stored as 32-bit ARGB BMP (no file header, just DIB)
        imageData[i] = BmpDib(bmp);
    }
}

WriteIco(outputPath, sizes, imageData);
Console.WriteLine($"Icon written to: {outputPath}");
Console.WriteLine($"Sizes: {string.Join(", ", sizes.Select(s => $"{s}x{s}"))}");

static Bitmap RenderShield(int size)
{
    var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
    using var g = Graphics.FromImage(bmp);
    g.SmoothingMode = SmoothingMode.AntiAlias;
    g.Clear(Color.Transparent);

    float s = size;
    float pad = s * 0.08f;

    using var path = new GraphicsPath();

    // Shield shape: flat top with slight curve, straight sides tapering to pointed bottom
    PointF topLeft = new(pad, s * 0.06f);
    PointF topRight = new(s - pad, s * 0.06f);
    PointF midLeft = new(pad, s * 0.52f);
    PointF midRight = new(s - pad, s * 0.52f);
    PointF bottom = new(s * 0.5f, s * 0.94f);

    // Top edge (slight upward curve)
    path.AddBezier(topLeft, new PointF(s * 0.3f, s * 0.02f),
                   new PointF(s * 0.7f, s * 0.02f), topRight);

    // Right side
    path.AddLine(topRight, midRight);

    // Bottom right curve to point
    path.AddBezier(midRight, new PointF(s * 0.85f, s * 0.68f),
                   new PointF(s * 0.65f, s * 0.85f), bottom);

    // Bottom left curve from point
    path.AddBezier(bottom, new PointF(s * 0.35f, s * 0.85f),
                   new PointF(s * 0.15f, s * 0.68f), midLeft);

    // Left side
    path.AddLine(midLeft, topLeft);

    path.CloseFigure();

    // Fill with amber gradient (lighter at top, darker at bottom)
    using var brush = new LinearGradientBrush(
        new PointF(s * 0.5f, 0), new PointF(s * 0.5f, s),
        Color.FromArgb(230, 170, 70),   // lighter amber top
        Color.FromArgb(180, 120, 40));   // darker amber bottom
    g.FillPath(brush, path);

    // Thin dark outline
    using var pen = new Pen(Color.FromArgb(80, 12, 11, 14), Math.Max(1f, s * 0.015f));
    g.DrawPath(pen, path);

    // Draw a subtle "S" letterform for StreamShield (only at larger sizes)
    if (size >= 32)
    {
        float fontSize = s * 0.38f;
        using var font = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
        using var textBrush = new SolidBrush(Color.FromArgb(200, 12, 11, 14));
        using var sf = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };
        var textRect = new RectangleF(0, s * 0.02f, s, s * 0.88f);
        g.DrawString("S", font, textBrush, textRect, sf);
    }

    return bmp;
}

static byte[] BmpDib(Bitmap bmp)
{
    int w = bmp.Width;
    int h = bmp.Height;

    // BITMAPINFOHEADER (40 bytes) + pixel data + AND mask
    int rowBytes = w * 4;
    int andRowBytes = ((w + 31) / 32) * 4;
    int pixelDataSize = rowBytes * h;
    int andMaskSize = andRowBytes * h;

    using var ms = new MemoryStream();
    using var bw = new BinaryWriter(ms);

    // BITMAPINFOHEADER
    bw.Write(40);              // biSize
    bw.Write(w);               // biWidth
    bw.Write(h * 2);           // biHeight (doubled for ICO: XOR + AND)
    bw.Write((short)1);        // biPlanes
    bw.Write((short)32);       // biBitCount
    bw.Write(0);               // biCompression
    bw.Write(pixelDataSize + andMaskSize); // biSizeImage
    bw.Write(0);               // biXPelsPerMeter
    bw.Write(0);               // biYPelsPerMeter
    bw.Write(0);               // biClrUsed
    bw.Write(0);               // biClrImportant

    // Pixel data (bottom-up BGRA)
    for (int y = h - 1; y >= 0; y--)
    {
        for (int x = 0; x < w; x++)
        {
            Color c = bmp.GetPixel(x, y);
            bw.Write(c.B);
            bw.Write(c.G);
            bw.Write(c.R);
            bw.Write(c.A);
        }
    }

    // AND mask (all zeros = fully opaque, alpha channel handles transparency)
    bw.Write(new byte[andMaskSize]);

    return ms.ToArray();
}

static void WriteIco(string path, int[] sizes, byte[][] imageData)
{
    using var fs = new FileStream(path, FileMode.Create);
    using var bw = new BinaryWriter(fs);

    int count = sizes.Length;

    // ICO header (6 bytes)
    bw.Write((short)0);       // reserved
    bw.Write((short)1);       // type = ICO
    bw.Write((short)count);   // image count

    // Calculate offsets (header=6, each dir entry=16)
    int dataOffset = 6 + count * 16;

    // Directory entries
    for (int i = 0; i < count; i++)
    {
        byte w = sizes[i] >= 256 ? (byte)0 : (byte)sizes[i];
        byte h = w;
        bw.Write(w);              // width (0 = 256)
        bw.Write(h);              // height (0 = 256)
        bw.Write((byte)0);        // color palette
        bw.Write((byte)0);        // reserved
        bw.Write((short)1);       // color planes
        bw.Write((short)32);      // bits per pixel
        bw.Write(imageData[i].Length); // image data size
        bw.Write(dataOffset);     // offset to image data
        dataOffset += imageData[i].Length;
    }

    // Image data
    for (int i = 0; i < count; i++)
        bw.Write(imageData[i]);
}
