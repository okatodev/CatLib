# Crash reports

When the game closes unexpectedly, CatLib shows a small window with what happened and keeps a report with the logs.
This works for every kind of crash, native ones included, because the window comes from a separate program
that waits for the game to close instead of code inside the crashing game.

The program, `CatLib.CrashWatcher.exe`, is its own Thunderstore package, [`CatLib-CrashWatcher`](https://thunderstore.io/c/cat-mail-co/p/CatLib/CrashWatcher/), with its own version,
so a release of CatLib does not change it. CatLib depends on it, so a mod manager installs it with CatLib.
CatLib looks for it next to `CatLib.dll` first, then in every folder of `BepInEx/plugins` up to 4 levels deep,
and takes the newest one when there are several. The log names the version and the path it started.
How the watcher is built and what each of its parts does is in [its own README](../tools/CatLib.CrashWatcher/README.md).

## What the player sees

A small window titled "Cat Mail Co: crash report" in the language of the game. The heading is a random cozy phrase,
different from crash to crash. Below it:

- what happened in one line: the reason from the exit code, the module and offset of the crash,
  how long the game ran, whether it happened while the game was quitting and whether a memory dump was saved;
- **Details**: time, exit code, module, the method it happened in and the nearest method of the game, the thread (the game thread or another one), the .NET exception,
  game and CatLib versions, the mods, the last game events;
- **Open the report folder** and **Copy the report**; the window stays open after both;
- the path of the report folder at the bottom.

A normal exit shows nothing. Closing the game from the Task Manager shows the window with "closed from outside".

## The report folder

`BepInEx/CatLib/Crashes/<date>_<time>_<process id>/`:

| File | Contents |
|---|---|
| `report.txt` | The full report in English for the mod authors: summary, exit code, module, offset, method and thread of the crash, .NET message, the stacks of the crashing thread and the game thread, versions, the game build check (whether the game is the build CatLib is made for), game language, mods, the last 15 game events, the last 40 lines of the BepInEx log |
| `stacks.txt` | The stack of every thread of the game at the moment of the crash or the hang, threads with the same stack grouped, see [Method names and stacks](#method-names-and-stacks) |
| `crash.dmp` | The memory dump of the moment of the crash (5 to 50 MB): the stacks of every thread with the memory they point to, the modules, the handles |
| `LogOutput.log` | The BepInEx log of the crashed session |
| `Player.log` | The Unity log of the crashed session, which the game replaces on the next start |
| `session.txt` | What CatLib recorded while the game ran |

The last 10 reports are kept, memory dumps only in the last 3 of them. `BepInEx/CatLib/Crashes/watcher.log` has the watcher's own messages if something went wrong with it.

## How it works

1. At start CatLib writes `session_<process id>.txt` in `BepInEx/CatLib/Crashes` with the versions, the log paths,
   the id of the game thread and the window texts in the game language, and starts `CatLib.CrashWatcher.exe`. The first line of the file is the format number of the session file:
   a watcher that reads an older format still writes the report and says in it and in `watcher.log` that it should be updated.
2. While the game runs, CatLib adds the mod list and the game events to that file (network ticks are left out,
   the file keeps the last 200 events). When the player changes the language, CatLib writes the texts again.
3. With memory dumps on, the watcher follows the game like a debugger. Exceptions the game handles itself go straight back to it.
   An exception nobody handles is the crash: the watcher notes the module, the offset and the thread, reads the stack of every thread
   and writes the dump while the game is still stopped at that moment, then lets it close.
   Unity closes the game itself after a crash on the game thread, so such an exception never reaches the watcher as unhandled.
   That is why the watcher also keeps a dump of every access violation, bad instruction or stack overflow the moment it is raised
   in native code (not in .NET code, which turns them into ordinary exceptions): at most one every 5 seconds and 10 per session.
   When the game then closes with that same code, with the .NET code of a stack overflow (`0x800703E9`) after a stack overflow,
   or with any crash code within 10 seconds, this dump is the crash dump; otherwise the game recovered and it is deleted.
4. On a normal quit CatLib marks the file. Exit code 0: the watcher removes the file and closes.
   Anything else: it adds the .NET message from the Windows event log (.NET Runtime 1026), and without its own record
   the crash record too (Application Error 1000), writes the report folder and shows the window.
5. An unhandled .NET exception is written into the session file by CatLib, so the report shows it even when Windows logs nothing.
6. A hang while quitting: once CatLib has marked the quit, the watcher waits 8 seconds. If the game still runs then,
   it notes a hang, pauses the game for a moment to read the stack of every thread and, with memory dumps on, writes a dump of every thread. When the player then closes the game
   from the Task Manager, the report and the window say the game stopped responding while quitting and for how long, and the dump goes with the report.
   When the game closes normally after all, the dump is deleted. Either way `watcher.log` notes how many seconds quitting took.

The watcher changes nothing in the game: it only pauses it for the moment of writing a dump. It is a .NET Framework 4.8 program, so it runs on every Windows 10 and 11 without installing anything.
The window texts come from CatLib's translations (`crash.*` keys of `catlib.core`), so they are in every game language
and can be fixed by a translation file like every other text; without them the window is in English.

## Method names and stacks

Instead of a bare `GameAssembly.dll + 0x4d5f30`, the report names the method the crash happened in, the nearest method
of the game on that thread when the crash is in Windows, Unity or another DLL, and shows what every thread was doing at that moment:

```
Module: KERNELBASE.dll + 0x00000000000c41ca, exception 0xc0000005, seen by the crash watcher
Method: RaiseException + 0x8a
Game code: EntityInteractableStore.Start() + 0x60, the nearest game method on the crashing thread

Stack of thread 22412, the game thread:
  GameAssembly.dll + 0x4d5f30         EntityInteractableStore.Start() + 0x60
  GameAssembly.dll + 0x3d6146         IL2CPP runtime, in the function at 0x3d6100
  UnityPlayer.dll + 0x6a1b2c
  ...
```

The newest call is at the top; `+ 0x60` is the distance from the start of the method in bytes.

| What a frame says | Means |
|---|---|
| `Namespace.Type.Method(int, string[]) + 0x60` | A method of the game, of Unity or of another library compiled into `GameAssembly.dll` |
| `…, 3 more methods have the same code` | The compiler kept one copy of identical methods, often small getters; any of them may be the one |
| `IL2CPP runtime, in the function at 0x…` | The runtime that runs the game's code: the garbage collector, threads, reflection |
| `IL2CPP code that is not a game method, such as a generic method` | Code generated for `List<T>`, `Dictionary<K, V>` and other generics, or a helper of IL2CPP |
| `NtWaitForAlertByThreadId + 0x14` | A function a Windows or Steam DLL exports by name |
| `UnityPlayer.dll + 0x6a1b2c` | Code without a name we can read; the offset still identifies it in that build |
| `0x… not in a module` | Code compiled while the game runs: .NET code of BepInEx, Harmony patches and mods |
| `0x… not code` | The thread called or jumped to an address without code, often a null pointer; the next frame made that call |
| `? ` at the start | The frame was found by searching the stack for a return address after code without unwind data, or it depends on a register such a search could not restore; it may be a leftover of an earlier call |

How it works:

- **Stacks.** With memory dumps on, the watcher follows the game as a debugger, so at the moment of a crash every thread is stopped.
  It reads their registers and walks each stack with the unwind data of x64 code (`.pdata` of every DLL, read from the DLL files),
  the same data Windows uses for exceptions. For a hang it pauses the threads only while it walks their stacks, also without memory dumps,
  and names the frames after the game goes on.
  Without memory dumps a crash has no stacks, only the method of the crash from the Windows event log.
- **Names.** BepInEx writes `BepInEx/interop/MethodAddressToToken.db` when it generates the interop assemblies: the address of every method
  of the game in `GameAssembly.dll` and the method it belongs to. The watcher looks the address up there and reads the name and the parameters
  from the interop assembly, nothing is written while the game runs. Before it trusts the file, it checks a sample of the addresses against the
  functions of `GameAssembly.dll`; a file left from another game build is not used and `watcher.log` says so.
- **Original names.** Interop renames compiler-generated types such as `<Start>d__12` and keeps the original in an attribute, which the report shows.
  Methods renamed by interop like `Method_Private_IEnumerator_PDM_0` have no original name there and stay as they are.

> [!NOTE]
> `UnityPlayer.dll`, `steamclient64.dll` and the drivers have no names in the report beyond their exported functions.
> Unity publishes symbols for `UnityPlayer.dll` on its symbol server, so a mod author can still resolve such a frame
> in Visual Studio or WinDbg with the memory dump.

## Settings

In section `[Diagnostics]` of CatLib's config, both on the Mods tab and taking effect on the next start:

- `CrashWindow` turns the watcher off.
- `CrashDumps` turns the memory dumps off; the watcher then only waits for the game to close, and a crash report has no stacks.

## Checking it

The developer menu of `CatLib.Tests` has the group **Crash**; the crash commands need a second press within 3 s:

| Command | What happens |
|---|---|
| Native crash, game thread | Unity's own forced crash, an access violation on the game thread |
| Native crash, worker thread | An access violation on a new native thread, like the Steam networking crash found in 0.5.0 |
| Managed crash | An unhandled .NET exception on a new thread |
| Hang on quit | Turns on or off a 90 s hang of the game thread while Unity shuts down after a quit, like the real hangs; close the game from the Task Manager after 10 s to get the hang report |

To look at the window without crashing, run
`CatLib.CrashWatcher.exe --preview --session <any file>` from its folder in `BepInEx/plugins`.

## Memory dumps

`crash.dmp` opens in Visual Studio or WinDbg next to the game's `GameAssembly.dll` and `UnityPlayer.dll`.
If the watcher cannot follow the game (another debugger is attached, for example), it waits without dumps and says so in `watcher.log`.
When Windows Error Reporting is set up to keep dumps (`LocalDumps` in the registry), the report names that dump instead.
