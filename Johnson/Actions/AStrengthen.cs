﻿using FSPRO;
using System.Collections.Generic;

namespace Shockah.Johnson;

public sealed class AStrengthen : CardAction
{
	public required int CardId;
	public required int Amount;

	public override void Begin(G g, State s, Combat c)
	{
		base.Begin(g, s, c);
		timer = 0;
		if (s.FindCard(CardId) is not { } card)
			return;
		card.AddStrengthen(Amount);
		Audio.Play(Event.Status_PowerUp);
	}

	public override Icon? GetIcon(State s)
		=> new(ModEntry.Instance.StrengthenIcon.Sprite, Amount, Colors.textMain);

	public override List<Tooltip> GetTooltips(State s)
		=> [
			new CustomTTGlossary(
				CustomTTGlossary.GlossaryType.action,
				() => ModEntry.Instance.StrengthenIcon.Sprite,
				() => ModEntry.Instance.Localizations.Localize(["action", "Strengthen", "name"]),
				() => ModEntry.Instance.Localizations.Localize(["action", "Strengthen", "description"], new { Damage = Amount }),
				key: $"{ModEntry.Instance.Package.Manifest.UniqueName}::Strengthen"
			)
		];
}
