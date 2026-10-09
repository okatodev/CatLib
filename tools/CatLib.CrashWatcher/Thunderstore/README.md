# CatLib Crash Watcher

The crash window of CatLib. You don't need to install it yourself: it comes with CatLib.

When Cat Mail Co. closes by itself or freezes while quitting, a small window tells you what happened in your language
and keeps a report folder with everything a mod author needs to fix it. Send that folder with your bug report.

## What it does

- Starts together with the game and waits for it to close. On a normal quit it closes too and leaves nothing behind.
- After a crash it shows a small window: what happened in one line, the details, and buttons to open the report folder or copy the report.
- Keeps the report in `BepInEx/CatLib/Crashes`: the report, both game logs and a memory dump of the moment of the crash.
  Only the last 10 reports are kept, dumps only in the last 3.
- Notices when the game hangs while quitting and says so in the report.
- Names the method of the game the crash happened in and writes down what every thread of the game was doing at that moment,
  so a mod author sees where it broke without opening the memory dump.

## What it doesn't do

- It sends nothing anywhere. The report stays on your computer until you attach it yourself.
- It doesn't change the game. To write a memory dump it follows the game the way a debugger does,
  which pauses the game only for the moment of writing and of reading the threads.

It is a separate Windows program, `CatLib.CrashWatcher.exe`, because a program can't report its own crash reliably from the inside.
Source code: [tools/CatLib.CrashWatcher](https://github.com/okatodev/CatLib/tree/main/tools/CatLib.CrashWatcher),
how it works: [Crash reports](https://github.com/okatodev/CatLib/blob/main/docs/CrashReports.md).

## Settings

Settings → Mods → CatLib:

- **Crash window:** turn the whole thing off.
- **Crash dump:** keep the window and the report, but without the memory dump.

## Found a bug?

Report it on [GitHub](https://github.com/okatodev/CatLib/issues).
If the crash window itself misbehaved, attach `BepInEx/CatLib/Crashes/watcher.log` too.
