using System;
using System.Collections.Generic;
using CatLib.Assets;
using CatLib.Tests.Framework;

namespace CatLib.Tests.Suites.Assets;

public sealed class JpegDecodeTest : TestCase
{
    private const string BaselineJpeg = "/9j/4AAQSkZJRgABAQAAAQABAAD/2wBDAAIBAQEBAQIBAQECAgICAgQDAgICAgUEBAMEBgUGBgYFBgYGBwkIBgcJBwYGCAsICQoKCgoKBggLDAsKDAkKCgr/2wBDAQICAgICAgUDAwUKBwYHCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgr/wAARCAAQACADASIAAhEBAxEB/8QAHwAAAQUBAQEBAQEAAAAAAAAAAAECAwQFBgcICQoL/8QAtRAAAgEDAwIEAwUFBAQAAAF9AQIDAAQRBRIhMUEGE1FhByJxFDKBkaEII0KxwRVS0fAkM2JyggkKFhcYGRolJicoKSo0NTY3ODk6Q0RFRkdISUpTVFVWV1hZWmNkZWZnaGlqc3R1dnd4eXqDhIWGh4iJipKTlJWWl5iZmqKjpKWmp6ipqrKztLW2t7i5usLDxMXGx8jJytLT1NXW19jZ2uHi4+Tl5ufo6erx8vP09fb3+Pn6/8QAHwEAAwEBAQEBAQEBAQAAAAAAAAECAwQFBgcICQoL/8QAtREAAgECBAQDBAcFBAQAAQJ3AAECAxEEBSExBhJBUQdhcRMiMoEIFEKRobHBCSMzUvAVYnLRChYkNOEl8RcYGRomJygpKjU2Nzg5OkNERUZHSElKU1RVVldYWVpjZGVmZ2hpanN0dXZ3eHl6goOEhYaHiImKkpOUlZaXmJmaoqOkpaanqKmqsrO0tba3uLm6wsPExcbHyMnK0tPU1dbX2Nna4uPk5ebn6Onq8vP09fb3+Pn6/9oADAMBAAIRAxEAPwD53ooor8HP9aDxOiiiv9pD/C8//9k=";
    private const string ProgressiveJpeg = "/9j/4AAQSkZJRgABAQAAAQABAAD/2wBDAAIBAQEBAQIBAQECAgICAgQDAgICAgUEBAMEBgUGBgYFBgYGBwkIBgcJBwYGCAsICQoKCgoKBggLDAsKDAkKCgr/2wBDAQICAgICAgUDAwUKBwYHCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgr/wgARCAAQACADASIAAhEBAxEB/8QAFgABAQEAAAAAAAAAAAAAAAAAAAYH/8QAFQEBAQAAAAAAAAAAAAAAAAAACAb/2gAMAwEAAhADEAAAAc7EGtIkNIL/AP/EABQQAQAAAAAAAAAAAAAAAAAAADD/2gAIAQEAAQUCD//EABQRAQAAAAAAAAAAAAAAAAAAABD/2gAIAQMBAT8BP//EABQRAQAAAAAAAAAAAAAAAAAAABD/2gAIAQIBAT8BP//EABQQAQAAAAAAAAAAAAAAAAAAADD/2gAIAQEABj8CD//EABQQAQAAAAAAAAAAAAAAAAAAADD/2gAIAQEAAT8hD//aAAwDAQACAAMAAAAQAA//xAAUEQEAAAAAAAAAAAAAAAAAAAAQ/9oACAEDAQE/ED//xAAUEQEAAAAAAAAAAAAAAAAAAAAQ/9oACAECAQE/ED//xAAUEAEAAAAAAAAAAAAAAAAAAAAw/9oACAEBAAE/EA//2Q==";
    private const string RotatedJpeg = "/9j/4AAQSkZJRgABAQAAAQABAAD/4QAiRXhpZgAATU0AKgAAAAgAAQESAAMAAAABAAYAAAAAAAD/2wBDAAIBAQEBAQIBAQECAgICAgQDAgICAgUEBAMEBgUGBgYFBgYGBwkIBgcJBwYGCAsICQoKCgoKBggLDAsKDAkKCgr/2wBDAQICAgICAgUDAwUKBwYHCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgr/wAARCAAQACADASIAAhEBAxEB/8QAHwAAAQUBAQEBAQEAAAAAAAAAAAECAwQFBgcICQoL/8QAtRAAAgEDAwIEAwUFBAQAAAF9AQIDAAQRBRIhMUEGE1FhByJxFDKBkaEII0KxwRVS0fAkM2JyggkKFhcYGRolJicoKSo0NTY3ODk6Q0RFRkdISUpTVFVWV1hZWmNkZWZnaGlqc3R1dnd4eXqDhIWGh4iJipKTlJWWl5iZmqKjpKWmp6ipqrKztLW2t7i5usLDxMXGx8jJytLT1NXW19jZ2uHi4+Tl5ufo6erx8vP09fb3+Pn6/8QAHwEAAwEBAQEBAQEBAQAAAAAAAAECAwQFBgcICQoL/8QAtREAAgECBAQDBAcFBAQAAQJ3AAECAxEEBSExBhJBUQdhcRMiMoEIFEKRobHBCSMzUvAVYnLRChYkNOEl8RcYGRomJygpKjU2Nzg5OkNERUZHSElKU1RVVldYWVpjZGVmZ2hpanN0dXZ3eHl6goOEhYaHiImKkpOUlZaXmJmaoqOkpaanqKmqsrO0tba3uLm6wsPExcbHyMnK0tPU1dbX2Nna4uPk5ebn6Onq8vP09fb3+Pn6/9oADAMBAAIRAxEAPwD53ooor8HP9aDxOiiiv9pD/C8//9k=";
    private const string GrayJpeg = "/9j/4AAQSkZJRgABAQAAAQABAAD/2wBDAAIBAQEBAQIBAQECAgICAgQDAgICAgUEBAMEBgUGBgYFBgYGBwkIBgcJBwYGCAsICQoKCgoKBggLDAsKDAkKCgr/wAALCAAQACABAREA/8QAHwAAAQUBAQEBAQEAAAAAAAAAAAECAwQFBgcICQoL/8QAtRAAAgEDAwIEAwUFBAQAAAF9AQIDAAQRBRIhMUEGE1FhByJxFDKBkaEII0KxwRVS0fAkM2JyggkKFhcYGRolJicoKSo0NTY3ODk6Q0RFRkdISUpTVFVWV1hZWmNkZWZnaGlqc3R1dnd4eXqDhIWGh4iJipKTlJWWl5iZmqKjpKWmp6ipqrKztLW2t7i5usLDxMXGx8jJytLT1NXW19jZ2uHi4+Tl5ufo6erx8vP09fb3+Pn6/9oACAEBAAA/APneivE6K9sorxOiv//Z";

    public override string Suite => "Assets";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        foreach (var (name, data) in new[] { ("baseline", BaselineJpeg), ("progressive", ProgressiveJpeg) })
        {
            Assert.True(ImageData.TryDecode(Convert.FromBase64String(data), out var image, out var problem), $"A {name} JPG decodes: {problem}");
            Assert.Equal(32, image.Width, $"Width of the {name} JPG");
            Assert.Equal(16, image.Height, $"Height of the {name} JPG");
            Near(image, 4, 8, 220, 40, 40, $"The red half of the {name} JPG");
            Near(image, 27, 8, 30, 60, 200, $"The blue half of the {name} JPG");
            Assert.Equal(255, image.Rgba[3], $"A {name} JPG is opaque");
        }

        Assert.True(ImageData.TryDecode(Convert.FromBase64String(RotatedJpeg), out var rotated, out _), "A JPG with an EXIF orientation decodes");
        Assert.Equal(16, rotated.Width, "A JPG turned by its EXIF orientation, width");
        Assert.Equal(32, rotated.Height, "A JPG turned by its EXIF orientation, height");
        Near(rotated, 8, 4, 220, 40, 40, "Turned clockwise, the red half is on top");
        Near(rotated, 8, 27, 30, 60, 200, "and the blue half at the bottom");

        Assert.True(ImageData.TryDecode(Convert.FromBase64String(GrayJpeg), out var gray, out _), "A gray JPG decodes");
        var index = (8 * gray.Width + 4) * 4;
        Assert.True(gray.Rgba[index] == gray.Rgba[index + 1] && gray.Rgba[index + 1] == gray.Rgba[index + 2], "A gray JPG has equal channels");

        var damaged = Convert.FromBase64String(BaselineJpeg);
        Array.Resize(ref damaged, 40);
        Assert.False(ImageData.TryDecode(damaged, out _, out var damagedProblem), "A JPG cut after its header is refused");
        Assert.True(damagedProblem != null && damagedProblem.Contains("JPG"), "and the problem names the JPG: " + damagedProblem);
        Assert.False(ImageData.TryDecode(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, out _, out var unknown), "Other files are refused");
        Assert.True(unknown != null && unknown.Contains("neither"), "and the problem says what is read: " + unknown);
        yield break;
    }

    private static void Near(ImageData image, int x, int y, int r, int g, int b, string what)
    {
        var index = (y * image.Width + x) * 4;
        var distance = Math.Abs(image.Rgba[index] - r) + Math.Abs(image.Rgba[index + 1] - g) + Math.Abs(image.Rgba[index + 2] - b);
        Assert.True(distance <= 30, $"{what}: {image.Rgba[index]},{image.Rgba[index + 1]},{image.Rgba[index + 2]} is close to {r},{g},{b}");
    }
}
