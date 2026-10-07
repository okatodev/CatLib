# Changelog

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
