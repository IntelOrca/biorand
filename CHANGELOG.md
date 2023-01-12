BioRand v2.1.5 (2023-01-12)

* [Feature] RE 2, Add Ada as a playable character.
* [Feature] RE 2, Add Alyssa (RE Outbreak) as a playable character and NPC.
* [Feature] RE 2, Add Jake (RE 6) as a playable character and NPC.
* [Feature] RE 2, Add Piers (RE 6) as a playable character and NPC.
* [Feature] RE 2, Add random enemy positions and quantities.
* [Feature] RE 2, Add Regina (Dino Crisis) as a playable character.
* [Feature] RE 2, Birkin / G1 can now appear with random enemy placements enabled.
* [Enhancement] Title backgrounds now have BioRand logo.
* [Fix] #124: RE 2, room 601, softlock due to partner flag in door rando.
* [Fix] #125: Random enemy placements places enemies in cutscene rooms.
* [Fix] #127: Firing the grenade launcher (explosive) crashes the game.
* [Fix] #128: Cabin key spawned in A scenario non-door rando.
* [Fix] #131: RE 2, room 404, Claire softlock when partner is not Sherry.
* [Fix] #132: RE 2, room 409, Irons invisible during Ada / Annette cutscene.
* [Fix] #142: RE 2, room 601, soft lock when door is locked.
* [Fix] #143: RE 1, room 10E, crash when dog is in closet.
* [Fix] RE 2, Hunk model..
* [Fix] RE 2, Kevin voice lines do not play.
* [Fix] RE 2, Richard PLD injured texture was incorrect.
* [Fix] RE 2, room 21B, lift is not randomized for Leon.
* [Fix] RE 2, room 40A, voices not randomized for Claire.
* [Fix] RE 2, room 611, crash when em90 is used.
* [Fix] RE 2, William, two voice lines do not play.
* [Fix] Reduce chance of duplicates in voice randomization.
* [Fix] Volume of some BGM tracks were too loud.

BioRand v2.1.4 (2023-01-07)

* [Feature] Claire / female playable characters can now use all of Leon's weapons.
* [Feature] Inventory can now be randomized for RE 2.
* [Enhancement] Add description to rebirth mod selection.
* [Enhancement] Add Josh and Doug radio voice lines from RE 5.
* [Enhancement] Add Mike radio voice lines from RE 4.
* [Enhancement] Add more Ada voice lines from RE: Umbrella Chronicles and RE: The Darkside Chronicles.
* [Enhancement] Add more Hunk voice lines from RE: Umbrella Chronicles.
* [Enhancement] Add more music tracks from RE: Code Veronica, RE 4, RE 5, RE:UC, and RE:DC.
* [Enhancement] Add more RE: Code Veronica music tracks.
* [Enhancement] Allow Leon to use Claire's handgun.
* [Enhancement] Custom voice files can now use conditions and kind. E.g. `joe-chris_radio.ogg` or `joe-nojill.ogg`.
* [Enhancement] Inventory randomization can now be toggled on or off.
* [Enhancement] Speed up music randomization by utilizing all processor cores.
* [Change] Hunk model updated to be more similar to RE 2 Remake.
* [Change] Normalize volume of William's voice lines.
* [Change] Random inventories can now contain mixed herbs and ammo for the random weapon.
* [Change] RE 2, enemy ratios for easy difficulty adjusted.
* [Change] RE 2, room 106, enable back door in door randos.
* [Fix] #95: RE 2, room 614, Claire A cutscene softlocks.
* [Fix] #106: RE 1, room 502, possible softlock if cutscene plays.
* [Fix] #114: RE 2, room 409, crash when Annettee is swapped with Sherry (jacket).
* [Fix] #119: RE 1, room 203, Barry cutscene can softlock.
* [Fix] #120: RE 1, room 307, item 3 is only available if Barry is triggered.
* [Fix] #121: RE 1, room 308 Barry cutscene can crash game.
* [Fix] #122: Config button's function doesn't reflect its name.
* [Fix] #123: RE 1, room 30C, item not present.
* [Fix] RE 2, Hurt Ark and William models / textures not correct.
* [Fix] RE 2, main fuse is never placed for Leon door randos.
* [Fix] RE 2, non-door rando, scenario B key items in scenario A start area.
* [Fix] RE 2, room 20E, softlock due to item on other side of mirror.
* [Fix] RE 2, room 214, item may not be in scenario B.
* [Fix] RE 2, room 216, Leon B, enemies may be present during cutscene.
* [Fix] RE 2, room 505, Claire B cutscenes.

BioRand v2.1.3 (2023-01-02)

* [Fix] RE 2, softlock due to door change after cutscene in 60C.

BioRand 2.1.2 (2023-01-02)

* [Fix] RE 2, every seed fails in 2.1.1.

 BioRand 2.1.1 (2023-01-01)

* [Feature] RE 1, implement document/item randomization for RE 1.
* [Feature] RE 2, Add more music tracks from RE 0.
* [Feature] RE 2, Add results music track from RE 5.
* [Feature] RE 2, Add Ark Thompson (RE Survivor) as a playable character and NPC.
* [Feature] RE 2, Add Kevin Ryman (RE Outbreak) as a playable character and NPC.
* [Feature] RE 2, Add Sherry Birkin as a playable character.
* [Feature] RE 2, Add William Birkin as a playable character and NPC.
* [Enhancement] Countdown/alarm music tracks now swapped with another countdown track (non-door rando only).
* [Enhancement] Reduce chance of unused rooms in door rando.
* [Enhancement] RE 1, randomize ladder in room 302 and 406.
* [Enhancement] RE 2, allow randomization of radio to any character (inc. Brad).
* [Enhancement] RE 2, enable room 101 front door in door rando.
* [Enhancement] RE 2, enable room 103 gate in door rando.
* [Enhancement] RE 2, enable room 10C middle door in door rando.
* [Enhancement] RE 2, enable room 11B in door rando.
* [Enhancement] RE 2, enable room 20E in door rando.
* [Enhancement] RE 2, enable room 400 in door rando.
* [Enhancement] RE 2, enable room 404 in door rando.
* [Enhancement] RE 2, enable room 701 in door rando (scenario A).
* [Enhancement] RE 2, force most cutscenes (regardless of scenario) to play in door rando.
* [Change] RE 1, room 20D, make clip low priority.
* [Change] RE 2, room 204, prevent confusion due to change of player position.
* [Change] RE 2, room 703, prevent confusion due to cutscene.
* [Fix] #94: RE 1, using Helmet Key before killing Plant 42 crashes the game.
* [Fix] #102: RE 1, room 405, cutscene crashes when enemies are swapped.
* [Fix] #103: RE 1, room 11A, crests are already placed in door rando.
* [Fix] #104: RE 1, room 208, dog spawn crashes the game.
* [Fix] #105: RE 1, enemy swap with tyrant 2 crashes game.
* [Fix] #107: RE 2, room 20C, 109, tyrant encouters crash the game.
* [Fix] #108: RE 2, room 307, softlock caused by swap with Irons.
* [Fix] #110: RE 2, room 110, moths crash the game.
* [Fix] RE 1, prevent Yawn 2 from spawning anywhere other than lesson room.
* [Fix] RE 2, NPC randomization in room 505.
* [Fix] RE 2, player death sound not randomized.
* [Fix] RE 2, room 216, 301, Ada NPC swap softlocks when partner flag is enabled.
* [Fix] RE 2, room 400, Sherry scream not randomized.
* [Fix] RE 2, room 404, Leon's voice not randomized.
* [Fix] RE 2, room 604, Ada's grunts not randomized.
* [Fix] RE 2, Sherry/swap height positioning in cutscenes.

BioRand v2.0.3 (2022-12-20)

* [Change] RE 1, make x-ray lab room wooden box items low priority.
* [Change] RE 2, room 205, do not randomize enemies.
* [Change] RE 2, room 309, now bi-directional for Leon and remove ladder lock.
* [Change] RE 2, room 301, allow Ben to be swapped with Irons in death cutscene.
* [Fix] #82: Room 104 items are not low priority in RE 1.
* [Fix] #86: RE 1 double doors to plant 42 room is not randomized.
* [Fix] #87: RE 1 Barry Forrest cutscene NPC rando always Barry.
* [Fix] #88: RE 1 softlock with Tyrant 1 elevator cutscene.
* [Fix] #89: RE 2 Vaccine placed in RPD in Claire B.
* [Fix] #90: Improve tooltips for door rando sliders.
* [Fix] #91: RE 1 "file is already in use by another process" error occurs.
* [Fix] RE 1, room 408, snakes are floating.
* [Fix] RE 2, room 608, sherry voice not randomized in room 608.
* [Fix] RE 2, room 115, Leon NPC not randomized for Claire B.
* [Fix] RE 2, potential softlock if manhole cover is open for Claire.
* [Fix] RE 2, room 301, softlock when swapping Ada NPC swap.

BioRand v2.0.2 (2022-12-18)

* [Change] Avoid duplicate actors in include type pool.
* [Change] Allow any NPC for Ada swap in room 300.
* [Fix] Make Barry's gift in room 203 low priority for RE 1.
* [Fix] Improve RE2 directory validation.
* [Fix] Potential issues with room 303.
* [Fix] Update model textures for damage.
* [Fix] #60: Room 409 two music tracks play.
* [Fix] #70: Killing Chimeras crashes RE 1.
* [Fix] #71: BGM tracks larger than 30 MiB crash RE 1.
* [Fix] #74: Room 60C cutscene has no randomized voice for Annette NPC.
* [Fix] #75: Chess piece not locked to clock tower to ensure G boss spawns.
* [Fix] #76: Music is replaced with ambient sounds from RE 1.
* [Fix] #77: Small key spawns with 0 quanity.
* [Fix] #81: Duplicate items when playing with "Shuffle default items".

BioRand v2.0.1 (2022-12-17)

* [Fix] opcode NOP logic not working, caused many crashes, and player swaps.
* [Fix] Annette player model.
* [Fix] RE 2 safe theme replaced with calm music instead of safe music.

BioRand v2.0.0 (2022-12-17)

* Annette, Ben, Brad, Hunk, Irons, Kendo, Marvin are now playable characters.
* Barry, Chris, Enrico, Jill, Rebecca, Richard, and Wesker are now playable characters.
* Barry, Chris, Enrico, Jill, Rebecca, Richard, and Wesker  can now be NPCs (requires RE1 game data).
* Beretta now available for all male and female playable charactes.
* Bowgun, Colt, Grenade Launcher, and Sparkshot are now available for all male playable characters.
* Resident Evil 1 door randomizer now implemented.
* Boss music tracks now shuffled separately from danger music tracks.
* Additional music tracks from RE 2 remake added.
* Music tracks can now be shuffled between Resident Evil 1 and 2.
* [Fix] #61: "You Unlocked It" Freeze at ventilation shaft.
* [Fix] #64: Room 200 rando assumed ladder was down.
* [Fix] #66: Room 216 Softlock possible when NPC is Claire or Ben.
* [Fix] #68: Room 700 for B scenario crashes in door rando.

BioRand v1.3.3 (2022-12-11)

* [Fix] Final door locked in scenario A
* [Fix] Clipped audio samples are silent when using HQ sound pack

BioRand v1.3.2 (2022-12-10)

* [Change] Some underlying logic has been changed to cater for upcoming RE1 randomizer.
* [Enhancement] Characters now avoid saying a voice line that directly mentions a character not in the room.
* [Enhancement] Improved non-key item distribution.
* [Fix] #42: Room 300 voice incorrect for Ben
* [Fix] #54: Allow alternative routes can cause softlock with Leon
* [Fix] #56: Room 504 Two BGM tracks are playing at the same time
* [Fix] #57 Lift in pump room is often in incorrect state
* [Fix] #58 Room 60E lickers can be knocked off platform

BioRand v1.3.0 (2022-12-05)

* [Feature] Add buttons to view the generation log
* [Feature] Add some extra BGM from RE4
* [Feature] Add some extra save room BGM from various RE games
* [Feature] Add visualization for enemy types
* [Enhancement] Add random chance of Brad zombies
* [Enhancement] Include Claire A vaccine related items in door randos
* [Enhancement] Do not spawn keys for unconnected doors
* [Fix] #33: Room 216 Annette swap causes softlock
* [Fix] #43: Room 21B revisiting the room in a door rando can cause incorrect camera angle
* [Fix] #44: Room 10E, 216, 500 Irons can cause cutscene softlock
* [Fix] #45: Room 20A, 20B moths crash the game
* [Fix] #46: Room 60A chute doesn't always require the lighter
* [Fix] #47: Room 40E should not be left unconnected
* [Fix] #48: Room 608, 617 play two music tracks
* [Fix] #49: Room 401 sometimes camera angle is wrong when going down ladder
* [Fix] #50: Room 40E Claire B softlock if Sherry partner flag is already set
* [Fix] #51: Room 30B Ada model glitch due to NPC swap
* [Fix] #52: Room 606 softlock due to "can't go back now" message
* [Fix] #53: Room 40A softlock caused by partner flag during aligator fight
* [Fix] Browse dialog crash if initial directory does not exist
* [Fix] Inconsistent text size on generated background
* [Change] Make documents in stars office low priorty
* [Change] Prevent plugs from spawning in room 701
* [Change] Increase chance of rooms with cutscenes being included in door randos
* [Change] Reduce chance of same NPC as player and force NPCs to be swapped to a different character
* [Change] Reduce 'S.T.A.R.S. room' desk inspection from 50 down to 5
* [Change] Rename 'Prevent soft lock' to 'Safe key placement'

BioRand v1.2.2 (2022-11-25)

* [Enhancement] Allow previously disabled locker items to be randomized
* [Enhancement] Guarantee weapons for harder enemy difficulties
* [Enhancement] Make the seed font clearer on the background
* [Enhancement] Randomize NPCs for room 702
* [Fix] #31: Room 219 unable to go through door after triggering Ben cutscene
* [Fix] #32: First licker in corridor is silent
* [Fix] #34: Item 501:2 should require the lighter
* [Fix] #35: Setting all item ratios to 0 crashes biorand and will not open again
* [Fix] #36: Room 603 radio cutscene causes incorrect camera angle in door rando
* [Fix] #38: No grenade rounds are generated
* [Fix] #39: Room 40E Claire B Sherry cutscene sometimes crashes the game

BioRand v1.2.1 (2022-11-22)

* [Feature] Add a pie chart to user interface to show item pickup distribution
* [Feature] Add version and seed info to in-game backgrounds
* [Enhancement] Allow more enemy types for Leon in room 21A
* [Fix] #22: Sparkshot pickup always has only 1% ammo
* [Fix] #23: Room 21B elevator glitches when unlocked
* [Fix] #24: Room 60C for door rando has a camera glitch
* [Fix] #25: Room 402 soft lock caused by sherry NPC switch
* [Fix] #26: Room 614 soft lock possible if document is replaced in door rando with key item
* [Fix] #27: Room 213 door rando causes Japanese translation to crash when inspecting puzzle
* [Fix] #28: Room 208 document is not randomized
* [Fix] #30: In some rooms, replaced enemies are silent

BioRand v1.2.0 (2022-11-21)

* [Feature] Add documentation link to user interface
* [Feature] Add a file integrity warning when generating a rando
* [Feature] Add an ETA to the user interface for door rando
* [Enhancement] Avoid identical seeds for both scenarios
* [Enhancement] Prevent lickers, dogs, tyrants in crowded rooms for easy enemy difficulty
* [Enhancement] Room 60A is now a bridge node.
* [Fix] #6: Hall ladder is an unlock
* [Fix] #8: Room 60A to 609 link breaks door rando
* [Fix] #9: Room 404 crash due to NPC swap
* [Fix] #10: Room 10A licker replacement enemy is not on ground
* [Fix] #11: Room 30D exiting as Claire switches player to Sherry
* [Fix] #12: Room 502 incorrect camera angle for Claire
* [Fix] #14: Room 109, 20C tyrant encounters cause camera glitches and soft lock
* [Fix] #15: Some door lock IDs were duplicated causing some doors to not require a key or randomly unlock
* [Fix] #16: Soft lock may occur when 'alternative routes' is enabled
* [Fix] #18: Room 601 soft lock due to early Sherry spawn
* [Fix] #19: Room 608 cutscene locks doors and can cause soft lock
* [Fix] #20: Room 216 Claire door rando connects blocked door causing soft lock
* [Fix] Room 409 crash due to NPC swap
* [Fix] Room 612 crash due to NPC swap
* [Fix] NPCs that did not get swapped would keep same voice lines
* [Fix] NPCs that could get swapped, always got swapped

BioRand v1.1.0 (2022-11-18)

* [Feature] Door randomization can now be enabled
* [Feature] Show more helpful error message if access is denied
* [Fix] #1: G-Virus is swapped when enabling 'Shuffle default items'
* [Fix] #2: Error is given when setting all item ratios to 0
* [Fix] #3: Room 410, NPC change can crash
* [Fix] #4: Room 301, NPC change can soft lock
* [Fix] #5: Randomize optional reverse-player sections
* [Fix] 3 small keys are now placed for Leon B
* [Fix] Randomize Fuse Case

BioRand v1.0.1 (2022-11-06)

* Fix generation for game structures that do not contain a data directory.

BioRand 1.0.0 (2022-11-03)

* First release of BioRand, with initial support for Resident Evil 2.
