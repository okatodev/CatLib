# Changelog

## 1.1.0

- Reports name the method of the game a crash happened in, for example `EntityInteractableStore.Start() + 0x60` instead of only
  `GameAssembly.dll + 0x4d5f30`. The names come from `BepInEx/interop/MethodAddressToToken.db`, which BepInEx writes with the interop
  assemblies; the watcher reads it only after a crash and checks that it belongs to the running game build.
- Every report of a crash or a hang has the stacks of all game threads: the crashing thread and the game thread in `report.txt`,
  every thread in `stacks.txt`, threads with the same stack grouped. The watcher walks them with the unwind data of the game's DLLs
  while the game is stopped at the crash, or pauses it for a moment when it hangs.
- Frames outside the game's methods say what they are: the IL2CPP runtime, generic code of IL2CPP, a function a DLL exports,
  or .NET code compiled while the game runs. Frames found by searching the stack are marked with `?`.
- When the crash is in Windows, Unity or another DLL, the report and the window also name the nearest method of the game
  on the crashing thread, for example the method that called `RaiseException`.
- A call through a bad pointer shows the bad address as "not code" and the frame that made the call below it.
- C++ names of exported functions are shown readable, for example `il2cpp_baselib::Baselib_SystemFutex_Wait`.
- The window shows the method next to the module in the details.

## 1.0.2

- A stack overflow keeps its memory dump. .NET closes the game with 0x800703E9 after a stack overflow, the watcher took that for
  another exit and removed the dump it wrote when the overflow happened. Now that dump stays, and the report names the place
  of the overflow instead of "no crash record". The same goes for any crash within 10 seconds after an exception the game did not survive.
- The exit code 0x800703E9 is named as a stack overflow.
- The game no longer closes when it stops at a breakpoint. Steam and other libraries stop there only when a debugger follows the game,
  and the watcher is one, so the game closed with 0x80000003 where it goes on without the watcher, for example when the host returned to the menu
  right after a player left. The watcher now lets the game go on and writes the first stops into its log.

## 1.0.0

- A package of its own. Until now the crash watcher came inside CatLib (0.6.0 and 0.6.1); CatLib 0.6.2 and later depend on this package
  and find the program in any folder of `BepInEx/plugins`.
- The crash window in every language of the game, with a different cozy phrase every time, the details, and buttons
  to open the report folder and to copy the report.
- A report folder in `BepInEx/CatLib/Crashes` for every crash: the report for mod authors, both game logs, the recorded session and a memory dump.
- Memory dumps of the moment of the crash, also for crashes that Unity closes itself on the game thread.
- A hang while quitting is noticed after 8 seconds, with a dump of every thread at that moment.
- The report names its own version and warns when CatLib records more than this version can read.
