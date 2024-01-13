﻿using Newtonsoft.Json;
using Nickel;
using Shockah.Shared;
using System.Collections.Generic;
using System.Reflection;

namespace Shockah.Dracula;

internal sealed class BatFormCard : Card, IDraculaCard
{
	[JsonProperty]
	public int FlipIndex { get; private set; } = 0;

	[JsonProperty]
	private bool LastFlipped { get; set; }

	public float ActionSpacingScaling
		=> 2f;

	public static void Register(IModHelper helper)
	{
		helper.Content.Cards.RegisterCard("BatForm", new()
		{
			CardType = MethodBase.GetCurrentMethod()!.DeclaringType!,
			Meta = new()
			{
				deck = ModEntry.Instance.DraculaDeck.Deck,
				rarity = Rarity.common,
				upgradesTo = [Upgrade.A, Upgrade.B]
			},
			Name = ModEntry.Instance.AnyLocalizations.Bind(["card", "BatForm", "name"]).Localize
		});
	}

	public override CardData GetData(State state)
		=> new()
		{
			cost = upgrade == Upgrade.A ? 0 : 1,
			floppable = true
		};

	public override void ExtraRender(G g, Vec v)
	{
		base.ExtraRender(g, v);
		if (LastFlipped != flipped)
		{
			LastFlipped = flipped;
			FlipIndex = (FlipIndex + 1) % (upgrade == Upgrade.B ? 3 : 4);
		}
	}

	public override List<CardAction> GetActions(State s, Combat c)
		=> upgrade switch
		{
			Upgrade.B => [
				new AMove
				{
					targetPlayer = true,
					dir = 1,
					isRandom = true
				}.Disabled(FlipIndex % 3 != 0),
				new AMove
				{
					targetPlayer = true,
					dir = 2,
					isRandom = true
				}.Disabled(FlipIndex % 3 != 1),
				new AMove
				{
					targetPlayer = true,
					dir = 3,
					isRandom = true
				}.Disabled(FlipIndex % 3 != 2)
			],
			_ => [
				new AMove
				{
					targetPlayer = true,
					dir = 1,
					ignoreFlipped = true
				}.Disabled(FlipIndex % 4 != 0),
				new AMove
				{
					targetPlayer = true,
					dir = 2,
					ignoreFlipped = true
				}.Disabled(FlipIndex % 4 != 1),
				new AMove
				{
					targetPlayer = true,
					dir = -2,
					ignoreFlipped = true
				}.Disabled(FlipIndex % 4 != 2),
				new AMove
				{
					targetPlayer = true,
					dir = -1,
					ignoreFlipped = true
				}.Disabled(FlipIndex % 4 != 3)
			]
		};
}
