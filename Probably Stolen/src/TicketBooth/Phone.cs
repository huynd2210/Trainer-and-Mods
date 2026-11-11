using UnityEngine;

namespace TicketBooth
{
    // The Lottery Board's retailer line, built like the game's own supplier contacts
    // (mining, pharma): order on the phone, a courier brings the stock next day at the
    // normal price, then the line is on cooldown.
    //
    // The contact is a vanilla StorePhoneClient in the save. If the mod is removed its
    // dialogue id no longer resolves and the game plays its own "bug" line, and a queued
    // courier id that no longer exists falls back to a vanilla scav, so saves stay loadable.
    internal static class Phone
    {
        internal const long Number = 56883L;
        internal const string DialogFuncId = "TicketBooth";
        internal const string CourierId = "ticketBoothCourier";

        private const string BoardName = "Lottery Board";

        // Orders, by StorePhoneClient.stockType. Large unlocks after 500 credits of stock.
        private static readonly (int tickets, int stacks, int unlockSpent)[] Orders =
        {
            (10, 2, 0),
            (20, 4, 500),
        };

        internal static StorePhoneClient Contact()
        {
            PlayerStore store = PlayerStore.Instance;
            if (store == null || store.PhoneClientDict == null)
                return null;
            store.PhoneClientDict.TryGetValue(Number, out StorePhoneClient client);
            return client;
        }

        internal static void AddContact()
        {
            StorePhoneClient client = new StorePhoneClient
            {
                phoneClientType = StorePhoneClient.PhoneClientType.Supplier,
                phoneState = StorePhoneClient.PhoneState.Regular,
                displayName = BoardName,
                locID = "name_lottery_representative",
                dialogFuncId = DialogFuncId,
                cooldownDuration = TicketBoothMod.PhoneCooldown,
                // In the phone book straight away; the slip has the number too.
                dialedBefore = true,
            };
            // While the courier is at the counter the line doesn't answer, like the others.
            client.alias.Add(CourierId);
            PlayerStore.Instance.PhoneClientDict[Number] = client;
        }

        internal static Dialogue CallDialog(StorePhoneClient client)
        {
            if (client.phoneState == StorePhoneClient.PhoneState.Cooldown)
                return PhoneDialogList.GenericCooldown(client.GetLocalizedDisplayName());

            string name = client.GetLocalizedDisplayName();
            Dialogue greeting = new Dialogue();
            Dialogue menu = new Dialogue();
            greeting.SetText(name, "Lottery Board, retailer line.").SetNextDialogue(menu);
            menu.SetText(name, "What can I send you?");

            for (int i = 0; i < Orders.Length; i++)
            {
                int stockType = i;
                (int tickets, int stacks, int unlockSpent) = Orders[i];
                if (client.amountSpent < unlockSpent)
                    continue;
                Dialogue order = new Dialogue();
                order.SetText("PLAYER", tickets + " lottery tickets and " + stacks + " scratcher stacks.");
                menu.AddChoice(DirectoryMaster.Item("$"), "", "", order, delegate
                {
                    client.stockType = stockType;
                    client.cooldownDuration = TicketBoothMod.PhoneCooldown;
                    client.UseService();
                    PlayerStore.Instance.QueueFuturClient(CourierId, 1);
                    order.NextDialogue()
                        .SetText(name, "The courier brings it tomorrow.")
                        .SetEndAction(delegate { PhoneUIManager.Instance.OnPhoneDialogEnd(); });
                });
            }

            Dialogue quit = new Dialogue();
            quit.SetText("PLAYER", LocHelper.GetLocalizedDialoguePhone("end_call"));
            menu.AddChoice(DirectoryMaster.Item("$"), "", "", quit, delegate
            {
                quit.NextDialogue()
                    .SetText(name, LocHelper.GetLocalizedDialoguePhone("goodbye"))
                    .SetEndAction(delegate { PhoneUIManager.Instance.OnPhoneDialogEnd(); });
            });

            return PhoneUIManager.Instance.isQuickDialCall ? menu : greeting;
        }

        internal static StoreClient CreateCourier()
        {
            StoreClient client = new StoreClient();
            client.identifier = CourierId;
            client.displayName = BoardName + " Courier";
            client.spriteName = SpriteDict.GetCourrierSpriteName();
            client.clientFaction = "FACTION_LOWER_LEVEL";
            client.isBargainAvailable = false;
            client.clientIntent = StoreClient.ClientIntent.SELL;
            client.isRegularSellDisable = true;
            client.mainDialogue
                .SetText(client.displayName, "Delivery from the Lottery Board.")
                .NextDialogue()
                .SetText(client.displayName, "Here are your tickets and scratchers.")
                .SetEndAction(PlaceStock);
            client.onItemSold = delegate(GameItem item, int cost)
            {
                StorePhoneClient contact = Contact();
                if (contact != null)
                    contact.amountSpent += cost;
            };
            client.allDoneDialogue = new Dialogue()
                .SetText(client.displayName, "Sell the tickets before the draw. Good luck.")
                .SetEndAction(delegate { DialogUIManager.Instance.CurrentClientLeave(); });
            client.CompleteClientCreation();
            return client;
        }

        private static void PlaceStock()
        {
            StorePhoneClient contact = Contact();
            int type = contact == null ? 0 : Mathf.Clamp(contact.stockType, 0, Orders.Length - 1);
            (int tickets, int stacks, int _) = Orders[type];
            PlayerStore store = PlayerStore.Instance;
            int unplaced = 0;
            for (int i = 0; i < tickets; i++)
                if (!store.TryAddDirectSellingItemToTable("lottery_ticket"))
                    unplaced++;
            for (int i = 0; i < stacks; i++)
                if (!store.TryAddDirectSellingItemToTable("scratcher_stack"))
                    unplaced++;
            if (unplaced > 0)
                Debug.LogWarning("[TicketBooth] Front counter full: " + unplaced + " of " + (tickets + stacks) + " items not placed.");
        }
    }
}
