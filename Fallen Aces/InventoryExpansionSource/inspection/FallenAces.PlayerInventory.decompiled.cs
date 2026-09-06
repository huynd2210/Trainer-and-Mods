using System;
using System.Collections.Generic;
using FallenAces.Gadgets;
using FallenAces.HUD;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FallenAces;

public class PlayerInventory : Inventory
{
	[SerializeField]
	private PlayerEventManager _playerEventManager;

	private int _currentSlotHighlighted;

	private int _lastHighlightedSlotBeforeHolster;

	private int _lootValue;

	private bool _currentlyHolstering;

	private IItem _itemHeldForInWorldReload;

	private bool _allowSwapping;

	private bool AllowInput
	{
		get
		{
			if (!GlobalEventManager.TryGet(out var instance))
			{
				return true;
			}
			if (!instance.RequestValue<bool>(3))
			{
				return !instance.RequestValue<bool>(4);
			}
			return false;
		}
	}

	private Vector3 ItemDropPostion => Player.PlayerHeadPosition;

	private Vector3 ItemDropDirection => Player.PlayerHeadDirection;

	public IItem ItemHeldForInWorldReload => _itemHeldForInWorldReload;

	public int LootValue
	{
		get
		{
			return _lootValue;
		}
		set
		{
			_lootValue = value;
			Arguments reusableArguments = _playerEventManager.ReusableArguments;
			reusableArguments.AddArgumentAsInt(_lootValue);
			_playerEventManager.TriggerEvent(35, reusableArguments);
		}
	}

	public bool WeaponItemIsHighlighted
	{
		get
		{
			IItem itemInSlot = GetItemInSlot(CurrentSlotHighlighted);
			if (itemInSlot == null)
			{
				return false;
			}
			if (itemInSlot.Type == IItem.ItemType.Weapon)
			{
				return true;
			}
			return false;
		}
	}

	public int CurrentSlotHighlighted => _currentSlotHighlighted;

	public event Action<int> SlotHighlighted;

	private void Start()
	{
		HighlightSlot(0);
	}

	private void OnEnable()
	{
		HandleGlobalEventManagerSubscriptions(subscribe: true);
		HandleInputManagerSubscriptions(subscribe: true);
		HandleSelfSubscriptions(subscribe: true);
		HandlePlayerSubscriptions(subscribe: true);
	}

	private void OnDisable()
	{
		HandleGlobalEventManagerSubscriptions(subscribe: false);
		HandleInputManagerSubscriptions(subscribe: false);
		HandleSelfSubscriptions(subscribe: false);
		HandlePlayerSubscriptions(subscribe: false);
	}

	private void Warn(string message)
	{
		Debug.LogWarningFormat(this, "{0}: {1}", "PlayerInventory", message);
	}

	private int OnRequestLootValue()
	{
		return _lootValue;
	}

	private void OnLootSpent(Arguments arguments)
	{
		_lootValue -= arguments.GetArgumentAsInt(0);
		Arguments reusableArguments = _playerEventManager.ReusableArguments;
		reusableArguments.AddArgumentAsInt(_lootValue);
		_playerEventManager.TriggerEvent(35, reusableArguments);
	}

	private void OnKeysRequested(Arguments arguments)
	{
		List<IItem> obj = (List<IItem>)arguments.GetArgument(0);
		obj.Clear();
		obj.AddRange(base.Keys);
	}

	private void OnKeyUse(IItem key)
	{
		ShowMessageOnHud("Used " + key.Name);
	}

	private void HandleGlobalEventManagerSubscriptions(bool subscribe)
	{
		if (GlobalEventManager.TryGet(out var instance))
		{
			if (subscribe)
			{
				instance.StartListening(16, OnLootSpent);
				instance.StartListening(29, OnPlayerReloadedWeapon);
				instance.StartListening(30, OnPlayerDiscardedAmmoInWeapon);
				instance.StartListening(25, OnKeysRequested);
				instance.StartListening(33, OnAllowOrDisallowSwappingItemsEvent);
				instance.ListenForValueRequest(0, OnRequestLootValue);
			}
			else
			{
				instance.StopListening(16, OnLootSpent);
				instance.StopListening(29, OnPlayerReloadedWeapon);
				instance.StopListening(30, OnPlayerDiscardedAmmoInWeapon);
				instance.StopListening(25, OnKeysRequested);
				instance.StopListening(33, OnAllowOrDisallowSwappingItemsEvent);
				instance.StopListeningForValueRequest<int>(0);
			}
		}
	}

	private void OnPlayerReloadedWeapon(Arguments arguments)
	{
		if (arguments == null)
		{
			return;
		}
		object argument = arguments.GetArgument(0);
		FirstPersonWeapon.AmmoItem[] ammoItems = argument as FirstPersonWeapon.AmmoItem[];
		if (ammoItems == null || !(arguments.GetArgument(1) is IItem item))
		{
			return;
		}
		argument = arguments.GetArgument(2);
		if (!(argument is FirstPersonWeapon.Reload))
		{
			return;
		}
		FirstPersonWeapon.Reload reload = (FirstPersonWeapon.Reload)argument;
		FirstPersonWeapon firstPersonWeapon = item.WeaponInfo.FirstPersonWeapon;
		if (firstPersonWeapon == null || !HasAmmo(out var slotIndexOfAmmo, out var isAmmoLessReload))
		{
			return;
		}
		if (isAmmoLessReload)
		{
			item.WeaponInfo.AmmoLeft += firstPersonWeapon.Ammo;
			return;
		}
		IItem ammoItem = ((slotIndexOfAmmo >= 0) ? GetItemInSlot(slotIndexOfAmmo) : _itemHeldForInWorldReload);
		if (ammoItem.IsMagazine)
		{
			item.WeaponInfo.AmmoLeft += ammoItem.AmmoInMag;
			ammoItem.StackCount--;
			PropEventManager component;
			if (ammoItem.StackCount == 0)
			{
				RemoveAmmoItem();
				UnityEngine.Object.Destroy(ammoItem.GameObject);
			}
			else if (ammoItem.GameObject.TryGetComponent<PropEventManager>(out component))
			{
				component.TriggerEvent(11);
			}
			return;
		}
		int num = (reload.LoadInidividualRounds ? 1 : Mathf.Clamp(ammoItem.StackCount, 0, firstPersonWeapon.Ammo - item.WeaponInfo.AmmoLeft));
		item.WeaponInfo.AmmoLeft += num;
		ammoItem.StackCount -= num;
		if (ammoItem.GameObject.TryGetComponent<PropEventManager>(out var component2))
		{
			component2.TriggerEvent(11);
		}
		if (ammoItem.StackCount == 0)
		{
			RemoveAmmoItem();
			UnityEngine.Object.Destroy(ammoItem.GameObject);
		}
		bool HasAmmo(out int reference2, out bool reference)
		{
			if (reload.AmmoDefinitionId < 0)
			{
				reference = true;
				reference2 = -1;
				return true;
			}
			reference = false;
			reference2 = 0;
			if (_itemHeldForInWorldReload != null)
			{
				FirstPersonWeapon.AmmoItem[] array = ammoItems;
				for (int i = 0; i < array.Length; i++)
				{
					FirstPersonWeapon.AmmoItem ammoItem2 = array[i];
					if (reload.AmmoDefinitionId == ammoItem2.DefinitionId && ammoItem2.DefinitionId == ammoItem2.DefinitionId)
					{
						reference2 = -1;
						return true;
					}
				}
			}
			for (int j = 0; j < ammoItems.Length; j++)
			{
				if (reload.AmmoDefinitionId == ammoItems[j].DefinitionId && HasSlotItemOfId(ammoItems[j].DefinitionId, out reference2))
				{
					return true;
				}
			}
			reference2 = 0;
			return false;
		}
		void RemoveAmmoItem()
		{
			if (ammoItem == _itemHeldForInWorldReload)
			{
				_itemHeldForInWorldReload = null;
			}
			else
			{
				RemoveItem(ammoItem);
			}
		}
	}

	private void OnPlayerDiscardedAmmoInWeapon(Arguments arguments)
	{
		if (arguments == null || !(arguments.GetArgument(0) is FirstPersonWeapon.AmmoItem[]) || !(arguments.GetArgument(1) is IItem item))
		{
			return;
		}
		bool argumentAsBool = arguments.GetArgumentAsBool(2);
		int num = (argumentAsBool ? (item.WeaponInfo.AmmoLeft - 1) : item.WeaponInfo.AmmoLeft);
		item.WeaponInfo.AmmoLeft = (argumentAsBool ? 1 : 0);
		if (num <= 0)
		{
			return;
		}
		FirstPersonWeapon firstPersonWeapon = item.WeaponInfo.FirstPersonWeapon;
		if (!(firstPersonWeapon == null) && TryGetAmmoItem(out var magDef, out var ammoDef))
		{
			Vector3 positionToDrop = Player.PlayerHeadPosition + Vector3.down * 1f + -Player.PlayerHeadDirection * 0.1f;
			if (magDef != null)
			{
				int num2 = Mathf.Min(num, magDef.AmmoPerMagazine);
				Common.DropAmmo(magDef, positionToDrop, num2, spawnedFromBrokenWeapon: true, 2f);
				num = Mathf.Max(0, num - num2);
			}
			if (num > 0 && ammoDef != null)
			{
				Common.DropAmmo(ammoDef, positionToDrop, num, spawnedFromBrokenWeapon: true, 2f);
			}
		}
		bool TryGetAmmoItem(out DoomBuilderPropDefinition reference, out DoomBuilderPropDefinition reference2)
		{
			reference = null;
			reference2 = null;
			bool flag = false;
			bool flag2 = false;
			for (int i = 0; i < firstPersonWeapon.AmmoItems.Length; i++)
			{
				FirstPersonWeapon.AmmoItem ammoItem = firstPersonWeapon.AmmoItems[i];
				if (ammoItem.UseOnPropBreak)
				{
					DoomBuilderDefinition dbdef2;
					if (!flag && DoomBuilderDefinitions.TryGet(ammoItem.DefinitionId, out var dbdef) && dbdef is DoomBuilderPropDefinition { AmmoPerMagazine: >0 } doomBuilderPropDefinition)
					{
						flag = true;
						reference = doomBuilderPropDefinition;
					}
					else if (!flag2 && DoomBuilderDefinitions.TryGet(ammoItem.DefinitionId, out dbdef2) && dbdef2 is DoomBuilderPropDefinition { AmmoPerMagazine: <=0 } doomBuilderPropDefinition2)
					{
						flag2 = true;
						reference2 = doomBuilderPropDefinition2;
					}
					if (flag2 && flag)
					{
						break;
					}
				}
			}
			return flag || flag2;
		}
	}

	private void HandleInputManagerSubscriptions(bool subscribe)
	{
		if (!TryGetInputManager(out var inputManager))
		{
			return;
		}
		InputMaster.PlayerActions playerInput = inputManager.PlayerInput;
		if (subscribe)
		{
			playerInput.Holster.performed += delegate
			{
				OnSlotKeyPressed(-1);
			};
			playerInput.InventorySlot1.performed += delegate
			{
				OnSlotKeyPressed(0);
			};
			playerInput.InventorySlot2.performed += delegate
			{
				OnSlotKeyPressed(1);
			};
			playerInput.InventorySlot3.performed += delegate
			{
				OnSlotKeyPressed(2);
			};
			playerInput.InventorySlot4.performed += delegate
			{
				OnSlotKeyPressed(3);
			};
			playerInput.InventorySlot5.performed += delegate
			{
				OnSlotKeyPressed(4);
			};
			playerInput.Scroll.performed += delegate(InputAction.CallbackContext ctx)
			{
				OnMouseScroll(ctx.ReadValue<float>());
			};
		}
		else
		{
			playerInput.Holster.performed -= delegate
			{
				OnSlotKeyPressed(-1);
			};
			playerInput.InventorySlot1.performed -= delegate
			{
				OnSlotKeyPressed(0);
			};
			playerInput.InventorySlot2.performed -= delegate
			{
				OnSlotKeyPressed(1);
			};
			playerInput.InventorySlot3.performed -= delegate
			{
				OnSlotKeyPressed(2);
			};
			playerInput.InventorySlot4.performed -= delegate
			{
				OnSlotKeyPressed(3);
			};
			playerInput.InventorySlot5.performed -= delegate
			{
				OnSlotKeyPressed(4);
			};
			playerInput.Scroll.performed -= delegate(InputAction.CallbackContext ctx)
			{
				OnMouseScroll(ctx.ReadValue<float>());
			};
		}
	}

	private void HandleSelfSubscriptions(bool subscribe)
	{
		if (subscribe)
		{
			base.ItemAddedToSlot += OnItemAddedToSlot;
			base.ItemRemovedFromSlot += OnItemRemovedFromSlot;
			base.ItemAdded += OnItemAdded;
			base.KeyUsed += OnKeyUse;
			base.TryAddItemFailed += OnTryAddItemFailed;
		}
		else
		{
			base.ItemAddedToSlot -= OnItemAddedToSlot;
			base.ItemRemovedFromSlot -= OnItemRemovedFromSlot;
			base.ItemAdded -= OnItemAdded;
			base.KeyUsed -= OnKeyUse;
			base.TryAddItemFailed -= OnTryAddItemFailed;
		}
	}

	private void HandlePlayerSubscriptions(bool subscribe)
	{
		if (!_playerEventManager.IsNullOrDestroyed())
		{
			if (subscribe)
			{
				_playerEventManager.StartListening(49, OnGadgetAdded);
				_playerEventManager.StartListening(50, OnGadgetRemoved);
			}
			else
			{
				_playerEventManager.StopListening(49, OnGadgetAdded);
				_playerEventManager.StopListening(50, OnGadgetRemoved);
			}
		}
	}

	private void OnGadgetAdded(Arguments arguments)
	{
		switch ((arguments.GetArgument(0) as GadgetInstance).Definition.ID)
		{
		case GadgetId.AmmoPouch:
			AddExtraSlot(Slot.TypeID.AmmoPouch);
			break;
		case GadgetId.Lunchbox:
			AddExtraSlot(Slot.TypeID.Lunchbox);
			break;
		}
	}

	private void OnGadgetRemoved(Arguments arguments)
	{
		GadgetInstance gadgetInstance = arguments.GetArgument(0) as GadgetInstance;
		if (gadgetInstance.Definition.ID == GadgetId.AmmoPouch)
		{
			RemoveExtraSlot(Slot.TypeID.AmmoPouch);
		}
		else if (gadgetInstance.Definition.ID == GadgetId.Lunchbox)
		{
			RemoveExtraSlot(Slot.TypeID.Lunchbox);
		}
	}

	private void OnItemAdded(IItem item)
	{
		if (item.Type == IItem.ItemType.Body && TryGetComponent<Voice>(out var component))
		{
			component.Speak("Pickup Body", VoiceLines.Priority.Unimportant, overrideEqualPriority: true);
		}
	}

	private void AddExtraSlot(Slot.TypeID slotType)
	{
		if (!HasSlotOfType(slotType, out var _))
		{
			AddSlot(new Slot(slotType));
			if (slotType == Slot.TypeID.Lunchbox && HasSlotOfType(Slot.TypeID.AmmoPouch, out var slotNumber2) && HasSlotOfType(Slot.TypeID.Lunchbox, out var slotNumber3))
			{
				SwapSlot(slotNumber2, slotNumber3);
			}
		}
	}

	private void RemoveExtraSlot(Slot.TypeID slotType)
	{
		if (!HasSlotOfType(slotType, out var slotNumber))
		{
			return;
		}
		List<IItem> list = new List<IItem>();
		DropItemInSlot(slotNumber, ItemDropPostion, ItemDropDirection, list);
		RemoveSlot(slotNumber);
		foreach (IItem item in list)
		{
			if (item.GameObject.TryGetComponent<Prop>(out var component) && component.TryGetPickupableHandler(out var pickupableHandler))
			{
				pickupableHandler.TryPickup(this, silentPickup: true);
			}
		}
		if (CurrentSlotHighlighted == slotNumber)
		{
			HighlightSlot(0);
		}
	}

	private void OnSlotKeyPressed(int slot)
	{
		if (AllowInput && (slot != -1 || (!PlayerHud.Instance.NoteIsOnScreen && !PlayerHud.Instance.JournalIsOnScreen)))
		{
			if (slot == -1 && _currentlyHolstering)
			{
				slot = _lastHighlightedSlotBeforeHolster;
			}
			if (slot < base.Slots.Count)
			{
				HighlightSlot(slot);
			}
		}
	}

	private void OnMouseScroll(float direction)
	{
		if (AllowInput)
		{
			if (direction > 0f)
			{
				Scroll(1);
			}
			else if (direction < 0f)
			{
				Scroll(-1);
			}
		}
	}

	private void OnItemAddedToSlot(int slotNumber, IItem item)
	{
		if (!(_playerEventManager == null))
		{
			Arguments reusableArguments = _playerEventManager.ReusableArguments;
			reusableArguments.AddArgumentAsInt(slotNumber);
			reusableArguments.AddArgumentAsObject(item);
			_playerEventManager.TriggerEvent(3, reusableArguments);
			if (GetItemInSlot(CurrentSlotHighlighted) == null)
			{
				HighlightSlot(slotNumber);
			}
			if (base.AllNormalSlotsHaveItems)
			{
				_playerEventManager.TriggerEvent(42);
			}
		}
	}

	private void OnItemRemovedFromSlot(int slotNumber, IItem item)
	{
		if (!(_playerEventManager == null))
		{
			Arguments reusableArguments = _playerEventManager.ReusableArguments;
			reusableArguments.AddArgumentAsInt(slotNumber);
			reusableArguments.AddArgumentAsObject(item);
			_playerEventManager.TriggerEvent(4, reusableArguments);
		}
	}

	private void OnTryAddItemFailed(TryAddItemFailReason failReason)
	{
		if (failReason == TryAddItemFailReason.ReachedMaxUnstorables)
		{
			ShowMessageOnHud("Drop what you're holding first!", isInvalidAction: true);
		}
	}

	private bool TryGetInputManager(out InputManager inputManager)
	{
		inputManager = InputManager.Instance;
		if (inputManager == null)
		{
			return false;
		}
		return true;
	}

	private void OnAllowOrDisallowSwappingItemsEvent(Arguments arguments)
	{
		_allowSwapping = arguments.GetArgumentAsBool(0);
	}

	protected override void Awake()
	{
		base.Awake();
		if (_playerEventManager == null)
		{
			Warn("Player Event Manager not assigned!");
		}
	}

	protected override void ClearEventsOfSubscribers()
	{
		this.SlotHighlighted = null;
	}

	public override void SerializeSaveData(DataSerializer ds)
	{
		base.SerializeSaveData(ds);
		ds.Serialize(ref _lootValue);
		ds.Serialize(ref _currentSlotHighlighted);
		if (ds.IsReading)
		{
			Arguments reusableArguments = _playerEventManager.ReusableArguments;
			reusableArguments.AddArgumentAsInt(_lootValue);
			_playerEventManager.TriggerEvent(35, reusableArguments);
			HighlightSlot(_currentSlotHighlighted);
		}
	}

	public void ClearItemHeldForInWorldReload()
	{
		_itemHeldForInWorldReload = null;
	}

	public bool HasTempHeldAmmo(int definitionId, out int ammoCount)
	{
		ammoCount = 0;
		if (_itemHeldForInWorldReload == null || _itemHeldForInWorldReload.DefinitionId != definitionId)
		{
			return false;
		}
		ammoCount = (_itemHeldForInWorldReload.IsMagazine ? _itemHeldForInWorldReload.AmmoInMag : _itemHeldForInWorldReload.StackCount);
		return true;
	}

	public bool TryReloadEquippedWeaponWithInWorldItem(IItem itemToReloadWith, out bool addedToExistingStack, out bool tryToSwapItemsOnFail)
	{
		addedToExistingStack = false;
		tryToSwapItemsOnFail = true;
		if (itemToReloadWith.Type != IItem.ItemType.Ammo)
		{
			return false;
		}
		if (!GlobalEventManager.TryGet(out var instance) || !instance.TryGetHandsEventManager(out var handEventManager))
		{
			return false;
		}
		IItem item = instance.RequestValue<IItem>(6);
		if (item == null)
		{
			return false;
		}
		FirstPersonWeapon firstPersonWeapon = item.WeaponInfo.FirstPersonWeapon;
		if (firstPersonWeapon == null)
		{
			return false;
		}
		if (!firstPersonWeapon.TryGetReload(itemToReloadWith.DefinitionId, out var _))
		{
			return false;
		}
		tryToSwapItemsOnFail = false;
		if (!itemToReloadWith.IsMagazine && _itemHeldForInWorldReload != null && _itemHeldForInWorldReload.IsStackable && itemToReloadWith.DefinitionId == _itemHeldForInWorldReload.DefinitionId)
		{
			if (_itemHeldForInWorldReload.StackCount >= firstPersonWeapon.Ammo - item.WeaponInfo.AmmoLeft)
			{
				ShowMessageOnHud("Can't do that right now", isInvalidAction: true);
				return false;
			}
			_itemHeldForInWorldReload.StackCount++;
			addedToExistingStack = true;
			Arguments reusableArguments = handEventManager.ReusableArguments;
			reusableArguments.AddArgumentAsObject(_itemHeldForInWorldReload);
			handEventManager.TriggerEvent(94, reusableArguments);
			return true;
		}
		Arguments reusableArguments2 = handEventManager.ReusableArguments;
		bool[] array = new bool[1];
		reusableArguments2.AddArgumentAsObject(itemToReloadWith);
		reusableArguments2.AddArgumentAsObject(array);
		handEventManager.TriggerEvent(92, reusableArguments2);
		if (array[0])
		{
			_itemHeldForInWorldReload = itemToReloadWith;
		}
		else if (item.WeaponInfo.AmmoLeft >= firstPersonWeapon.Ammo)
		{
			ShowMessageOnHud("Ammo is full!", isInvalidAction: true);
		}
		else
		{
			ShowMessageOnHud("Can't do that right now", isInvalidAction: true);
		}
		return array[0];
	}

	public bool TrySwapWithCurrentlyHighlightedItem(IItem itemToSwapWith, TryAddItemFailReason addFailReason, out TryAddItemFailReason swapFailReason)
	{
		swapFailReason = TryAddItemFailReason.Undefined;
		if (!_allowSwapping)
		{
			ShowMessageOnHud("Can't do that right now", isInvalidAction: true);
			return false;
		}
		if (itemToSwapWith.Type == IItem.ItemType.Unstorable || itemToSwapWith.Type == IItem.ItemType.Body)
		{
			return false;
		}
		int num = ((CurrentSlotHighlighted != -1) ? CurrentSlotHighlighted : 0);
		IItem itemInSlot = GetItemInSlot(num);
		if (itemInSlot == null)
		{
			return false;
		}
		if (addFailReason == TryAddItemFailReason.CantCarryAnymoreAmmo && itemToSwapWith.DefinitionId == itemInSlot.DefinitionId)
		{
			ShowMessageOnHud("Can't carry anymore!", isInvalidAction: true);
			swapFailReason = TryAddItemFailReason.CantCarryAnymoreAmmo;
			return false;
		}
		if (base.Slots[num].Type == Slot.TypeID.AmmoPouch && !itemToSwapWith.AmmoPouchable)
		{
			ShowMessageOnHud("No room in inventory!", isInvalidAction: true);
			swapFailReason = TryAddItemFailReason.InventoryIsFull;
			return false;
		}
		if (base.Slots[num].Type == Slot.TypeID.Lunchbox && !itemToSwapWith.Lunchboxable)
		{
			ShowMessageOnHud("No room in inventory!", isInvalidAction: true);
			swapFailReason = TryAddItemFailReason.InventoryIsFull;
			return false;
		}
		if (CurrentSlotHighlighted == -1)
		{
			HighlightSlot(0);
		}
		DropItem(itemInSlot, Player.PlayerHeadPosition, Player.PlayerHeadDirection, RemoveFromStackSetting.TryRemoveAll);
		bool addedAllToExistingStack;
		TryAddItemFailReason failReason;
		return TryAddItem(itemToSwapWith, out addedAllToExistingStack, out failReason);
	}

	public void PickupLoot(int lootValue)
	{
		_lootValue += lootValue;
		Arguments reusableArguments = _playerEventManager.ReusableArguments;
		reusableArguments.AddArgumentAsInt(_lootValue);
		_playerEventManager.TriggerEvent(35, reusableArguments);
		TriggerGlobalEvent();
		if (lootValue > 0)
		{
			ShowHudNotification($"Picked up Loot! + ${lootValue}", NotificationHandler.MessageType.GainedLoot);
		}
		else
		{
			ShowHudNotification($"Picked cursed Loot! - ${lootValue}", NotificationHandler.MessageType.NegativeEffect);
		}
		void ShowHudNotification(string message, NotificationHandler.MessageType messageType)
		{
			Arguments reusableArguments2 = _playerEventManager.ReusableArguments;
			reusableArguments2.AddArgumentAsObject(message);
			reusableArguments2.AddArgumentAsObject(messageType);
			_playerEventManager.TriggerEvent(7, reusableArguments2);
		}
		void TriggerGlobalEvent()
		{
			if (GlobalEventManager.TryGet(out var instance))
			{
				Arguments reusableArguments2 = instance.ReusableArguments;
				reusableArguments2.AddArgumentAsInt(lootValue);
				instance.TriggerEvent(17, reusableArguments2);
			}
		}
	}

	public void HighlightSlot(int slotNumber)
	{
		bool flag = slotNumber == -1;
		if (flag)
		{
			_lastHighlightedSlotBeforeHolster = CurrentSlotHighlighted;
		}
		_currentSlotHighlighted = slotNumber;
		_currentlyHolstering = flag;
		this.SlotHighlighted?.Invoke(slotNumber);
		if (_currentlyHolstering)
		{
			_playerEventManager.TriggerEvent(43);
		}
	}

	public void Scroll(int direction)
	{
		int currentSlotHighlighted = CurrentSlotHighlighted;
		int num = currentSlotHighlighted;
		int num2 = base.Slots.Count - 1;
		switch (direction)
		{
		case -1:
			if (currentSlotHighlighted < 2)
			{
				num++;
			}
			else if (currentSlotHighlighted >= 3 && currentSlotHighlighted < num2)
			{
				num++;
			}
			else if (currentSlotHighlighted == num2 && num2 > 2)
			{
				num = 0;
			}
			break;
		case 1:
			switch (currentSlotHighlighted)
			{
			case -1:
				num = ((num2 > 2) ? num2 : 0);
				break;
			case 1:
			case 2:
				num--;
				break;
			default:
				if (currentSlotHighlighted == 0 && num2 > 2)
				{
					num = num2;
				}
				else if (currentSlotHighlighted == 4)
				{
					num--;
				}
				break;
			}
			break;
		}
		if (num != currentSlotHighlighted)
		{
			HighlightSlot(num);
		}
	}

	public bool CheckTwoHandedWeaponInSlot(int slotNumber)
	{
		IItem itemInSlot = GetItemInSlot(slotNumber);
		if (itemInSlot == null || !itemInSlot.TwoHanded)
		{
			return false;
		}
		return true;
	}

	public void ShowMessageOnHud(string message, bool isInvalidAction = false)
	{
		if (!(_playerEventManager == null))
		{
			Arguments reusableArguments = _playerEventManager.ReusableArguments;
			reusableArguments.AddArgumentAsObject(message);
			reusableArguments.AddArgumentAsObject(isInvalidAction ? NotificationHandler.MessageType.InvalidAction : NotificationHandler.MessageType.Standard);
			_playerEventManager.TriggerEvent(7, reusableArguments);
		}
	}
}
