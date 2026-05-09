using System.Numerics;

namespace VoidRunner.Graphics;

public readonly struct ColorRgba
{
    public byte R { get; }
    public byte G { get; }
    public byte B { get; }
    public byte A { get; }

    public ColorRgba(byte r, byte g, byte b, byte a = 255)
    {
        R = r;
        G = g;
        B = b;
        A = a;
    }

    public static ColorRgba FromArgb(int a, int r, int g, int b) =>
        new((byte)r, (byte)g, (byte)b, (byte)a);

    public Vector4 ToVector4() =>
        new(R / 255f, G / 255f, B / 255f, A / 255f);

    public ColorRgba WithAlpha(byte a) => new(R, G, B, a);
}
