﻿//--- Aura Script -----------------------------------------------------------
// Keychest 3 Mimic Puzzle
//--- Description -----------------------------------------------------------
// Spawns a chest in a alley with 3 mimics, key chest and mobs.
//---------------------------------------------------------------------------

using Aura.Channel.Scripting.Scripts;
using Aura.Channel.World.Dungeons.Props;
using Aura.Channel.World.Dungeons.Puzzles;
using Aura.Channel.World.Entities;

[PuzzleScript("keychest_3mimic")]
internal class Keychest3Mimic : PuzzleScript
{
	public override void OnPrepare(Puzzle puzzle)
	{
		var lockedPlace = puzzle.NewPlace("LockedPlace");
		var chestPlace = puzzle.NewPlace("ChestPlace");

		lockedPlace.DeclareLock();
		chestPlace.DeclareUnlock(lockedPlace);
		chestPlace.ReservePlace();
	}

	public override void OnPuzzleCreate(Puzzle puzzle)
	{
		var lockedPlace = puzzle.GetPlace("LockedPlace");
		var chestPlace = puzzle.GetPlace("ChestPlace");

		lockedPlace.CloseAllDoors();

		var key = puzzle.LockPlace(lockedPlace, "Lock");

		var chest = new Chest(puzzle, "KeyChest");
		chest.Add(Item.Create(id: 2000, amountMin: 10, amountMax: 30));
		chest.Add(key);
		chestPlace.AddProp(chest, DungeonPropPositionType.Corner4);

		chestPlace.SpawnSingleMob("Trap", "Mob3", DungeonPropPositionType.Corner4);
		chestPlace.SpawnSingleMob("ChestMob1", "Mob1");
		chestPlace.SpawnSingleMob("ChestMob2", "Mob2");
	}
}