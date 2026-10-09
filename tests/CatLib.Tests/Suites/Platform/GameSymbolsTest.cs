using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using BepInEx;
using CatLib.CrashWatcher.Symbols;
using CatLib.Tests.Framework;

namespace CatLib.Tests.Suites.Platform;

public sealed class GameSymbolsTest : TestCase
{
    public const int MinGenericMethods = 1000;
    public const int MinMethods = 10000;

    public override string Suite => "Platform";

    public override TimeSpan Timeout => TimeSpan.FromSeconds(90);

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        var gameAssembly = Path.Combine(Paths.GameRootPath, CodeNames.GameAssemblyName);
        var metadata = Il2CppCodeNames.FindMetadata(gameAssembly);
        Assert.True(File.Exists(gameAssembly), "GameAssembly.dll is next to the game");
        Assert.NotNull(metadata, "global-metadata.dat is found in the data folder of the game");

        var result = Task.Run(() => Check(gameAssembly, metadata));
        yield return Wait.Until(() => result.IsCompleted, 80, "the game's IL2CPP metadata to be read");
        foreach (var line in result.Result)
        {
            context.Note(line);
        }
    }

    private static List<string> Check(string gameAssembly, string metadataPath)
    {
        var notes = new List<string>();
        var clock = System.Diagnostics.Stopwatch.StartNew();
        using (var image = PeImage.Open(gameAssembly))
        {
            var il2cpp = Il2CppCodeNames.Open(image, metadataPath, out var problem);
            Assert.Null(problem, "The IL2CPP metadata and registrations of the game are read");
            notes.Add($"Metadata version {il2cpp.Version}, code registration 0x{il2cpp.CodeRegistration:X}, metadata registration 0x{il2cpp.MetadataRegistration:X}, " +
                      $"{il2cpp.GenericMethodCount} generic method addresses, {il2cpp.MethodCount} methods, {clock.ElapsedMilliseconds} ms");
            Assert.AtLeast(MinGenericMethods, il2cpp.GenericMethodCount, "Generic method addresses");
            Assert.AtLeast(MinMethods, il2cpp.MethodCount, "Method addresses from the code modules");

            var map = GameMethodMap.Open(Path.Combine(Paths.BepInExRootPath, "interop"), out problem);
            Assert.Null(problem, "The method map of BepInEx is read");
            var compared = 0;
            var agreed = 0;
            var placeholders = 0;
            for (var index = 0; index < map.Count; index += Math.Max(1, map.Count / 500))
            {
                var rva = map.Sample(index);
                var known = map.NameAt(rva, out var shared);
                var read = il2cpp.MethodNameAt(rva);
                if (known == null || read == null || shared > 0)
                {
                    continue;
                }

                compared++;
                placeholders += CodeNames.IsInteropPlaceholder(known) ? 1 : 0;
                if (CodeNames.SameMethod(known, read))
                {
                    agreed++;
                }
                else if (notes.Count < 6)
                {
                    notes.Add($"Differs: {known} / {read}");
                }
            }

            notes.Add($"{agreed} of {compared} methods agree with the BepInEx method map, {placeholders} of them have a made-up name in the interop assemblies");
            Assert.AtLeast(compared * 98 / 100, agreed, "Method names from the metadata agree with the BepInEx method map");

            var named = 0;
            var withArguments = 0;
            var fullyShared = 0;
            var codeSection = false;
            foreach (var section in image.Sections)
            {
                if (section.Name != "il2cpp")
                {
                    continue;
                }

                for (var rva = section.VirtualAddress; rva < section.VirtualAddress + section.VirtualSize && named < 200; rva += section.VirtualSize / 400)
                {
                    if (!il2cpp.TryFloorGeneric(rva, out var start))
                    {
                        continue;
                    }

                    var name = il2cpp.GenericNameAt(start, out _);
                    if (name == null)
                    {
                        continue;
                    }

                    named++;
                    withArguments += name.Contains("<") ? 1 : 0;
                    fullyShared += name.Contains(Il2CppCodeNames.FullySharedPrefix) ? 1 : 0;
                    if (named <= 5 || (name.Contains("<T") && named % 20 == 0))
                    {
                        notes.Add($"Generic 0x{start:x}: {name}");
                    }
                }

                codeSection = true;
            }

            Assert.AtLeast(10, named, "Generic methods across the code get names");
            Assert.AtLeast(named * 8 / 10, withArguments, "Generic method names carry their type arguments");
            Assert.Equal(0, fullyShared, "Code shared by all type arguments shows the generic parameter names");
            Assert.True(codeSection, "GameAssembly.dll has its il2cpp code section");
        }

        var player = Path.Combine(Paths.GameRootPath, "UnityPlayer.dll");
        using (var image = PeImage.Open(player))
        {
            Assert.True(image.TryGetCodeView(out var pdbName, out var key), "UnityPlayer.dll names its PDB");
            Assert.NotNull(SymbolStore.ServerFor(pdbName), "UnityPlayer's PDB is on a known symbol server");
            var cached = new SymbolStore(Path.Combine(Paths.BepInExRootPath, "CatLib", "Symbols"), null).CachedPath(pdbName, key);
            notes.Add(cached == null ? $"{pdbName} {key} is not downloaded yet: run the game for half a minute with the crash watcher" : $"{pdbName} is in {cached}");
            if (cached != null)
            {
                var publics = PdbPublics.Read(cached, image, key, out var problem);
                Assert.Null(problem, "The downloaded UnityPlayer PDB reads");
                notes.Add($"{publics.Count} public symbols in UnityPlayer.dll");
                Assert.AtLeast(1000, publics.Count, "UnityPlayer.dll has public symbols");
            }
        }

        return notes;
    }
}
