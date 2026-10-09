using CatLib.CrashWatcher.Symbols;

namespace CatLib.CrashWatcher.Stacks;

internal sealed class UnwindInfo
{
    public const byte ChainFlag = 0x04;

    private UnwindInfo()
    {
    }

    public uint Address { get; private set; }

    public int Version { get; private set; }

    public bool Chained { get; private set; }

    public int PrologSize { get; private set; }

    public int CodeCount { get; private set; }

    public int FrameRegister { get; private set; }

    public int FrameOffset { get; private set; }

    public byte[] Codes { get; private set; }

    public static UnwindInfo Read(PeImage image, uint rva)
    {
        var header = image.Read(rva, 4);
        if (header == null)
        {
            return null;
        }

        var version = header[0] & 7;
        if (version != 1 && version != 2)
        {
            return null;
        }

        var count = header[2];
        var codes = count == 0 ? new byte[0] : image.Read(rva + 4, count * 2);
        if (codes == null)
        {
            return null;
        }

        return new UnwindInfo
        {
            Address = rva,
            Version = version,
            Chained = (header[0] >> 3 & ChainFlag) != 0,
            PrologSize = header[1],
            CodeCount = count,
            FrameRegister = header[3] & 0x0F,
            FrameOffset = header[3] >> 4,
            Codes = codes
        };
    }
}
