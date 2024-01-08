﻿using Nickel;
using System.Collections.Generic;

namespace Shockah.Dracula;

internal sealed class SecretProtectiveCard : SecretCard, IDraculaCard
{
	public override void Register(IModHelper helper)
	{
		helper.Content.Cards.RegisterCard("Secret.Protective", new()
		{
			CardType = GetType(),
			Meta = new()
			{
				deck = ModEntry.Instance.DraculaDeck.Deck,
				rarity = Rarity.common,
				upgradesTo = [Upgrade.A],
				dontOffer = true
			},
			Name = ModEntry.Instance.AnyLocalizations.Bind(["card", "Secret", "Protective", "name"]).Localize
		});
	}

	public override List<CardAction> GetActions(State s, Combat c)
		=> [
			new AStatus
			{
				targetPlayer = true,
				status = Status.shield,
				statusAmount = 2
			}
		];
}
