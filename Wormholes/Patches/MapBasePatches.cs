﻿using HarmonyLib;
using Shockah.Shared;
using System;
using System.Linq;

namespace Shockah.Wormholes;

internal static class MapBasePatches
{
	private static ModEntry Instance => ModEntry.Instance;

	public static void Apply(Harmony harmony)
	{
		harmony.TryPatch(
			logger: Instance.Logger!,
			original: () => AccessTools.DeclaredMethod(typeof(MapBase), nameof(MapBase.Populate)),
			postfix: new HarmonyMethod(typeof(MapBasePatches), nameof(MapBase_Populate_Postfix))
		);
	}

	private static void MapBase_Populate_Postfix(MapBase __instance, Rand rng)
	{
		int firstY = __instance.markers.Keys.Min(v => (int)v.y);
		int lastY = __instance.markers.Keys.Max(v => (int)v.y);
		int minX = __instance.markers.Keys.Min(v => (int)v.x);
		int maxX = __instance.markers.Keys.Max(v => (int)v.x);

		int firstPossibleY = firstY + 1;
		int lastPossibleY = lastY - 3;

		Vec? GetRandomPosition(int minY, int maxY, int connectionOffset)
		{
			foreach (var y in Enumerable.Range(minY, maxY - minY + 1).Shuffle(rng))
			{
				foreach (var x in Enumerable.Range(minX, maxX - minX + 1).Shuffle(rng))
				{
					if (__instance.markers.TryGetValue(new(x, y), out var marker) && marker.contents is not null)
						continue;

					bool HasPossiblePath(int offsetX, int offsetY)
					{
						if (!__instance.markers.TryGetValue(new(x + offsetX, y + offsetY), out var marker))
							return false;
						return marker.contents is not null;
					}

					if (!Enumerable.Range(-1, 3).Any(offset => HasPossiblePath(offset, connectionOffset)))
						continue;
					return new(x, y);
				}
			}
			return null;
		}

		(Vec EarlyPosition, Vec LatePosition)? GetRandomPositions()
		{
			for (int i = 0; i < 100; i++)
			{
				var earlyWormholePosition = GetRandomPosition(firstPossibleY, firstPossibleY + 2, -1);
				if (earlyWormholePosition is null)
					continue;

				var lateWormholePosition = GetRandomPosition(Math.Max(lastPossibleY - 2, (int)earlyWormholePosition.Value.y + 4), lastPossibleY, 1);
				if (lateWormholePosition is null)
					continue;

				return (earlyWormholePosition.Value, lateWormholePosition.Value);
			}
			return null;
		}

		var wormholePositions = GetRandomPositions();
		if (wormholePositions is null)
			return;
		var (earlyWormholePosition, lateWormholePosition) = wormholePositions.Value;

		if (!__instance.markers.TryGetValue(earlyWormholePosition, out var earlyWormhole))
		{
			earlyWormhole = new();
			__instance.markers[earlyWormholePosition] = earlyWormhole;
		}
		if (!__instance.markers.TryGetValue(lateWormholePosition, out var lateWormhole))
		{
			lateWormhole = new();
			__instance.markers[lateWormholePosition] = lateWormhole;
		}

		earlyWormhole.contents = new MapWormhole { OtherWormholePosition = lateWormholePosition, IsFurther = false };
		lateWormhole.contents = new MapWormhole { OtherWormholePosition = earlyWormholePosition, IsFurther = true };

		void AddPaths(Marker marker, Vec position)
		{
			for (int offset = -1; offset <= 1; offset++)
			{
				if (__instance.markers.TryGetValue(new(position.x + offset, position.y - 1), out var neighbor) && neighbor.contents is not null)
					neighbor.paths.Add((int)position.x);
				if (__instance.markers.TryGetValue(new(position.x + offset, position.y + 1), out neighbor) && neighbor.contents is not null)
					marker.paths.Add((int)position.x + offset);
			}
		}

		AddPaths(earlyWormhole, earlyWormholePosition);
		AddPaths(lateWormhole, lateWormholePosition);
	}
}
