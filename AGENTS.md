# AI Repository Rules — fahrenheit-qol-mod

Runtime consumer. Reverse-engineering evidence and engine addresses live in the
sibling `ffx-knowledge-base`; findings discovered here get promoted there.

- FFX only (`FhGameId.FFX`).
- One module per convenience, each independently loadable and independently
  removable. They share nothing but the address table.
- Hook targets are addressed by RVA (`FhMethodLocation`), because the pinned
  Fahrenheit's generated call table exposes most engine functions only as
  `FUN_<address>` entries. RVA = Ghidra VA - 0x400000.
- `FhMethodLocation` is a ref struct; construct it at the use site.
- Never hardcode a value that has not been read off the running game. If it is
  unknown, survey it and log, as FocusInput does with the axis packing.
