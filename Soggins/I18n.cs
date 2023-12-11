﻿namespace Shockah.Soggins;

internal static class I18n
{
	public static string SogginsName => "Soggins";
	public static string SogginsDescription => "<c=soggins>SOGGINS</c>\nThis is that frog who keeps making mistakes. Why did we let him on the ship?";

	public static string SmugArtifactName => "Smug";
	public static string SmugArtifactDescription => "Start each combat with <c=status>SMUG</c>.";

	public static string SmugStatusName => "Smug";
	public static string SmugStatusDescription => "The current level of <c=soggins>Soggins'</c> smugness, which affects his chance to <c=cheevoGold>double</c> or <c=downside>botch</c> card effects.\nLow smugness will <c=downside>botch</c> more often, while high smugness will <c=cheevoGold>double</c> more often.\n<c=downside>Beware of reaching max smugness, as that WILL GUARANTEE a botch and set smugness to 0.</c>";

	public static string ApologyCardName => "Halfhearted Apology";

	public static string SmugnessControlCardName => "Smugness Control";
	public static string PressingButtonsCardName => "Pressing Buttons";
	public static string TakeCoverCardName => "Take Cover!";
	public static string ZenCardName => "Zen";
	public static string ZenCardText => "Reset your <c=status>SMUG</c>.";
	public static string HarnessingSmugnessCardName => "Harnessing Smugness";
}
