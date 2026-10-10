# InventoryTracking

A Fallout 76 inventory export mod and viewer: export buttons in the stash, scrap, ammo and aid boxes, a decoder, a web/desktop
viewer (.NET 8 + Vue 3), and a Windows tray tool (the Syncer) that uploads the exports to a hosted viewer.

- `exporter/` - the game mod (ActionScript hook + build script), `tools/` - decoder and release packaging
- `inventorytracking-ui/` - viewer, `inventorytracking-syncer/` - Syncer
- `spec/project-guide.md` - the full specification and developer guide (start here)

## License

MIT, see [LICENSE](LICENSE). It covers the code in this repository only.

## Credits and third-party material

- **Fallout 76** and its interface files belong to Bethesda Game Studios / ZeniMax Media. They are not part of this repository:
  `exporter/vanilla/` has to be filled from your own installation (see its README). Not affiliated with or endorsed by Bethesda.
- **xScal Scaleform Extender** is required by the mod at runtime (not included). Tested with xScal 0.2.20; not tested with ZFE.
- **InventOmaticStash-UO** (third-party mod, tested v2.85): the "compatible" build patches that mod's storage menu. Its files are not
  in this repository (see `inventomaticstash-mod/README.md`); credit to its author.
- **JPEXS Free Flash Decompiler (FFDec)** is used to build the mod (not included).
- The viewer and Syncer use .NET and open-source libraries (Vue, Vite and others) under their own licenses; see their package files.
