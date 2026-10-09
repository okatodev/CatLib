namespace CatLib.CrashWatcher.Symbols;

internal readonly struct PeSection
{
    public PeSection(string name, uint virtualAddress, uint virtualSize, uint rawPointer, uint rawSize, uint characteristics)
    {
        Name = name;
        Characteristics = characteristics;
        VirtualAddress = virtualAddress;
        VirtualSize = virtualSize;
        RawPointer = rawPointer;
        RawSize = rawSize;
    }

    public string Name { get; }

    public uint Characteristics { get; }

    public uint VirtualAddress { get; }

    public uint VirtualSize { get; }

    public uint RawPointer { get; }

    public uint RawSize { get; }

    public bool IsExecutable => (Characteristics & PeImage.ExecutableSection) != 0;
}
