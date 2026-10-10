# inventomaticstash-mod - InventOmaticStash-UO baseline (not in this repository)

The "compatible with InventOmaticStash-UO" build patches that mod's own `securetrade.swf`. The mod is by a third party
and is not published here. To build that variant, extract the original files of the version you want to support
(currently **v2.85**) into this folder:

- `archive/interface/securetrade.swf` and `archive/interface/InventOmaticStash.swf` - taken from the mod's
  `InventOmaticStash-UO.ba2`, unmodified (`securetrade.swf` SHA-256 starts with `B6214239`).
- `source/securetrade/scripts/` - FFDec script export (`ffdec -export script <dir> securetrade.swf`); the build reads
  `source/securetrade/scripts/SecureTrade.as`, the rest is for diffing. `source/InventOmaticStash/` is the export of the
  child SWF (reading only).
- `inventOmaticStashConfig.json` - the mod's config as shipped (reference).

The build stops if the SHA-256 of `securetrade.swf` differs from the baseline. Please ask the mod's author before
redistributing builds based on it.
