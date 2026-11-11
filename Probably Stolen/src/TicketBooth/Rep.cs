namespace TicketBooth
{
    // The Lottery Representative's licence pitch. Same character and choice layout as the
    // appraisal board's membership offer: join (if you can pay), or not now. He comes
    // back a week later until you join.
    internal static class Rep
    {
        internal const string ClientId = "ticketBoothRep";

        internal static StoreClient CreatePitch()
        {
            StoreClient client = new StoreClient();
            client.identifier = ClientId;
            client.nonShootable = true;
            client.displayName = LocHelper.GetLocalizedClientName("name_lottery_representative");
            client.spriteName = SpriteDict.GetClerkSpriteName();
            StoreClientFactionSetup.InitLowerLevelCitizen(client);
            client.clientIntent = StoreClient.ClientIntent.DIALOGUE;
            int fee = TicketBoothMod.LicenseFee;
            int cooldown = TicketBoothMod.PhoneCooldown;

            Dialogue offer = new Dialogue();
            offer.SetText(client.displayName, "It's " + fee + " credits to sign up, and you only pay once. You also get our phone number, so you can order more tickets.");

            Dialogue join = new Dialogue();
            join.SetText("PLAYER", "Okay, I'm in. (" + fee + " credits)")
                .NextDialogue()
                .SetStartAction(delegate { Join(fee); })
                .SetText("CLIENT", "Great, you're in. Our number is on this slip: " + Phone.Number + ".")
                .NextDialogue()
                .SetText("CLIENT", "Call us when you need more tickets. You can order once every " + cooldown + " days.")
                .NextDialogue()
                .SetText("CLIENT", "When someone wins, they bring the ticket to you, and you pay them out of your register.")
                .NextDialogue()
                .SetText("CLIENT", "One more thing. Some people bring fake tickets. Check the stamp, the edition and the numbers before you pay them anything.")
                .SetEndAction(delegate { DialogUIManager.Instance.CurrentClientLeave(); });

            Dialogue later = new Dialogue();
            later.SetText("PLAYER", "Not now.")
                .NextDialogue()
                .SetText("CLIENT", "No problem. I come by every week, so you can sign up next time.")
                .SetEndAction(delegate { DialogUIManager.Instance.CurrentClientLeave(); });

            offer.AddChoice(DirectoryMaster.Item("$"), "", "", join, delegate { },
                () => PlayerStore.Instance.playerCash >= fee);
            offer.AddChoice(DirectoryMaster.Item("$"), "", "", later, delegate { });

            client.mainDialogue
                .SetText(client.displayName, "Hi. I'm with the Lottery Board. We're looking for shops down here to sell lottery tickets.")
                .NextDialogue()
                .SetText(client.displayName, "You sell the tickets and scratchers here. When someone wins, they come to you to get paid.")
                .nextDialogue = offer;
            client.CompleteClientCreation();
            return client;
        }

        private static void Join(int fee)
        {
            PlayerStore store = PlayerStore.Instance;
            store.ModCash(-fee);
            State.Licensed = true;
            State.LicenseDay = State.Today;
            Phone.AddContact();
            store.AddDirectSellingItemToTable(PreBuiltItemHelper.CreatePaperWithText(
                "Lottery Board phone: " + Phone.Number + "\n\nCall to order tickets. They arrive the next day. You can order once every "
                + TicketBoothMod.PhoneCooldown + " days.\n\nWhen someone wins, you pay them out of your register. Check every winning ticket before you pay. Some are fake.",
                "Lottery Board"), isOwend: true);
            store.AddDirectSellingItemToTable("lottery_info", isOwend: true);
        }
    }
}
