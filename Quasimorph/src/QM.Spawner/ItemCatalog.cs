using System;
using System.Collections.Generic;
using MGSC;

namespace QM.Spawner;

internal readonly struct CatalogEntry
{
	public readonly string Id;
	public readonly string DisplayName;
	private readonly string _searchBlob;

	public CatalogEntry(string id, string displayName)
	{
		Id = id;
		DisplayName = displayName;
		_searchBlob = (id + " " + displayName).ToLowerInvariant();
	}

	public bool Matches(string lowercaseQuery) => _searchBlob.Contains(lowercaseQuery);
}

/// <summary>
/// The item list and the current search results, both cached. <c>OnGUI</c> runs several times per
/// frame, so neither the catalogue scan nor the filter may happen inside it on every call.
/// </summary>
internal sealed class ItemCatalog
{
	private readonly List<CatalogEntry> _all = new List<CatalogEntry>();
	private readonly List<CatalogEntry> _filtered = new List<CatalogEntry>();

	private bool _built;
	private bool _buildAttempted;
	private bool _lastHideInternal;

	/// <summary>Null means "no filter has run yet", which no real query can equal.</summary>
	private string _lastQuery;

	public IReadOnlyList<CatalogEntry> Filtered => _filtered;
	public int TotalCount => _all.Count;
	public bool IsBuilt => _built;

	/// <summary>Scans <c>Data.Items.Records</c> once. Failure is remembered, so a build that came too early does not retry every frame.</summary>
	public void Build(bool hideInternalItems)
	{
		_all.Clear();
		_built = false;
		_buildAttempted = true;
		_lastHideInternal = hideInternalItems;
		_lastQuery = null;

		if (Data.Items?.Records == null)
		{
			Cfg.Log.LogWarning("Item data is not loaded yet; catalogue left empty.");
			return;
		}

		foreach (BasePickupItemRecord record in Data.Items.Records)
		{
			if (record?.Id == null || (hideInternalItems && IsInternal(record)))
			{
				continue;
			}

			_all.Add(new CatalogEntry(record.Id, ResolveDisplayName(record.Id)));
		}

		_all.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
		_built = true;

		Cfg.Log.LogInfo($"Item catalogue built: {_all.Count} entries (hideInternalItems={hideInternalItems}).");
	}

	/// <summary>Recomputes the visible list only when the query or the filter setting actually changed.</summary>
	public void Refresh(string query, bool hideInternalItems)
	{
		if (!_buildAttempted || hideInternalItems != _lastHideInternal)
		{
			Build(hideInternalItems);
		}

		query ??= string.Empty;
		if (query == _lastQuery)
		{
			return;
		}

		_lastQuery = query;
		_filtered.Clear();

		string needle = query.Trim().ToLowerInvariant();
		bool matchAll = needle.Length == 0;

		foreach (CatalogEntry entry in _all)
		{
			if (matchAll || entry.Matches(needle))
			{
				_filtered.Add(entry);
			}
		}
	}

	/// <summary>Forces a fresh scan on the next <see cref="Refresh"/>.</summary>
	public void Invalidate()
	{
		_built = false;
		_buildAttempted = false;
	}

	/// <summary>
	/// Mirrors the exclusions the game's own <c>allitems</c> debug command applies: placeholder
	/// records, and the implicit weapon/ammo records that only exist as parts of other items.
	/// </summary>
	private static bool IsInternal(BasePickupItemRecord record)
	{
		if (record.Id.Contains("_custom") || record.Id.Contains("placeholder_ammo"))
		{
			return true;
		}

		if (record is CompositeItemRecord composite)
		{
			WeaponRecord weapon = composite.GetRecord<WeaponRecord>();
			if (weapon != null && weapon.IsImplicit)
			{
				return true;
			}

			AmmoRecord ammo = composite.GetRecord<AmmoRecord>();
			if (ammo != null && ammo.IsImplictedAmmo)
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>
	/// Localization.Get returns the key itself when there is no translation, so an untranslated
	/// item falls back to showing nothing extra rather than a raw "item.foo.name" string.
	/// </summary>
	private static string ResolveDisplayName(string id)
	{
		try
		{
			string key = "item." + id + ".name";
			string name = Localization.Get(key, warnIfMissingTag: false);
			return (string.IsNullOrEmpty(name) || name == key) ? string.Empty : name;
		}
		catch (Exception)
		{
			return string.Empty;
		}
	}
}
