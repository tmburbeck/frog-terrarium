# Save format

Saves are small binary files (`.ftsv`), written by `SaveSystem`.

| Bytes | Content |
| --- | --- |
| 4 | Magic `FTSV` |
| 4 | Format version (int32, little-endian). Currently **9**. |
| rest | Body for that version |

**Version 1 body:** world seed, tank half-size X and Z, simulated time, every random
stream (name, state, increment), the next agent id, then each agent's id, kind, position,
velocity, hunger, thirst and fatigue.

**Version 2 body:** the whole version 1 body, then:

* terrain: grid points across and deep, cell size, water level, every point's height, every
  point's ground layer (one byte: 0 soil, 1 moss, 2 sand, 3 rock);
* environment: room temperature and humidity, grid cells across and deep, every cell's
  temperature, every cell's humidity;
* devices: the next device id, then each device's id, kind (0 heater, 1 mister), position,
  on/off, two settings, supply, timer and whether it is working right now.

**Version 3 body:** the same as version 2, except that each agent's record also has, after
fatigue: comfort, goal (one byte: 0 idle, 1 wander, 2 eat, 3 drink, 4 sleep, 5 seek comfort,
6 flee), seconds on that goal, target agent id, target position X and Z, age, lifespan, wander
timer and tongue cooldown. Device kind 2 (feeder) is allowed.

**Version 4 body:** the same as version 3, except that:

* each agent's record also has, after tongue cooldown: species (one byte: 0 White's tree frog,
  1 red-eyed tree frog, 2 Oriental fire-bellied toad), sex (one byte: 0 female, 1 male), life stage
  (one byte: 0 egg, 1 tadpole, 2 froglet, 3 juvenile, 4 adult, 5 elder), growth, stress, mother id,
  father id, mate id, seconds carrying eggs, breeding cooldown, then the frog's genes and its mate's
  genes (seven floats each: size, hue, heat shift, damp shift, boldness, longevity, fertility);
* goals 7 (lay eggs), 8 (escape) and 9 (disperse) are allowed;
* after the devices comes the list of escaped frogs: a count, then each one's id, species, sex,
  age, growth, lifespan, mother id, father id, genes, reason (text) and time of escape.

**Version 5 body:** the same as version 4, except that:

* each agent's record also has, after its mate's genes: loneliness (float), seconds left backing
  off after a lost dispute (float), whether it holds a territory (one byte), and the territory's
  centre X and Z (floats);
* goals 10 (socialise), 11 (defend territory) and 12 (back off) are allowed;
* after the escaped frogs come the population controls: breeding on (one byte), frog limit
  (int32, 0 means the tank's capacity), frogs rehomed so far (int32) and eggs collected so far (int32).

**Version 6 body:** the same as version 5, except that:

* every set of genes (a frog's own, its mate's, and an escaped frog's) also has, after fertility:
  mutations (uint32, two bits per mutation giving 0, 1 or 2 copies, in the order teeth, claws, giant
  eyes, long legs, loud call, albino, blue, black, stubby legs), the other species in a hybrid (one
  byte), how much of the frog is that species (float, 0 to 0.5) and generations bred true (one byte);
* each agent's record also has, after the territory centre: the species of the father of the eggs she
  carries (one byte);
* after the population controls come: mutation boost on (one byte), mutations found so far (uint16,
  one bit each), hybrids found so far (uint64, bit = smaller species id × 8 + larger id), then the
  registered species: a count, then each one's type (int32: main species, other species, hybrid mix in
  quarters and visible mutations, see `Lineage.TypeOf`), name (text) and time registered (double).

**Version 7 body:** the same as version 6, then the building and decoration parts: the next part
id (int32), a count, then each part's id (int32), kind (one byte: 0 rock, 1 stone slab, 2 log,
3 branch, 4 hide, 5 leafy plant, 6 fern, 7 moss pad, 8 wall, 9 pillar, 10 platform, 11 ramp),
centre X and Z, turn in degrees, size (0.5 to 2), colour (three bytes: red, green, blue) and the id of
the part it stands on (int32, 0 = the ground).

Loading a version 6 or earlier save gives a tank with no parts.

**Version 8 body:** the same as version 7, then money mode: on or off (bool), money (double),
reputation (float, 0 to 100), research points (int32), researched items (uint32, one bit each, see
`Research`), visitors so far (int64), frogs sold (int32), money earned from visitors, sales and
contracts and spent on power, upkeep and the shop (six doubles), the next contract id (int32), seconds
to the next contract offer (float), the part of a visitor still to arrive (float), then a count of
open contracts and for each: id (int32), kind (one byte: 0 species, 1 mutation, 2 hybrid,
3 registered species), two bytes (species or mutation number, and the other species of a hybrid),
the wanted type (int32), frogs needed and delivered (two int32), reward (float), deadline in
simulation seconds (double) and the buyer (text).

Loading a version 7 or earlier save gives money mode off (the sandbox), so old tanks play as before.

**Version 9 body:** the same as version 8, then the style set in use (one byte, see
`TankLooks.Styles`: 0 classic, 1 rainforest, 2 desert, 3 riverbank, 4 volcanic, 5 moonlit) and the
style sets bought (uint32, one bit each). A bigger tank needs nothing new: its size was always saved.

Loading a version 8 or earlier save gives the classic style.

Loading a version 5 or earlier save makes every frog pure-bred with no mutations and generation 0,
with eggs being carried for a male of the mother's own species, the mutation boost off, and nothing
found or registered yet.

Loading a version 4 or earlier save gives every frog no loneliness and no territory (males claim
one again within a few seconds), with breeding on, no limit, and both counts at zero.

Loading a save from version 3 or earlier makes every frog a grown White's tree frog with average
genes and no known parents; odd ids become female and even ids male, and each frog gets a lifespan
worked out from its id that leaves it at least 20 more minutes.

Loading a version 2 save gives frogs an adult age and crickets a lifespan worked out from
their id. Loading a version 1 save gives flat ground at the default height, no devices, and room
conditions, with every agent placed on the ground.

Saves are written to a temporary file and then swapped in, so a crash while saving never
destroys the previous save. Quick-saves go to Unity's `Application.persistentDataPath/saves/`.

## Rules for changing the format

1. **Never change how an existing version is read.** Old saves and shared files must keep loading.
2. To add or change data: bump `CurrentVersion`, change `Write`, add a `ReadV2` (etc.) and a
   `case` for it in `Read`. Older readers fill new fields with sensible defaults.
3. Keep a sample save of every old version and a test that loads it. The version 1 sample is
   stored as text inside `Assets/Tests/EditMode/SaveSystemTests.cs` (`Version1Sample`), so both
   Unity and the plain .NET tests can read it; `Version2Sample` to `Version8Sample` sit
   next to it. Before bumping the version again, add a `Version9Sample` the same way.
4. Files from a *newer* game are refused with a clear message instead of being misread.
