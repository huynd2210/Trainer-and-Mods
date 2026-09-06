using System;
using System.Collections.Generic;
using FallenAces.NonEnemyNPC;
using UnityEngine;
using UnityEngine.Serialization;

namespace FallenAces;

public class Inventory : MonoBehaviour
{
	public struct Consumable
	{
		public int LeftOverPropDefinitionId;

		public bool HealthPrerequisite;

		public bool UnpickupableByPlayer;

		public bool NoUnderwaterUse;

		public string UseVerb;

		public string HoldVerb;

		public string MessageOnConsumption;

		public PlayerVoiceLines.PickupReaction ReactionOnConsumption;

		public bool StayAfterConsumption;

		public DoomBuilderPropDefinition Definition { get; set; }

		public StatusEffect[] StatusEffects { get; private set; }

		public AudioClip ConsumeClip => Definition.EffectSet.ConsumeClip;

		public void GetStatusEffects(List<int> givenIds)
		{
			StatusEffect[] statusEffects = CommonReferences.StatusEffects;
			for (int num = givenIds.Count - 1; num >= 0; num--)
			{
				int num2 = givenIds[num];
				if (num2 < 0 || num2 >= statusEffects.Length)
				{
					givenIds.RemoveAt(num);
				}
			}
			StatusEffect[] array = new StatusEffect[givenIds.Count];
			for (int i = 0; i < givenIds.Count; i++)
			{
				array[i] = statusEffects[givenIds[i]];
			}
			StatusEffects = array;
		}
	}

	public interface IItem
	{
		public enum ItemType
		{
			Unknown,
			Weapon,
			Consumable,
			Key,
			Unstorable,
			Note,
			Body,
			Ammo
		}

		Sprite WorldSprite { get; }

		GameObject GameObject { get; }

		ItemType Type { get; }

		string Name { get; }

		bool IsBroken { get; }

		bool TwoHanded { get; }

		bool IsStackable => MaxStackCount > 1;

		bool IsMagazine => MaxAmmoInMag > 0;

		int StackCount
		{
			get
			{
				return 0;
			}
			set
			{
			}
		}

		int AmmoInMag
		{
			get
			{
				return 0;
			}
			set
			{
			}
		}

		bool AmmoPouchable => false;

		bool Lunchboxable => false;

		int MagLoadedAmmoDef => 0;

		int MaxAmmoInMag => 0;

		int MaxStackCount => 0;

		int DefinitionId => -1;

		ref Weapon WeaponInfo { get; }

		ref Consumable ConsumableInfo { get; }

		void ChangeDefinition(int newDefinitionId)
		{
			throw new NotImplementedException();
		}

		Sprite GetSlotIconSprite(out bool hasDedicatedIconSprite)
		{
			throw new NotImplementedException();
		}

		bool TryGetKeyComponent(out Key keyComponent)
		{
			return GameObject.TryGetComponent<Key>(out keyComponent);
		}

		void OnPickedUp(Inventory inventory)
		{
			throw new NotImplementedException();
		}

		void GetDroppedFromInventory(Vector3 position, Vector3 direction, float force)
		{
			throw new NotImplementedException();
		}

		void GetDroppedFromInventoryInRandomDirection(Vector3 position, float force)
		{
			throw new NotImplementedException();
		}

		void GetConsumed()
		{
			throw new NotImplementedException();
		}

		void SpawnLeftovers(Vector3 leftoversSpawnPosition, Vector3 directionToThrowAwayLeftovers)
		{
			throw new NotImplementedException();
		}

		void TryDamage(float damage)
		{
			throw new NotImplementedException();
		}

		void TryBreak()
		{
			throw new NotImplementedException();
		}

		void TryRepair()
		{
			throw new NotImplementedException();
		}

		void TryPickup(Inventory inventory, bool silentPickup = false, bool pickingUpWithTwoHands = true)
		{
			throw new NotImplementedException();
		}

		ItemSaveData GetSaveData()
		{
			ItemSaveData result = default(ItemSaveData);
			if (Type == ItemType.Weapon)
			{
				result.GenericInt1 = WeaponInfo.AmmoLeft;
				result.GenericInt2 = WeaponInfo.LastReloadAmmoCount;
				result.GenericBool = WeaponInfo.CurrentlyInAlternateVariant;
			}
			return result;
		}

		void LoadFromSaveData(ItemSaveData saveData)
		{
			if (Type == ItemType.Weapon)
			{
				WeaponInfo.AmmoLeft = saveData.GenericInt1;
				WeaponInfo.LastReloadAmmoCount = saveData.GenericInt2;
				WeaponInfo.UpdateCurrentlyInAlternateVariantFlag(saveData.GenericBool);
			}
		}
	}

	public enum TryAddItemFailReason
	{
		Undefined,
		InventoryIsFull,
		ReachedMaxUnstorables,
		CantCarryAnymoreAmmo
	}

	public enum RemoveFromStackSetting
	{
		DontAffectStack,
		TryRemoveOne,
		TryRemoveAll
	}

	[Serializable]
	public struct ItemSaveData
	{
		public int GenericInt1;

		public int GenericInt2;

		public bool GenericBool;
	}

	public struct Slot
	{
		public enum TypeID
		{
			Normal,
			AmmoPouch,
			Lunchbox
		}

		public IItem Item;

		public TypeID Type;

		public Slot(TypeID type)
		{
			Item = null;
			Type = type;
		}
	}

	public struct Weapon
	{
		private bool _currentlyInAlternateVariant;

		private bool _hasBeenEquipped;

		public FirstPersonWeapon FirstPersonWeapon;

		public int AmmoLeft;

		public int LastReloadAmmoCount;

		public readonly bool CurrentlyInAlternateVariant => _currentlyInAlternateVariant;

		public readonly FirstPersonWeapon CurrentlyActiveFirstPersonWeapon
		{
			get
			{
				if (!_currentlyInAlternateVariant)
				{
					return FirstPersonWeapon;
				}
				return FirstPersonWeapon.AlternateVariant;
			}
		}

		public readonly bool IsOutOfAmmo
		{
			get
			{
				FirstPersonWeapon firstPersonWeapon = (_currentlyInAlternateVariant ? FirstPersonWeapon.AlternateVariant : FirstPersonWeapon);
				if (firstPersonWeapon == null)
				{
					return false;
				}
				if (!firstPersonWeapon.UsesAmmo)
				{
					return false;
				}
				if (AmmoLeft > 0)
				{
					return false;
				}
				return true;
			}
		}

		public readonly bool HasAmmoLeft => AmmoLeft > 0;

		public readonly bool HasBeenEquipped => _hasBeenEquipped;

		public readonly bool TryGetFirstPersonWeapon(out FirstPersonWeapon fpw)
		{
			fpw = CurrentlyActiveFirstPersonWeapon;
			return fpw != null;
		}

		public void UpdateBeenEquippedFlag(bool hasBeenEquipped)
		{
			_hasBeenEquipped = true;
		}

		public void UpdateCurrentlyInAlternateVariantFlag(FirstPersonWeapon firstPersonWeaponEquipped)
		{
			if (!(FirstPersonWeapon == null) && !(firstPersonWeaponEquipped == null))
			{
				_currentlyInAlternateVariant = firstPersonWeaponEquipped == FirstPersonWeapon.AlternateVariant;
			}
		}

		public void UpdateCurrentlyInAlternateVariantFlag(bool currentlyInAlternateVariant)
		{
			_currentlyInAlternateVariant = currentlyInAlternateVariant;
		}

		public void SerializeSaveData(DataSerializer ds)
		{
			ds.Serialize(ref AmmoLeft);
			ds.Serialize(ref _currentlyInAlternateVariant);
			ds.Serialize(ref _hasBeenEquipped);
		}
	}

	[SerializeField]
	[FormerlySerializedAs("_size")]
	private int _initialSize = 10;

	[SerializeField]
	private int _maxUnstorableProps = -1;

	private readonly List<IItem> _unstorableItems = new List<IItem>();

	private readonly List<Slot> _slots = new List<Slot>();

	public bool HasUnstorableItems => _unstorableItems.Count > 0;

	public bool AllNormalSlotsHaveItems
	{
		get
		{
			foreach (Slot slot in _slots)
			{
				if (slot.Type == Slot.TypeID.Normal && slot.Item.IsNullOrDestroyed())
				{
					return false;
				}
			}
			return true;
		}
	}

	public int Capacity => _slots.Count;

	public List<IItem> Keys { get; private set; } = new List<IItem>();

	protected IReadOnlyList<Slot> Slots => _slots;

	public event Action<IItem> KeyUsed;

	public event Action<IItem> ItemAdded;

	public event Action<IItem> ItemRemoved;

	public event Action<int, IItem> ItemAddedToSlot;

	public event Action<int, IItem> ItemRemovedFromSlot;

	public event Action<TryAddItemFailReason> TryAddItemFailed;

	public event Action<int, Slot> SlotAdded;

	public event Action<int, Slot> SlotRemoved;

	public event Action<int, int> SlotsIndicesSwapped;

	public bool HasItems(bool includeWeapons)
	{
		int num = 0;
		for (int i = 0; i < _slots.Count; i++)
		{
			IItem item = _slots[i].Item;
			if (item != null && (item.Type != IItem.ItemType.Weapon || includeWeapons))
			{
				num++;
			}
		}
		num += Keys.Count;
		num += _unstorableItems.Count;
		return num > 0;
	}

	public bool HasSlotItemOfId(int id, out int slotIndex)
	{
		for (int i = 0; i < _slots.Count; i++)
		{
			IItem item = _slots[i].Item;
			if (!item.IsNullOrDestroyed())
			{
				Prop component;
				if (item.GameObject == null)
				{
					Debug.LogWarning("Item " + item.Name + " gameobject doesn't exist?", this);
				}
				else if (item.GameObject.TryGetComponent<Prop>(out component) && component.Definition.Id == id)
				{
					slotIndex = i;
					return true;
				}
			}
		}
		slotIndex = -1;
		return false;
	}

	public bool HasTaggedItem(int tag, out IItem item)
	{
		item = null;
		foreach (Slot slot in _slots)
		{
			if (ItemHasTag(slot.Item))
			{
				item = slot.Item;
				return true;
			}
		}
		foreach (IItem unstorableItem in _unstorableItems)
		{
			if (ItemHasTag(unstorableItem))
			{
				item = unstorableItem;
				return true;
			}
		}
		foreach (IItem key in Keys)
		{
			if (ItemHasTag(key))
			{
				item = key;
				return true;
			}
		}
		return false;
		bool ItemHasTag(IItem item2)
		{
			if (item2.IsNullOrDestroyed())
			{
				return false;
			}
			GameObject gameObject = item2.GameObject;
			if (gameObject.IsNullOrDestroyed())
			{
				return false;
			}
			if (gameObject.TryGetComponent<Prop>(out var component) && component.ThingDefinition.Tag == tag)
			{
				return true;
			}
			if (gameObject.TryGetComponent<Enemy>(out var component2) && component2.ThingDefinition.Tag == tag)
			{
				return true;
			}
			if (gameObject.TryGetComponent<Delia>(out var component3) && component3.ThingDefinition.Tag == tag)
			{
				return true;
			}
			if (gameObject.TryGetComponent<HumanoidNPC>(out var component4) && component4.ThingDefinition.Tag == tag)
			{
				return true;
			}
			if (gameObject.TryGetComponent<IDBDefUser>(out var component5) && component5.ThingDefinition.Tag == tag)
			{
				return true;
			}
			return false;
		}
	}

	public void DropItemInSlot(int slot, Vector3 position, Vector3 direction, List<IItem> itemsDropped = null)
	{
		IItem itemInSlot = GetItemInSlot(slot);
		if (itemInSlot != null)
		{
			DropItem(itemInSlot, position, direction, RemoveFromStackSetting.TryRemoveAll, itemsDropped);
		}
	}

	public void DropItem(IItem item, Vector3 position, Vector3 direction, RemoveFromStackSetting removeFromStack = RemoveFromStackSetting.DontAffectStack, List<IItem> itemsDropped = null)
	{
		if (removeFromStack > RemoveFromStackSetting.DontAffectStack && item.StackCount > 1)
		{
			int num = ((removeFromStack == RemoveFromStackSetting.TryRemoveOne) ? 1 : item.StackCount);
			for (int i = 0; i < num; i++)
			{
				RemoveOneFromStack();
			}
		}
		else
		{
			RemoveItem(item);
			Drop(item, direction);
		}
		void Drop(IItem item2, Vector3 vector, bool randomizeDirAndForce = false)
		{
			float force = 5f;
			if (randomizeDirAndForce)
			{
				vector = Common.OffsetVector2D(vector, UnityEngine.Random.Range(-20, 20));
				vector.y += UnityEngine.Random.Range(0f, 0.5f);
				force = UnityEngine.Random.Range(2.5f, 5f);
			}
			item2.GetDroppedFromInventory(position, vector, force);
			itemsDropped?.Add(item2);
		}
		void RemoveOneFromStack()
		{
			RemoveItem(item, removeFromStackIfPossible: true);
			if (DoomBuilderDefinitions.TryGet(item.DefinitionId, out var dbdef) && dbdef.SpawnAfterWorldStart(position).TryGetComponent<Prop>(out var component) && component.TryGetPickupableHandler(out var pickupableHandler))
			{
				IItem item2 = pickupableHandler;
				if (item2 != null)
				{
					if (item2.IsMagazine)
					{
						item2.AmmoInMag = item.AmmoInMag;
					}
					Drop(item2, direction, randomizeDirAndForce: true);
				}
			}
		}
	}

	public void DropAll(Vector3 dropPosition, bool dropKeyItems = true, bool breakWeapons = false)
	{
		int i;
		for (i = 0; i < _slots.Count; i++)
		{
			if (breakWeapons)
			{
				TryBreakIfWeapon(breakThrowables: false);
			}
			Vector3 direction = GetRandomDropDir();
			DropItemInSlot(i, dropPosition, direction);
		}
		if (dropKeyItems)
		{
			for (int num = Keys.Count - 1; num >= 0; num--)
			{
				Vector3 direction = GetRandomDropDir();
				DropItem(Keys[num], dropPosition, direction);
			}
		}
		for (int num2 = _unstorableItems.Count - 1; num2 >= 0; num2--)
		{
			Vector3 direction = GetRandomDropDir();
			DropItem(_unstorableItems[num2], dropPosition, direction);
		}
		static Vector3 GetRandomDropDir()
		{
			return new Vector3(UnityEngine.Random.Range(-0.5f, 0.5f), 1f, UnityEngine.Random.Range(-0.5f, 0.5f));
		}
		void TryBreakIfWeapon(bool breakThrowables = true)
		{
			IItem itemInSlot = GetItemInSlot(i);
			if (itemInSlot != null && itemInSlot.Type == IItem.ItemType.Weapon && (breakThrowables || !(itemInSlot.WeaponInfo.FirstPersonWeapon != null) || !itemInSlot.WeaponInfo.FirstPersonWeapon.IsThrowable))
			{
				itemInSlot.TryBreak();
			}
		}
	}

	public void DropItemOfValue(Vector3 dropPosition, Vector3 dropDirection)
	{
		if (HasSomethingOfValue(out var item))
		{
			item.GetDroppedFromInventory(dropPosition, dropDirection, 5f);
			RemoveItem(item);
		}
	}

	public bool HasSomethingOfValue(out IItem item)
	{
		if (Keys.Count > 0)
		{
			item = Keys[0];
			return true;
		}
		for (int i = 0; i < _slots.Count; i++)
		{
			IItem item2 = _slots[i].Item;
			if (item2 != null && item2.Type != IItem.ItemType.Weapon)
			{
				item = item2;
				return true;
			}
		}
		if (HasUnstorableItems)
		{
			IItem item3 = _unstorableItems[0];
			item = item3;
			return true;
		}
		item = null;
		return false;
	}

	public IItem GetItemInSlot(int slotNumber)
	{
		if (slotNumber < 0 || slotNumber >= _slots.Count)
		{
			return null;
		}
		return _slots[slotNumber].Item;
	}

	public bool TryGetItemInSlot(int slotNumber, out IItem item)
	{
		if (slotNumber < 0 || slotNumber >= _slots.Count)
		{
			item = null;
			return false;
		}
		item = _slots[slotNumber].Item;
		return item != null;
	}

	public bool TryAddSlotItem(IItem item, out bool addedAllToExistingStack, out TryAddItemFailReason failReason)
	{
		if (item.IsStackable)
		{
			int num = item.StackCount;
			for (int i = 0; i < _slots.Count; i++)
			{
				IItem item2 = _slots[i].Item;
				if (!item2.IsNullOrDestroyed() && item2.DefinitionId == item.DefinitionId && (!item2.IsMagazine || item2.AmmoInMag == item.AmmoInMag))
				{
					if (item2.StackCount + num > item2.MaxStackCount)
					{
						int num2 = item2.MaxStackCount - item2.StackCount;
						num -= num2;
						item2.StackCount += num2;
					}
					else
					{
						item2.StackCount += num;
						num = 0;
					}
				}
			}
			if (num > 0)
			{
				item.StackCount = num;
				addedAllToExistingStack = false;
				if (TryAddToFreeSlot(TryAddItemFailReason.CantCarryAnymoreAmmo))
				{
					failReason = TryAddItemFailReason.Undefined;
					return true;
				}
				failReason = TryAddItemFailReason.CantCarryAnymoreAmmo;
				return false;
			}
			addedAllToExistingStack = true;
			failReason = TryAddItemFailReason.Undefined;
			return true;
		}
		addedAllToExistingStack = false;
		if (TryAddToFreeSlot(TryAddItemFailReason.InventoryIsFull))
		{
			failReason = TryAddItemFailReason.Undefined;
			return true;
		}
		failReason = TryAddItemFailReason.InventoryIsFull;
		return false;
		bool TryAddToFreeSlot(TryAddItemFailReason obj)
		{
			bool flag = false;
			int freeSlot = GetFreeSlot(item.AmmoPouchable, item.Lunchboxable);
			if (freeSlot != -1)
			{
				Slot value = _slots[freeSlot];
				value.Item = item;
				_slots[freeSlot] = value;
				flag = true;
				this.ItemAddedToSlot?.Invoke(freeSlot, item);
				this.ItemAdded?.Invoke(item);
			}
			if (!flag)
			{
				this.TryAddItemFailed?.Invoke(obj);
			}
			return flag;
		}
	}

	public bool TryAddSlotItem(IItem item, int slotNumber)
	{
		if (slotNumber < 0 || slotNumber >= _slots.Count)
		{
			return false;
		}
		if (_slots[slotNumber].Item != null)
		{
			return false;
		}
		Slot value = _slots[slotNumber];
		value.Item = item;
		_slots[slotNumber] = value;
		this.ItemAddedToSlot?.Invoke(slotNumber, item);
		this.ItemAdded?.Invoke(item);
		item.OnPickedUp(this);
		return true;
	}

	public bool TryAddUnstorableItem(IItem item, out TryAddItemFailReason failReason)
	{
		if (_maxUnstorableProps >= 0 && _unstorableItems.Count >= _maxUnstorableProps)
		{
			this.TryAddItemFailed?.Invoke(TryAddItemFailReason.ReachedMaxUnstorables);
			failReason = TryAddItemFailReason.ReachedMaxUnstorables;
			return false;
		}
		_unstorableItems.Add(item);
		this.ItemAdded?.Invoke(item);
		failReason = TryAddItemFailReason.Undefined;
		return true;
	}

	public bool TryGetUnstorableItem(out IItem item)
	{
		if (!HasUnstorableItems)
		{
			item = null;
			return false;
		}
		item = _unstorableItems[0];
		return item != null;
	}

	public void RemoveItem(IItem item, bool removeFromStackIfPossible = false)
	{
		if (item.Type == IItem.ItemType.Key)
		{
			int num = Keys.IndexOf(item);
			if (num >= 0)
			{
				Keys.RemoveAt(num);
				this.ItemRemoved?.Invoke(item);
			}
			return;
		}
		if (ItemIsUnstorableOrBody(item) || item.Type == IItem.ItemType.Note)
		{
			int num2 = _unstorableItems.IndexOf(item);
			if (num2 >= 0)
			{
				_unstorableItems.RemoveAt(num2);
				this.ItemRemoved?.Invoke(item);
			}
			return;
		}
		int i;
		for (i = 0; i < _slots.Count; i++)
		{
			if (_slots[i].Item != item)
			{
				continue;
			}
			if (removeFromStackIfPossible)
			{
				item.StackCount--;
				if (item.StackCount <= 0)
				{
					ClearSlot();
				}
			}
			else
			{
				ClearSlot();
			}
			break;
		}
		void ClearSlot()
		{
			Slot value = _slots[i];
			value.Item = null;
			_slots[i] = value;
			this.ItemRemovedFromSlot?.Invoke(i, item);
			this.ItemRemoved?.Invoke(item);
		}
	}

	public void AddKey(IItem key)
	{
		Keys.Add(key);
		this.ItemAdded?.Invoke(key);
	}

	public bool TryKey(int lockId)
	{
		if (!TryGetKeyForLock(lockId, out var item))
		{
			return false;
		}
		if (!item.TryGetKeyComponent(out var _))
		{
			return false;
		}
		this.KeyUsed?.Invoke(item);
		return true;
	}

	public bool TryGetKeyForLock(int lockId, out IItem item)
	{
		if (lockId == 0)
		{
			item = null;
			return false;
		}
		for (int i = 0; i < Keys.Count; i++)
		{
			IItem item2 = Keys[i];
			if (item2.TryGetKeyComponent(out var keyComponent) && keyComponent.LockId == lockId)
			{
				item = item2;
				return true;
			}
		}
		item = null;
		return false;
	}

	public bool HasKey(int lockId)
	{
		for (int i = 0; i < Keys.Count; i++)
		{
			if (Keys[i].TryGetKeyComponent(out var keyComponent) && keyComponent.LockId == lockId)
			{
				return true;
			}
		}
		return false;
	}

	public bool HasSlotOfType(Slot.TypeID type, out int slotNumber)
	{
		for (int i = 0; i < Slots.Count; i++)
		{
			if (Slots[i].Type == type)
			{
				slotNumber = i;
				return true;
			}
		}
		slotNumber = -1;
		return false;
	}

	public int GetFreeSlot(bool allowAmmoPouchSlot, bool allowLunchboxSlot)
	{
		if (allowAmmoPouchSlot || allowLunchboxSlot)
		{
			for (int i = 0; i < Capacity; i++)
			{
				Slot slot = _slots[i];
				if ((slot.Type == Slot.TypeID.AmmoPouch || slot.Type == Slot.TypeID.Lunchbox) && (_slots[i].Type != Slot.TypeID.AmmoPouch || allowAmmoPouchSlot) && (_slots[i].Type != Slot.TypeID.Lunchbox || allowLunchboxSlot) && _slots[i].Item.IsNullOrDestroyed())
				{
					return i;
				}
			}
		}
		int result = -1;
		for (int j = 0; j < Capacity; j++)
		{
			if (_slots[j].Item.IsNullOrDestroyed() && (_slots[j].Type != Slot.TypeID.AmmoPouch || allowAmmoPouchSlot) && (_slots[j].Type != Slot.TypeID.Lunchbox || allowLunchboxSlot))
			{
				result = j;
				break;
			}
		}
		return result;
	}

	public int GetSlotNumberOfItem(IItem item)
	{
		for (int i = 0; i < _slots.Count; i++)
		{
			if (_slots[i].Item == item)
			{
				return i;
			}
		}
		return -1;
	}

	public bool TryAddItem(IItem item, out bool addedAllToExistingStack, out TryAddItemFailReason failReason)
	{
		addedAllToExistingStack = false;
		switch (item.Type)
		{
		default:
			failReason = TryAddItemFailReason.Undefined;
			return false;
		case IItem.ItemType.Weapon:
		case IItem.ItemType.Consumable:
		case IItem.ItemType.Ammo:
			return TryAddSlotItem(item, out addedAllToExistingStack, out failReason);
		case IItem.ItemType.Unstorable:
		case IItem.ItemType.Note:
		case IItem.ItemType.Body:
			return TryAddUnstorableItem(item, out failReason);
		case IItem.ItemType.Key:
			failReason = TryAddItemFailReason.Undefined;
			AddKey(item);
			return true;
		}
	}

	public bool Contains(IItem item)
	{
		switch (item.Type)
		{
		default:
		{
			for (int j = 0; j < _slots.Count; j++)
			{
				if (_slots[j].Item == item)
				{
					return true;
				}
			}
			return false;
		}
		case IItem.ItemType.Key:
		{
			for (int k = 0; k < Keys.Count; k++)
			{
				if (Keys[k] == item)
				{
					return true;
				}
			}
			break;
		}
		case IItem.ItemType.Unstorable:
		case IItem.ItemType.Note:
		case IItem.ItemType.Body:
		{
			for (int i = 0; i < _unstorableItems.Count; i++)
			{
				if (_unstorableItems[i] == item)
				{
					return true;
				}
			}
			break;
		}
		}
		return false;
	}

	public void DeleteSlotItems()
	{
		for (int i = 0; i < _slots.Count; i++)
		{
			IItem item = _slots[i].Item;
			if (!item.IsNullOrDestroyed())
			{
				RemoveItem(item);
				UnityEngine.Object.Destroy(item.GameObject);
			}
		}
	}

	protected void AddSlot(Slot slot)
	{
		_slots.Add(slot);
		this.SlotAdded?.Invoke(_slots.Count - 1, slot);
	}

	protected void RemoveSlot(int slotIndex)
	{
		Slot arg = _slots[slotIndex];
		_slots.RemoveAt(slotIndex);
		this.SlotRemoved?.Invoke(slotIndex, arg);
	}

	protected void SwapSlot(int indexA, int indexB)
	{
		Swap<Slot>(_slots, indexA, indexB);
		this.SlotsIndicesSwapped?.Invoke(indexA, indexB);
		static void Swap<T>(IList<T> list, int index, int index2)
		{
			T value = list[index];
			T value2 = list[index2];
			list[index2] = value;
			list[index] = value2;
		}
	}

	private void SerializeItemReference(DataSerializer ds, ref IItem item)
	{
		int id = -1;
		if (ds.IsWriting)
		{
			GameObject thing = null;
			if (item is Prop.PickupableHandler pickupableHandler && pickupableHandler.Prop != null)
			{
				thing = pickupableHandler.Prop.gameObject;
			}
			else if (item is EnemyPickupableHandler enemyPickupableHandler && enemyPickupableHandler.Enemy != null)
			{
				thing = enemyPickupableHandler.Enemy.gameObject;
			}
			else if (item is GenericPickupableBodyHandler genericPickupableBodyHandler)
			{
				thing = genericPickupableBodyHandler.gameObject;
			}
			else if (item is NPCPickupableHandler nPCPickupableHandler)
			{
				thing = nPCPickupableHandler.gameObject;
			}
			if (!SaveSystem.TryGetThingId(thing, out id))
			{
				id = -1;
			}
		}
		ds.Serialize(ref id);
		if (!ds.IsReading)
		{
			return;
		}
		item = null;
		if (id != -1 && SaveSystem.TryGetThing(id, out var thing2))
		{
			Enemy component2;
			EnemyPickupableHandler pickupableHandler3;
			GenericPickupableBodyHandler component3;
			NPCPickupableHandler component4;
			if (thing2.TryGetComponent<Prop>(out var component) && component.TryGetPickupableHandler(out var pickupableHandler2))
			{
				item = pickupableHandler2;
			}
			else if (thing2.TryGetComponent<Enemy>(out component2) && component2.TryGetPickupableHandler(out pickupableHandler3))
			{
				item = pickupableHandler3;
			}
			else if (thing2.TryGetComponent<GenericPickupableBodyHandler>(out component3))
			{
				item = component3;
			}
			else if (thing2.TryGetComponent<NPCPickupableHandler>(out component4))
			{
				item = component4;
			}
		}
	}

	private void SerializeKeys(DataSerializer ds)
	{
		int value = (ds.IsWriting ? Keys.Count : 0);
		ds.Serialize(ref value);
		if (ds.IsReading)
		{
			Keys.Clear();
		}
		for (int i = 0; i < value; i++)
		{
			IItem item = (ds.IsWriting ? Keys[i] : null);
			SerializeItemReference(ds, ref item);
			if (ds.IsReading && item != null)
			{
				AddKey(item);
			}
		}
	}

	private void SerializeUnstorableItems(DataSerializer ds)
	{
		int value = (ds.IsWriting ? _unstorableItems.Count : 0);
		ds.Serialize(ref value);
		if (ds.IsReading)
		{
			_unstorableItems.Clear();
		}
		for (int i = 0; i < value; i++)
		{
			IItem item = (ds.IsWriting ? _unstorableItems[i] : null);
			SerializeItemReference(ds, ref item);
			if (ds.IsReading && item != null)
			{
				TryAddUnstorableItem(item, out var _);
			}
		}
	}

	private void SerializeItemSlots(DataSerializer ds)
	{
		int value = (ds.IsWriting ? _slots.Count : 0);
		ds.Serialize(ref value);
		for (int i = 0; i < value; i++)
		{
			IItem item = ((i < _slots.Count) ? _slots[i].Item : null);
			SerializeItemReference(ds, ref item);
			int value2 = ((ds.IsWriting && item != null) ? item.StackCount : 0);
			ds.Serialize(ref value2);
			Slot.TypeID value3 = (ds.IsWriting ? _slots[i].Type : Slot.TypeID.Normal);
			ds.Serialize(ref value3);
			if (ds.IsReading)
			{
				if (i >= _slots.Count)
				{
					AddSlot(new Slot(value3));
				}
				if (item != null)
				{
					item.StackCount = value2;
					TryAddSlotItem(item, i);
				}
			}
		}
	}

	public virtual void SerializeSaveData(DataSerializer ds)
	{
		SerializeItemSlots(ds);
		SerializeKeys(ds);
		SerializeUnstorableItems(ds);
	}

	protected virtual void Awake()
	{
		for (int i = 0; i < _initialSize; i++)
		{
			_slots.Add(default(Slot));
		}
	}

	protected virtual void OnDestroy()
	{
		ClearEventsOfSubscribers();
	}

	protected virtual void ClearEventsOfSubscribers()
	{
		this.ItemAddedToSlot = null;
		this.ItemRemovedFromSlot = null;
	}

	private bool ItemIsUnstorableOrBody(IItem item)
	{
		if (item.Type != IItem.ItemType.Unstorable)
		{
			return item.Type == IItem.ItemType.Body;
		}
		return true;
	}
}
