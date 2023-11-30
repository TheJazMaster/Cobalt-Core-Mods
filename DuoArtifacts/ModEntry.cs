﻿using CobaltCoreModding.Definitions;
using CobaltCoreModding.Definitions.ExternalItems;
using CobaltCoreModding.Definitions.ModContactPoints;
using CobaltCoreModding.Definitions.ModManifests;
using HarmonyLib;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Shockah.Shared;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Shockah.DuoArtifacts;

public sealed class ModEntry : IModManifest, IPrelaunchManifest, IApiProviderManifest, ISpriteManifest, IDeckManifest, IArtifactManifest, ICardManifest
{
	internal static ModEntry Instance { get; private set; } = null!;

	public string Name { get; init; } = typeof(ModEntry).Namespace!;
	public IEnumerable<DependencyEntry> Dependencies => new DependencyEntry[] { new DependencyEntry<IModManifest>("Shockah.Kokoro", ignoreIfMissing: false) };

	public DirectoryInfo? GameRootFolder { get; set; }
	public DirectoryInfo? ModRootFolder { get; set; }
	public ILogger? Logger { get; set; }

	internal ExternalDeck DuoArtifactsDeck { get; private set; } = null!;
	internal IKokoroApi KokoroApi { get; private set; } = null!;
	internal readonly DuoArtifactDatabase Database = new();

	internal TimeSpan TotalGameTime;

	private Harmony Harmony { get; set; } = null!;
	private readonly Dictionary<HashSet<string>, ExternalSprite> DuoArtifactSprites = new(HashSet<string>.CreateSetComparer());

	public void BootMod(IModLoaderContact contact)
	{
		Instance = this;
		KokoroApi = contact.GetApi<IKokoroApi>("Shockah.Kokoro")!;
		ReflectionExt.CurrentAssemblyLoadContext.LoadFromAssemblyPath(Path.Combine(ModRootFolder!.FullName, "Shrike.dll"));
		ReflectionExt.CurrentAssemblyLoadContext.LoadFromAssemblyPath(Path.Combine(ModRootFolder!.FullName, "Shrike.Harmony.dll"));

		Harmony = new(Name);
		ArtifactRewardPatches.Apply(Harmony);
		CharacterPatches.Apply(Harmony);
		MGPatches.Apply(Harmony);
		CustomTTGlossary.Apply(Harmony);

		foreach (var definition in DuoArtifactDefinition.Definitions)
			(Activator.CreateInstance(definition.Type) as DuoArtifact)?.ApplyPatches(Harmony);
	}

	public void FinalizePreperations()
	{
		foreach (var definition in DuoArtifactDefinition.Definitions)
			(Activator.CreateInstance(definition.Type) as DuoArtifact)?.ApplyLatePatches(Harmony);
	}

	object? IApiProviderManifest.GetApi(IModManifest requestingMod)
		=> new ApiImplementation(Database);

	public void LoadManifest(ISpriteRegistry registry)
	{
		string namePrefix = $"{typeof(ModEntry).Namespace}.Sprite";
		foreach (var definition in DuoArtifactDefinition.Definitions)
		{
			DuoArtifactSprites[definition.CharacterKeys.Value] = registry.RegisterArtOrThrow(
				id: $"{typeof(ModEntry).Namespace}.Artifact.{string.Join("_", definition.CharacterKeys.Value.OrderBy(key => key))}",
				file: new FileInfo(Path.Combine(ModRootFolder!.FullName, "assets", "Artifacts", $"{definition.AssetName}.png"))
			);
			(Activator.CreateInstance(definition.Type) as DuoArtifact)?.RegisterArt(registry, namePrefix);
		}
	}

	public void LoadManifest(IDeckRegistry registry)
	{
		DuoArtifactsDeck = new(
			globalName: $"{typeof(ModEntry).Namespace}.Deck.Duo",
			deckColor: System.Drawing.Color.White,
			titleColor: System.Drawing.Color.White,
			cardArtDefault: ExternalDeck.GetRaw((int)Deck.colorless).CardArtDefault,
			borderSprite: ExternalDeck.GetRaw((int)Deck.colorless).BorderSprite,
			bordersOverSprite: ExternalDeck.GetRaw((int)Deck.colorless).BordersOverSprite
		);
		registry.RegisterDeck(DuoArtifactsDeck);
	}

	public void LoadManifest(IArtifactRegistry registry)
	{
		foreach (var definition in DuoArtifactDefinition.Definitions)
		{
			ExternalArtifact artifact = new(
				globalName: $"{typeof(ModEntry).Namespace}.Artifact.{string.Join("_", definition.CharacterKeys.Value.OrderBy(key => key))}",
				artifactType: definition.Type,
				sprite: DuoArtifactSprites.GetValueOrDefault(definition.CharacterKeys.Value)!,
				ownerDeck: DuoArtifactsDeck
			);
			artifact.AddLocalisation(definition.Name, definition.Tooltip);
			registry.RegisterArtifact(artifact);
			Database.RegisterDuoArtifact(definition.Type, definition.Characters);
		}
	}

	public void LoadManifest(ICardRegistry registry)
	{
		string namePrefix = $"{typeof(ModEntry).Namespace}.Card";
		foreach (var definition in DuoArtifactDefinition.Definitions)
			(Activator.CreateInstance(definition.Type) as DuoArtifact)?.RegisterCards(registry, namePrefix);
	}
}
