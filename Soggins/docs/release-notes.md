[← back to readme](README.md)

# Release notes

## 1.3.1
Released 14 February 2024.

* Fixed Soggins name being always colored in various places (causing issues in other places).
* Fixed Smug rolling for RNG even if there was no chance for a botch nor double (in a no-Smug run).

## 1.3.0
Released 10 February 2024.

* Now comes with a `nickel.json` file.
* Added Dracula Blood Tap integration.
* Added alternative starters (More Difficulties integration).
* API improvements for Nickel mods.
* Fixed some dialogue never triggering.

## 1.2.2
Released 6 January 2024.

* Soggins.EXE now guarantees either a Smugness Control or Harnessing Smugness as one of the choices.
* Infinite and Recycle card traits now obey botches (the card goes to the discard pile).
* Fixed character-specific botching dialogue not triggering.
* Updated to the latest Kokoro.

## 1.2.1
Released 28 December 2023.

* Fixed Isaac-specific launch action doubling dialogue not being triggered.
* Fixed Cleo dialogue.

## 1.2.0
Released 28 December 2023.

* Added dialogue.
* Added Soggins.EXE.
* Rebalanced Extra Apology.
* Changed apology card weighting to be more chaotic.
* For modders: Added an API to get Soggins' `ExternalDeck`.

## 1.1.3
Released 18 December 2023.

* Fixed Smug being active on ALL runs.
* Fixed Frogproof randomly visually disappearing from non-Soggins cards (mechanically they were still affected).
* Fixed top/bottom art swapping on dual Halfhearted Apology which include an instant move action.

## 1.1.2
Released 17 December 2023.

* Fixed non-Soggins Frogproof cards not actually being Frogproof, even though they showed up as so.

## 1.1.1
Released 17 December 2023.

* Fixed a crash when creating a Halfhearted Apology card.

## 1.1.0
Released 17 December 2023.

* Rebalanced Stop It! and made it uncommon.
* Humiliating Attack is now common.
* I'm Trying! now has Frogproof.
* Rebalanced Harnessing Smugness.
* All non-character, non-ephemeral cards are now Frogproof.
* Changed Oversmug display to be more clear about the current state.
* Fixed Single Use cards being gone when botched, instead of being discarded.
* Frogproof no longer displays on cards in runs where it does not matter.
* The double odds tooltip now takes into account botch odds (as botch can't be avoided by stacking up the double chance).
* Fixed Shard tooltip on the Books duo artifact not appearing correctly on the latest game beta.
* "Smugged" (Smug being enabled) is no longer technically a status. This normally does not matter, but it might with other mods, like "Dizzy?"'s cards.

## 1.0.1
Released 15 December 2023.

* Bumped the botch/double odds by 2%.
* Removed odds config.

## 1.0.0
Released 15 December 2023.

* Initial release.