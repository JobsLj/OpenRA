#region Copyright & License Information
/*
 * Copyright 2007-2015 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see COPYING.
 */
#endregion

using System.Collections.Generic;
using System.Linq;
using OpenRA.Effects;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.RA.Effects;
using OpenRA.Traits;

namespace OpenRA.Mods.RA.Traits
{
	[Desc("Required for `GpsPower`. Attach this to the player actor.")]
	class GpsWatcherInfo : ITraitInfo
	{
		public object Create(ActorInitializer init) { return new GpsWatcher(init.Self.Owner); }
	}

	interface IOnGpsRefreshed { void OnGpsRefresh(Actor self, Player player); }

	class GpsWatcher : ISync, IFogVisibilityModifier
	{
		[Sync] public bool Launched { get; private set; }
		[Sync] public bool GrantedAllies { get; private set; }
		[Sync] public bool Granted { get; private set; }
		public readonly Player Owner;

		readonly List<Actor> actors = new List<Actor>();
		readonly HashSet<TraitPair<IOnGpsRefreshed>> notifyOnRefresh = new HashSet<TraitPair<IOnGpsRefreshed>>();

		public GpsWatcher(Player owner) { Owner = owner; }

		public void GpsRem(Actor atek)
		{
			actors.Remove(atek);
			RefreshGps(atek);
		}

		public void GpsAdd(Actor atek)
		{
			actors.Add(atek);
			RefreshGps(atek);
		}

		public void Launch(Actor atek, SupportPowerInfo info)
		{
			atek.World.Add(new DelayedAction(((GpsPowerInfo)info).RevealDelay * 25,
				() =>
				{
					Launched = true;
					RefreshGps(atek);
				}));
		}

		public void RefreshGps(Actor atek)
		{
			RefreshGranted();

			foreach (var i in atek.World.ActorsWithTrait<GpsWatcher>())
				i.Trait.RefreshGranted();

			if ((Granted || GrantedAllies) && atek.Owner.IsAlliedWith(Owner))
				atek.Owner.Shroud.ExploreAll(atek.World);
		}

		void RefreshGranted()
		{
			var wasGranted = Granted;
			var wasGrantedAllies = GrantedAllies;

			Granted = actors.Count > 0 && Launched;
			GrantedAllies = Owner.World.ActorsHavingTrait<GpsWatcher>(g => g.Granted).Any(p => p.Owner.IsAlliedWith(Owner));

			if (Granted || GrantedAllies)
				Owner.Shroud.ExploreAll(Owner.World);

			if (wasGranted != Granted || wasGrantedAllies != GrantedAllies)
				foreach (var tp in notifyOnRefresh.ToList())
					tp.Trait.OnGpsRefresh(tp.Actor, Owner);
		}

		public bool HasFogVisibility()
		{
			return Granted || GrantedAllies;
		}

		public bool IsVisible(Actor actor)
		{
			var gpsDot = actor.TraitOrDefault<GpsDot>();
			if (gpsDot == null)
				return false;

			return gpsDot.IsDotVisible(Owner);
		}

		public void RegisterForOnGpsRefreshed(Actor actor, IOnGpsRefreshed toBeNotified)
		{
			notifyOnRefresh.Add(new TraitPair<IOnGpsRefreshed>(actor, toBeNotified));
		}

		public void UnregisterForOnGpsRefreshed(Actor actor, IOnGpsRefreshed toBeNotified)
		{
			notifyOnRefresh.Remove(new TraitPair<IOnGpsRefreshed>(actor, toBeNotified));
		}
	}
}
