# exporter/vanilla - the game's SecureTrade menu (not in this repository)

`build_exporter.py` needs two files of the **unmodified game menu** here. They belong to Bethesda, so they are not
published; extract them from your own Fallout 76 installation:

- `securetrade.swf` - from the game's `SeventySix - Interface.ba2` (path `interface/securetrade.swf`; open the archive
  with a BA2 tool such as BAE or the Bethesda Archive Extractor).
- `source/scripts/` - a script export of that SWF, made with JPEXS Free Flash Decompiler (FFDec):
  `ffdec -export script source securetrade.swf` (the build only reads `source/scripts/SecureTrade.as`; the rest is
  for reading and diffing).

The build checks the SHA-256 of `securetrade.swf` against its baseline and stops if the game's menu has changed, so
after a game update refresh this folder first (see section 6.4 of `spec/project-guide.md`).
Only the standalone and child-only builds start from these files.
