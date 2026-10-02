# Changelog

## 1.0.0

- A package of its own. Until now the crash watcher came inside CatLib (0.6.0 and 0.6.1); CatLib 0.6.2 and later depend on this package
  and find the program in any folder of `BepInEx/plugins`.
- The crash window in every language of the game, with a different cozy phrase every time, the details, and buttons
  to open the report folder and to copy the report.
- A report folder in `BepInEx/CatLib/Crashes` for every crash: the report for mod authors, both game logs, the recorded session and a memory dump.
- Memory dumps of the moment of the crash, also for crashes that Unity closes itself on the game thread.
- A hang while quitting is noticed after 8 seconds, with a dump of every thread at that moment.
- The report names its own version and warns when CatLib records more than this version can read.
