﻿namespace Shockah.Soggins;

public partial interface IKokoroApi
{
	void RegisterStatusLogicHook(IStatusLogicHook hook, double priority);
	void UnregisterStatusLogicHook(IStatusLogicHook hook);
}

public interface IStatusLogicHook
{
	int ModifyStatusChange(State state, Combat combat, Ship ship, Status status, int oldAmount, int newAmount) => newAmount;
	bool? IsAffectedByBoost(State state, Combat combat, Ship ship, Status status) => null;
}