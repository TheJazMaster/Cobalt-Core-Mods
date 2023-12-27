﻿using HarmonyLib;
using Microsoft.Extensions.Logging;
using Nanoray.Shrike;
using Nanoray.Shrike.Harmony;
using Shockah.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace Shockah.Soggins;

internal class SmugStatusManager : HookManager<ISmugHook>
{
	private sealed class ExtraApologiesSmugHook : ISmugHook
	{
		public int ModifyApologyAmountForBotchingBySmug(State state, Combat combat, Card card, int amount)
		{
			int extraApologies = state.ship.Get((Status)Instance.ExtraApologiesStatus.Id!.Value);
			return Math.Max(amount + extraApologies, 1);
		}
	}

	private sealed class DoublersLuckSmugHook : ISmugHook
	{
		public double ModifySmugDoubleChance(State state, Ship ship, Card? card, double chance)
			=> chance * (ship.Get((Status)Instance.DoublersLuckStatus.Id!.Value) + 1);
	}

	private sealed class SmugClampStatusLogicHook : IStatusLogicHook
	{
		public int ModifyStatusChange(State state, Combat combat, Ship ship, Status status, int oldAmount, int newAmount)
		{
			if (status != (Status)Instance.SmugStatus.Id!.Value)
				return newAmount;
			return Math.Clamp(newAmount, Instance.Api.GetMinSmug(ship), Instance.Api.GetMaxSmug(ship) + 1);
		}
	}

	private sealed class DoubleTimeSmugFreezeStatusLogicHook : IStatusLogicHook
	{
		public int ModifyStatusChange(State state, Combat combat, Ship ship, Status status, int oldAmount, int newAmount)
		{
			if (status != (Status)Instance.SmugStatus.Id!.Value)
				return newAmount;
			return ship.Get((Status)Instance.DoubleTimeStatus.Id!.Value) > 0 ? oldAmount : newAmount;
		}
	}

	private static ModEntry Instance => ModEntry.Instance;

	internal SmugStatusManager() : base()
	{
		Register(new ExtraApologiesSmugHook(), 0);
		Register(new DoublersLuckSmugHook(), -100);
		Instance.KokoroApi.RegisterStatusLogicHook(new SmugClampStatusLogicHook(), double.MinValue);
		Instance.KokoroApi.RegisterStatusLogicHook(new DoubleTimeSmugFreezeStatusLogicHook(), -1000);
	}

	internal static void ApplyPatches(Harmony harmony)
	{
		harmony.TryPatch(
			logger: Instance.Logger!,
			original: () => AccessTools.DeclaredMethod(typeof(Combat), nameof(Combat.TryPlayCard)),
			transpiler: new HarmonyMethod(typeof(SmugStatusManager), nameof(Combat_TryPlayCard_Transpiler))
		);
		harmony.TryPatch(
			logger: Instance.Logger!,
			original: () => AccessTools.DeclaredMethod(typeof(Ship), "CanBeNegative"),
			postfix: new HarmonyMethod(typeof(SmugStatusManager), nameof(Ship_CanBeNegative_Postfix))
		);
		harmony.TryPatch(
			logger: Instance.Logger!,
			original: () => AccessTools.DeclaredMethod(typeof(Ship), nameof(Ship.Set)),
			prefix: new HarmonyMethod(typeof(SmugStatusManager), nameof(Ship_Set_Prefix))
		);
		harmony.TryPatch(
			logger: Instance.Logger!,
			original: () => AccessTools.DeclaredMethod(typeof(Ship), nameof(Ship.OnBeginTurn)),
			postfix: new HarmonyMethod(AccessTools.DeclaredMethod(typeof(SmugStatusManager), nameof(Ship_OnBeginTurn_Postfix_Last)), Priority.Last)
		);
		harmony.TryPatch(
			logger: Instance.Logger!,
			original: () => AccessTools.DeclaredMethod(typeof(Ship), nameof(Ship.OnAfterTurn)),
			prefix: new HarmonyMethod(AccessTools.DeclaredMethod(typeof(SmugStatusManager), nameof(Ship_OnAfterTurn_Prefix_First)), Priority.First)
		);
		harmony.TryPatch(
			logger: Instance.Logger!,
			original: () => AccessTools.DeclaredMethod(typeof(Card), nameof(Card.GetAllTooltips)),
			postfix: new HarmonyMethod(AccessTools.DeclaredMethod(typeof(SmugStatusManager), nameof(Card_GetAllTooltips_Postfix)), Priority.Normal - 1)
		);
	}

	public double GetSmugBotchChance(State state, Ship ship, Card? card)
	{
		var smug = Instance.Api.GetSmug(state, ship);
		if (smug is null)
			return 0;
		else if (ship.Get((Status)Instance.DoubleTimeStatus.Id!.Value) > 0)
			return 0;
		else if (smug.Value > Instance.Api.GetMaxSmug(ship))
			return 1; // oversmug

		var chance = smug.Value < Instance.Api.GetMinSmug(ship) ? Constants.BotchChances[0] : Constants.BotchChances[smug.Value - Instance.Api.GetMinSmug(ship)];
		foreach (var hook in GetHooksWithProxies(Instance.KokoroApi, state.EnumerateAllArtifacts()))
			chance = hook.ModifySmugBotchChance(state, ship, card, chance);
		return Math.Clamp(chance, 0, 1);
	}

	public double GetSmugDoubleChance(State state, Ship ship, Card? card)
	{
		var smug = Instance.Api.GetSmug(state, ship);
		if (smug is null)
			return 0;
		else if (ship.Get((Status)Instance.DoubleTimeStatus.Id!.Value) > 0)
			return 1;
		else if (smug.Value > Instance.Api.GetMaxSmug(ship))
			return 0; // oversmug

		var chance = smug.Value < Instance.Api.GetMinSmug(ship) ? Constants.DoubleChances[0] : Constants.DoubleChances[smug.Value - Instance.Api.GetMinSmug(ship)];
		foreach (var hook in GetHooksWithProxies(Instance.KokoroApi, state.EnumerateAllArtifacts()))
			chance = hook.ModifySmugDoubleChance(state, ship, card, chance);
		return Math.Clamp(chance, 0, 1);
	}

	internal static SmugResult GetSmugResult(Rand rng, double botchChance, double doubleChance)
	{
		if (botchChance >= 1)
			return SmugResult.Botch;
		if (botchChance <= 0 && doubleChance >= 1)
			return SmugResult.Double;

		var result = rng.Next();
		if (result < botchChance)
			return SmugResult.Botch;
		if (result < botchChance + doubleChance)
			return SmugResult.Double;

		return SmugResult.Normal;
	}

	public static Card GenerateAndTrackApology(State state, Combat combat, Rand rng, bool forDual = false, Type? ignoringType = null)
	{
		var timesApologyWasGiven = Instance.KokoroApi.ObtainExtensionData<Dictionary<string, int>>(combat, "TimesApologyWasGiven");
		var misprintedApologyArtifact = state.EnumerateAllArtifacts().OfType<MisprintedApologyArtifact>().FirstOrDefault();

		ApologyCard apology;
		if (!forDual && misprintedApologyArtifact is not null && !misprintedApologyArtifact.TriggeredThisTurn)
		{
			misprintedApologyArtifact.Pulse();
			misprintedApologyArtifact.TriggeredThisTurn = true;
			Narrative.SpeakBecauseOfAction(GExt.Instance!, combat, $".{misprintedApologyArtifact.Key()}Trigger");
			var firstCard = GenerateAndTrackApology(state, combat, rng, forDual: true);
			var secondCard = GenerateAndTrackApology(state, combat, rng, forDual: true, ignoringType: firstCard.GetType());
			apology = new DualApologyCard
			{
				FirstCard = firstCard,
				SecondCard = secondCard
			};
		}
		else
		{
			WeightedRandom<ApologyCard> weightedRandom = new();
			foreach (var apologyType in ModEntry.ApologyCards)
			{
				if (forDual && apologyType == typeof(DualApologyCard))
					continue;
				if (ignoringType is not null && apologyType == ignoringType)
					continue;

				apology = (ApologyCard)Activator.CreateInstance(apologyType)!;
				var weight = apology.GetApologyWeight(state, combat, timesApologyWasGiven.GetValueOrDefault(apologyType.FullName!));
				if (weight > 0)
					weightedRandom.Add(new(weight, apology));
			}
			apology = weightedRandom.Next(rng);
		}

		int totalApologies = timesApologyWasGiven.Values.Sum() - timesApologyWasGiven.GetValueOrDefault(typeof(DualApologyCard).FullName!);
		if (!forDual)
			apology.ApologyFlavorText = $"<c={I18n.SogginsColor}>{string.Format(I18n.ApologyFlavorTexts[rng.NextInt() % I18n.ApologyFlavorTexts.Length], totalApologies)}</c>";
		timesApologyWasGiven[apology.GetType().FullName!] = timesApologyWasGiven.GetValueOrDefault(apology.GetType().FullName!) + 1;
		return apology;
	}

	private static IEnumerable<CodeInstruction> Combat_TryPlayCard_Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase originalMethod)
	{
		try
		{
			return new SequenceBlockMatcher<CodeInstruction>(instructions)
				.Find(
					ILMatches.Ldloc<CardData>(originalMethod).CreateLdlocaInstruction(out var ldlocaCardData),
					ILMatches.Ldfld("exhaust"),
					ILMatches.Ldarg(4),
					ILMatches.Instruction(OpCodes.Or),
					ILMatches.Stloc<bool>(originalMethod).CreateLdlocaInstruction(out var ldlocaExhaust)
				)
				.Find(
					ILMatches.Ldloc<List<CardAction>>(originalMethod.GetMethodBody()!.LocalVariables),
					ILMatches.Call("Queue")
				)
				.PointerMatcher(SequenceMatcherRelativeElement.First)
				.Insert(
					SequenceMatcherPastBoundsDirection.After, SequenceMatcherInsertionResultingBounds.IncludingInsertion,
					new CodeInstruction(OpCodes.Ldarg_1),
					new CodeInstruction(OpCodes.Ldarg_0),
					new CodeInstruction(OpCodes.Ldarg_2),
					new CodeInstruction(OpCodes.Ldarg_3),
					ldlocaExhaust,
					ldlocaCardData,
					new CodeInstruction(OpCodes.Call, AccessTools.DeclaredMethod(typeof(SmugStatusManager), nameof(Combat_TryPlayCard_Transpiler_ModifyActions)))
				)
				.AllElements();
		}
		catch (Exception ex)
		{
			Instance.Logger!.LogError("Could not patch method {Method} - {Mod} probably won't work.\nReason: {Exception}", originalMethod, Instance.Name, ex);
			return instructions;
		}
	}

	private static List<CardAction> Combat_TryPlayCard_Transpiler_ModifyActions(List<CardAction> actions, State state, Combat combat, Card card, bool playNoMatterWhatForFree, ref bool exhaust, ref CardData data)
	{
		if (playNoMatterWhatForFree)
			return actions;

		var handlingHook = Instance.FrogproofManager.GetHandlingHook(state, combat, card, FrogproofHookContext.Action);
		var frogproofType = handlingHook?.GetFrogproofType(state, combat, card, FrogproofHookContext.Action) ?? FrogproofType.None;
		if (frogproofType is FrogproofType.Innate or FrogproofType.InnateHiddenIfNotNeeded)
			return actions;

		double botchChance = Instance.Api.GetSmugBotchChance(state, state.ship, card);
		if (frogproofType == FrogproofType.Paid && botchChance > 0)
		{
			handlingHook?.PayForFrogproof(state, combat, card);
			return actions;
		}

		var deck = card.GetMeta().deck;
		var deckKey = deck == Deck.colorless ? "comp" : deck.Key();

		double doubleChance = Instance.Api.GetSmugDoubleChance(state, state.ship, card);
		var result = GetSmugResult(state.rngActions, botchChance, doubleChance);
		var swing = Math.Max(card.GetCurrentCost(state), 1);
		switch (result)
		{
			case SmugResult.Botch:
				exhaust = false;
				data.singleUse = false;

				actions.Clear();
				actions.Add(new AStatus
				{
					status = (Status)Instance.BotchesStatus.Id!.Value,
					statusAmount = 1,
					targetPlayer = true
				});

				bool isOversmug = Instance.Api.IsOversmug(state, state.ship);
				actions.Add(new AStatus
				{
					status = (Status)Instance.SmugStatus.Id!.Value,
					mode = isOversmug ? AStatusMode.Set : AStatusMode.Add,
					statusAmount = isOversmug ? Instance.Api.GetMinSmug(state.ship) : -swing,
					targetPlayer = true
				});

				int apologies = swing;
				foreach (var hook in Instance.SmugStatusManager.GetHooksWithProxies(Instance.KokoroApi, state.EnumerateAllArtifacts()))
					apologies = hook.ModifyApologyAmountForBotchingBySmug(state, combat, card, apologies);
				
				for (int i = 0; i < apologies; i++)
				{
					actions.Add(new AAddCard
					{
						card = GenerateAndTrackApology(state, combat, state.rngActions),
						destination = CardDestination.Hand
					});
				}

				foreach (var hook in Instance.SmugStatusManager.GetHooksWithProxies(Instance.KokoroApi, state.EnumerateAllArtifacts()))
					hook.OnCardBotchedBySmug(state, combat, card);
				QueuedAction.Queue(new QueuedAction()
				{
					WaitForCombatQueueDrain = true,
					Action = () =>
					{
						double? responseDelay = DB.story.QuickLookup(state, $".{Instance.SogginsDeck.GlobalName}_Botch") is null ? null : 1.25;
						Narrative.SpeakBecauseOfAction(GExt.Instance!, combat, $".{Instance.SogginsDeck.GlobalName}_Botch");
						QueuedAction.Queue(new QueuedAction()
						{
							WaitForTotalGameTime = responseDelay is null ? null : Instance.KokoroApi.TotalGameTime.TotalSeconds + responseDelay.Value,
							Action = () => Narrative.SpeakBecauseOfAction(GExt.Instance!, combat, $".{Instance.SogginsDeck.GlobalName}_BotchResponse_{deckKey}")
						});
					}
				});
				break;
			case SmugResult.Double:
				var toAdd = card.GetActionsOverridden(state, combat)
					.Where(a => a is not AEndTurn)
					.ToList();

				var isSpawnAction = actions.SelectMany(Instance.KokoroApi.Actions.GetWrappedCardActionsRecursively)
					.OfType<ASpawn>()
					.Select(a =>
					{
						a.isaacNamesIt = false;
						return a;
					})
					.Any();
				if (isSpawnAction)
					toAdd.Add(new ADroneMove { dir = 1 });

				bool hasDoubleTime = state.ship.Get((Status)Instance.DoubleTimeStatus.Id!.Value) > 0;
				toAdd.Insert(0, new AShakeShip
				{
					statusPulse = hasDoubleTime ? (Status)Instance.DoubleTimeStatus.Id!.Value : (Status)Instance.SmugStatus.Id!.Value
				});
				if (!hasDoubleTime)
					toAdd.Insert(0, new AStatus
					{
						status = (Status)Instance.SmugStatus.Id!.Value,
						statusAmount = swing,
						targetPlayer = true
					});

				actions.InsertRange(0, toAdd);

				foreach (var hook in Instance.SmugStatusManager.GetHooksWithProxies(Instance.KokoroApi, state.EnumerateAllArtifacts()))
					hook.OnCardDoubledBySmug(state, combat, card);
				QueuedAction.Queue(new QueuedAction()
				{
					WaitForCombatQueueDrain = true,
					Action = () =>
					{
						double? responseDelay = DB.story.QuickLookup(state, $".{Instance.SogginsDeck.GlobalName}_Double") is null ? null : 1.25;
						Narrative.SpeakBecauseOfAction(GExt.Instance!, combat, $".{Instance.SogginsDeck.GlobalName}_Double");
						QueuedAction.Queue(new QueuedAction()
						{
							WaitForTotalGameTime = responseDelay is null ? null : Instance.KokoroApi.TotalGameTime.TotalSeconds + responseDelay.Value,
							Action = () =>
							{
								string storyKey = $".{Instance.SogginsDeck.GlobalName}_Double{(isSpawnAction ? "Launch" : "")}Response";
								if (isSpawnAction && DB.story.QuickLookup(state, storyKey) is null)
									storyKey = $".{Instance.SogginsDeck.GlobalName}_DoubleResponse";
								Narrative.SpeakBecauseOfAction(GExt.Instance!, combat, storyKey);
							}
						});
					}
				});
				break;
		}
		return actions;
	}

	private static void Ship_CanBeNegative_Postfix(Status status, ref bool __result)
	{
		if (status == (Status)Instance.SmugStatus.Id!.Value)
			__result = true;
	}

	private static void Ship_Set_Prefix(Ship __instance, Status status, ref int n)
	{
		if (status != (Status)Instance.SmugStatus.Id!.Value)
			return;
		n = Math.Clamp(n, Instance.Api.GetMinSmug(__instance), Instance.Api.GetMaxSmug(__instance) + 1);
	}

	private static void Ship_OnBeginTurn_Postfix_Last(Ship __instance, State s, Combat c)
	{
		if (__instance != s.ship)
			return;

		if (s.ship.Get((Status)Instance.BidingTimeStatus.Id!.Value) > 0)
		{
			c.Queue(new AStatus
			{
				status = (Status)Instance.DoubleTimeStatus.Id!.Value,
				statusAmount = 1,
				targetPlayer = true
			});

			if (s.ship.Get(Status.timeStop) <= 0)
				c.Queue(new AStatus
				{
					status = (Status)Instance.BidingTimeStatus.Id!.Value,
					statusAmount = -1,
					targetPlayer = true
				});
		}

		int constantApologies = s.ship.Get((Status)Instance.ConstantApologiesStatus.Id!.Value);
		for (int i = 0; i < constantApologies; i++)
			c.Queue(new AAddCard
			{
				card = GenerateAndTrackApology(s, c, s.rngActions),
				destination = CardDestination.Hand,
				statusPulse = (Status)Instance.ConstantApologiesStatus.Id!.Value
			});
	}

	private static void Ship_OnAfterTurn_Prefix_First(Ship __instance, State s, Combat c)
	{
		if (__instance != s.ship)
			return;

		if (s.ship.Get((Status)Instance.DoubleTimeStatus.Id!.Value) > 0)
		{
			c.Queue(new AStatus
			{
				status = (Status)Instance.DoubleTimeStatus.Id!.Value,
				mode = AStatusMode.Set,
				statusAmount = 0,
				targetPlayer = true
			});
		}
	}

	private static void Card_GetAllTooltips_Postfix(Card __instance, G g, State s, bool showCardTraits, ref IEnumerable<Tooltip> __result)
	{
		if (__instance is not ApologyCard apology)
			return;
		if (string.IsNullOrEmpty(apology.ApologyFlavorText))
			return;
		var tooltipToYield = new TTText(apology.ApologyFlavorText);

		IEnumerable<Tooltip> ModifyTooltips(IEnumerable<Tooltip> tooltips)
		{
			bool yieldedFlavorText = false;

			foreach (var tooltip in tooltips)
			{
				if (!yieldedFlavorText && tooltip is TTGlossary glossary && glossary.key.StartsWith("cardtrait.") && glossary.key != "cardtrait.unplayable")
				{
					yield return tooltipToYield;
					yieldedFlavorText = true;
				}
				yield return tooltip;
			}

			if (!yieldedFlavorText)
				yield return tooltipToYield;
		}

		__result = ModifyTooltips(__result);
	}
}
