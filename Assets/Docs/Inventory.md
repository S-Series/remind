# Game item inventory

`ItemCatalog` contains Game-only item definitions. Each item has a stable ID,
display name, optional icon, kind ID, and positive `long` quantity limit.
`Assets/Data/Items/currency.memory_fragment.asset` defines the existing first-clear
memory fragment as a consumable balance. Its icon is not authored yet.

`LocalPlayerDataStore` version 5 saves only item IDs and integer quantities.
Versions 1 through 4 seed `currency.memory_fragment` once from their cleared
song/difficulty records while retaining chart, favorite, character, and song
access data. Version 5 reads the saved balance, so spending cannot be reversed
by recalculating past clears. Missing or damaged version 5 inventory data is
rejected. Unknown IDs remain in the save, but grant and consume reject them.

Grant and consume persist a cloned document before changing in-memory data. A
failed write leaves both quantities unchanged. The catalog limit and `long` range
are checked before a grant; an insufficient balance prevents consumption.
`AppRoot` exposes the loaded balance and a spend method through its existing
player-data instance. A future UI should use that owner.

The existing manual first-clear rule awards one memory fragment in the same
write as the chart result and song unlock. Auto Play remains excluded. No UI
consumes items yet. Other reward amounts, ability multipliers, general
settlement IDs, rounding, world progress, and spending UI remain separate work.
The lower-level `RecordResult` method only changes chart records;
`RecordAndSaveResult` is the first-clear award and persistence boundary.

The [2026-10-11 isolated integration baseline](../../Logs/IntegrationBaseline/20261011-item-inventory/README.md)
verified the full Unity EditMode suite (355/355), two separate integration
probes (2/2), and a Game Windows build. The focused 87 tests are included in
the full 355. The isolated import reserialized five settings files, so the
build did not use byte-identical settings. Bootstrap `Awake` against a real
player save, UI spending, and a complete song playthrough were not verified.
