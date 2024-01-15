﻿using Newtonsoft.Json;
using Nickel;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Shockah.Dracula;

internal sealed class EcholocationCard : Card, IDraculaCard
{
	public static void Register(IModHelper helper)
	{
		helper.Content.Cards.RegisterCard("Echolocation", new()
		{
			CardType = MethodBase.GetCurrentMethod()!.DeclaringType!,
			Meta = new()
			{
				deck = ModEntry.Instance.DraculaDeck.Deck,
				rarity = Rarity.uncommon,
				upgradesTo = [Upgrade.A, Upgrade.B]
			},
			Name = ModEntry.Instance.AnyLocalizations.Bind(["card", "Echolocation", "name"]).Localize
		});

		helper.Events.RegisterBeforeArtifactsHook(nameof(Artifact.OnTurnEnd), (State s, Combat c) =>
		{
			if (!ModEntry.Instance.KokoroApi.TryGetExtensionData(c, "EcholocationReturnPosition", out int echolocationReturnPosition))
				return;
			c.QueueImmediate(new AMove
			{
				targetPlayer = true,
				dir = echolocationReturnPosition - s.ship.x,
				ignoreFlipped = true,
				ignoreHermes = true,
				isTeleport = true
			});
		}, 0);
	}

	public override CardData GetData(State state)
		=> new()
		{
			cost = upgrade == Upgrade.A ? 0 : 1,
			retain = upgrade == Upgrade.B,
			description = ModEntry.Instance.Localizations.Localize(["card", "Echolocation", "description", upgrade.ToString()])
		};

	public override List<CardAction> GetActions(State s, Combat c)
	{
		var maxCheck = Math.Abs(c.otherShip.x - s.ship.x) + s.ship.parts.Count + c.otherShip.parts.Count;

		int? FindClosestAlignment(bool inactiveCannons, bool nonArmored)
		{
			for (var i = 0; i < maxCheck; i++)
			{
				for (var partIndex = 0; partIndex < s.ship.parts.Count; partIndex++)
				{
					if (s.ship.parts[partIndex].type != PType.cannon)
						continue;
					if (!inactiveCannons && !s.ship.parts[partIndex].active)
						continue;
					if (i != 0 && c.otherShip.GetPartAtWorldX(s.ship.x + partIndex - i) is { } enemyPartLeft)
					{
						if (enemyPartLeft.damageModifier is PDamMod.weak or PDamMod.brittle)
							return -i;
						else if (nonArmored && enemyPartLeft.damageModifier == PDamMod.none)
							return -i;
					}
					if (c.otherShip.GetPartAtWorldX(s.ship.x + partIndex + i) is { } enemyPartRight)
					{
						if (enemyPartRight.damageModifier is PDamMod.weak or PDamMod.brittle)
							return i;
						else if (nonArmored && enemyPartRight.damageModifier == PDamMod.none)
							return i;
					}
				}
			}
			return null;
		}

		int? nullableAlignment = null;
		nullableAlignment ??= FindClosestAlignment(inactiveCannons: false, nonArmored: false);
		nullableAlignment ??= FindClosestAlignment(inactiveCannons: false, nonArmored: true);
		nullableAlignment ??= FindClosestAlignment(inactiveCannons: true, nonArmored: false);
		nullableAlignment ??= FindClosestAlignment(inactiveCannons: true, nonArmored: true);

		List<CardAction> actions = [];
		if (nullableAlignment is { } alignment)
			actions.Add(new AEcholocationMove
			{
				Dir = alignment,
				Return = upgrade == Upgrade.B,
				omitFromTooltips = true,
			});
		actions.Add(
			new ATooltipAction
			{
				Tooltips = [
					new TTGlossary("parttrait.weak"),
					new TTGlossary("parttrait.brittle"),
				]
			}
		);
		return actions;
	}

	public sealed class AEcholocationMove : CardAction
	{
		[JsonProperty]
		public required int Dir;

		[JsonProperty]
		public required bool Return;

		public override void Begin(G g, State s, Combat c)
		{
			base.Begin(g, s, c);
			timer = 0;

			if (Return)
				ModEntry.Instance.KokoroApi.SetExtensionData(c, "EcholocationReturnPosition", s.ship.x);
			c.QueueImmediate(new AMove
			{
				targetPlayer = true,
				dir = Dir,
				ignoreFlipped = true,
				ignoreHermes = true
			});
		}
	}
}