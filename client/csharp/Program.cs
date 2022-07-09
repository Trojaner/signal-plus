using System.Diagnostics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ArduinoArgb;
public class Program
{
    private static RgbDevice _device;

    public static async Task Main(string[] args)
    {
        _device = new RgbDevice("COM3");
        await _device.ConnectAsync();

        Console.WriteLine("Connected to RGB device");

        await _device.SetBrightnessAsync(64);
        // await DrawGifAsync("assets/Gif1.gif");
        // await DrawTextAsync("Hello SignalRGB", scrollSpeedMs: 75);
        
        //var visualizer = new AudioVisualizer(_device);
        //visualizer.Start();

        while (true)
        {
            var colors = new Color24[10];
            for (var i = 0; i < colors.Length; i++)
            {
                colors[i] = Color24.FromHsl((double)i / colors.Length, 0.5d, 0.5d);
            }

            await _device.SetLedsAsync(colors);
        }

        Console.ReadKey();
    }

    private static async Task DrawTextAsync(string text, int scrollSpeedMs = 100, Color24? fontColor = null)
    {
        var sw = Stopwatch.StartNew();
        var pixels = TextHelper.CreateText(text, fontColor);

        var padding = 8;
        pixels = AddPadding(pixels, 8);

        var textLength = pixels.GetLength(0);
        var textHeight = pixels.GetLength(1);

        var xOffset = 0;
        var yOffset = (MatrixPanel.Height - textHeight) / 2;

        while (true)
        {
            for (byte y = 0; y < textHeight; y++)
            {
                for (byte x = 0; x < Math.Min(pixels.Length, MatrixPanel.Width - 1); x++)
                {
                    var color = pixels[x + xOffset, y];
                    await _device.SetPixelAsync(x, (byte)(y + yOffset), color);
                }
            }

            if (sw.ElapsedMilliseconds > scrollSpeedMs)
            {
                xOffset++;

                if (xOffset >= Math.Max(0, textLength + padding - MatrixPanel.Width - 8))
                {
                    xOffset = 0;
                }

                sw.Restart();
            }
        }
    }

    private static Color24[,] AddPadding(Color24[,] pixels, int xPadding)
    {
        var length = pixels.GetLength(0);
        var height = pixels.GetLength(1);

        var newPixels = new Color24[length + xPadding * 2, height];

        // Add padding

        for (var x = 0; x < length + xPadding * 2; x++)
        {
            for (var y = 0; y < height; y++)
            {
                if (x < xPadding || x >= xPadding + length - 1)
                {
                    newPixels[x, y] = Color24.Black;
                }
                else
                {
                    newPixels[x, y] = pixels[x - xPadding, y];
                }
            }
        }

        return newPixels;
    }

    private static async Task DrawGifAsync(string path, float speedModifier = 1)
    {
        var image = await Image.LoadAsync<Rgba32>(path);
        var pixels = new ColorStatus[MatrixPanel.Width * MatrixPanel.Height];
        var sw = new Stopwatch();

        while (true)
        {
            for (var i = 0; i < image.Frames.Count; i++)
            {
                sw.Restart();

                var frame = image.Frames[i];
                var metadata = frame.Metadata.GetGifMetadata();

                for (byte x = 0; x < frame.Width; x++)
                {
                    for (byte y = 0; y < frame.Height; y++)
                    {
                        var idx = y * MatrixPanel.Width + x;
                        var frameColor = frame[x, y];
                        var color = new Color24(frameColor.R, frameColor.G, frameColor.B);

                        if (idx >= pixels.Length)
                        {
                            Debugger.Break();
                        }

                        var prevColor = pixels[idx].Color;
                        var isSameColor = color.R == prevColor.R && color.G == prevColor.G && color.B == prevColor.B;

                        pixels[idx].Color = color;
                        pixels[idx].Changed = !isSameColor;
                    }
                }

                for (byte idx = 0; idx < pixels.Length; idx++)
                {
                    var pixel = pixels[idx];
                    if (!pixel.Changed)
                    {
                        continue;
                    }

                    var x = (byte)(idx % MatrixPanel.Width);
                    var y = (byte)((idx - x) / MatrixPanel.Width);

                    await _device.SetPixelAsync(x, y, pixel.Color);
                }

                if (metadata.FrameDelay > 0)
                {
                    var delayDifference = (int)(metadata.FrameDelay * 10 / speedModifier) - (int)sw.ElapsedMilliseconds;
                    if (delayDifference > 0)
                    {
                        await Task.Delay(delayDifference);
                    }

                    if (delayDifference < 0)
                    {
                        Console.WriteLine($"Couldn't keep up with GIF speed, was {-delayDifference}ms late");
                    }
                }

                for (var index = 0; index < pixels.Length; index++)
                {
                    pixels[index].Changed = false;
                }
            }
        }
    }
}