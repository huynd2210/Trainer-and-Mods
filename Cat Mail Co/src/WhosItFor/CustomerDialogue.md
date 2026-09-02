# Cat Mail Co. - Customer Dialogue Reference

Extracted from `CatMailCo_Data/resources.assets` (I2 Localization table).
Each line carries its own `DialogueType`, `DialogueCategory`, `DialogueAge` and
`DialogueMood`, which is how the game decides when to use it.

`$NAME$` and `$SURNAME$` are substituted at runtime with the customer's name,
which is also printed on the parcel label.

---

## Summary

| Set | Lines | Notes |
|---|---|---|
| `ClientDialogues2/` | 182 | **Live system.** Fully typed and categorised. |
| `ClientDialogues/` | 218 | Legacy/unused. Untyped, contains dev placeholders. |
| `BoatDialogues/` | 135 | Boat captain, not customers. |
| `OldPostman_Dialogue/` | 12 | Scripted old postman scene. |

### Customer request lines by type

| DialogueType | Lines | Role in a request |
|---|---|---|
| `PersonName` | 11 | NAME CLUE - kept by the mod |
| `Size` | 12 | DESCRIPTION CLUE - removed by the mod |
| `Weight` | 4 | DESCRIPTION CLUE - removed by the mod |
| `VisualElement` | 16 | DESCRIPTION CLUE - removed by the mod |
| `StorageConstraint` | 53 | DESCRIPTION CLUE - removed by the mod |
| `BehaviorConstraint` | 15 | DESCRIPTION CLUE - removed by the mod |
| `Link` | 10 | CONNECTOR - removed by the mod |
| `Hello` | 22 | structural (greeting/farewell) - kept |
| `HelloDeposit` | 10 | structural (greeting/farewell) - kept |
| `Goodbye` | 10 | structural (greeting/farewell) - kept |
| `GoodbyeRefused` | 5 | structural (greeting/farewell) - kept |
| `ByeDeposit` | 6 | structural (greeting/farewell) - kept |
| `RequestWrongParcel` | 8 | structural (greeting/farewell) - kept |

---

## The live customer dialogue (`ClientDialogues2/`)

### PersonName (11 lines)

*NAME CLUE - kept by the mod*

| Term | Category | Mood | English |
|---|---|---|---|
| `PersonName_0` | MatchName | Neutral | My name is $NAME$ $SURNAME$. |
| `PersonName_1` | MatchSurname | Grumpy | Look for $SURNAME$. |
| `PersonName_2` | MatchName | Excited | The parcel is for my brother $NAME$! |
| `PersonName_3` | MatchName | Neutral | I'm $NAME$ $SURNAME$. |
| `PersonName_4` | MatchSurname | Neutral | The parcel is for my mom, her last name is $SURNAME$. |
| `PersonName_5` | MatchName | Neutral | It's for my wife, $NAME$ $SURNAME$. |
| `PersonName_6` | MatchSurname | Shy | <size=60%>My name is $NAME$ $SURNAME$… But the parcel is for my brother.</size> |
| `PersonName_7` | MatchName | Anxious | Uh… I… I'm $NAME$… $SURNAME$. |
| `PersonName_8` | MatchName | Grumpy | Don't you recognize me? It's me, $NAME$! |
| `PersonName_9` | MatchName | Anxious | Huh… My… My name is $NAME$… $NAME$ $SURNAME$. |
| `PersonName_10` | MatchSurname | Excited | My mom sent me to pick up her parcel, I'm $NAME$ $SURNAME$! |

### Size (12 lines)

*DESCRIPTION CLUE - removed by the mod*

| Term | Category | Mood | English |
|---|---|---|---|
| `Size_0` | Flat | Neutral | It's a small box. |
| `Size_1` | FlatLetter | Neutral | It's just a letter. |
| `Size_2` | Giant | Neutral | My package is supposed to be huge. |
| `Size_3` | GiantPlus | Neutral | You can't miss it, it's an enormous box. |
| `Size_4` | LongFlat | Neutral | It's a mid-sized package… with a cord all around. |
| `Size_5` | MediumSize | Neutral | It's a box with handles, easier to carry. |
| `Size_6` | OddCube | Neutral | It's a big box… I'm building a cardboard castle. |
| `Size_7` | Small | Neutral | Just a small box, nothing special. |
| `Size_8` | Standard | Neutral | My parcel is cube shaped. |
| `Size_9` | StandardLong | Neutral | My package has a rope around it… That's the only thing I know. |
| `Size_10` | Tall | Neutral | I was told that it is almost my height. |
| `Size_11` | VeryLong | Neutral | I hope this parcel will fit on my bike. |

### Weight (4 lines)

*DESCRIPTION CLUE - removed by the mod*

| Term | Category | Mood | English |
|---|---|---|---|
| `Weight_0` | Light | Neutral | My parcel may not be heavy at all. |
| `Weight_1` | Medium | Neutral | It will not be a challenge for you to carry it. |
| `Weight_2` | Heavy | Neutral | I warn you, it's a heavy package. |
| `Weight_3` | VeryHeavy | Neutral | Do you have someone to help you carry it? No? Well, good luck. |

### VisualElement (16 lines)

*DESCRIPTION CLUE - removed by the mod*

| Term | Category | Mood | English |
|---|---|---|---|
| `VisualElement_0` | StickerFrog | Neutral | It has this very cute sticker with a frog. |
| `VisualElement_1` | RibbonBlue | Neutral | It is supposed to have a blue ribbon. |
| `VisualElement_2` | StickerCrocodileBiteMark | Neutral | My aunt told me her pet crocodile tried to eat the parcel. |
| `VisualElement_3` | RibbonYellow | Neutral | I believe the parcel is wrapped with a yellow ribbon. |
| `VisualElement_4` | StickerCatClawMark | Neutral | My gran has very long claws, they might have scratched the parcel. |
| `VisualElement_5` | StickerDuck | Neutral | It has a special sticker with a duck. |
| `VisualElement_6` | StickerLemon | Neutral | I believe there is a lemon sticker on it. |
| `VisualElement_7` | StickerCrocodileClawMark | Neutral | There are a lot of crocodiles where my parcel comes from. |
| `VisualElement_8` | StickerEagleClawMark | Neutral | My grandpa told me the package was attacked by an eagle. |
| `VisualElement_9` | StickerSlothClawMark | Neutral | I was told a sloth tried to steal my package. |
| `VisualElement_10` | RibbonRed | Neutral | My package has a red ribbon. |
| `VisualElement_11` | RibbonAny | Neutral | My cousin loves to put ribbons on my packages. |
| `VisualElement_12` | StickerLavender | Neutral | It has a sprig of lavender sticker. |
| `VisualElement_13` | RibbonAny | Neutral | It is supposed to have a ribbon on it. |
| `VisualElement_14` | RibbonAny | Neutral | I asked my sister to put a ribbon on it. |
| `VisualElement_15` | StickerClover | Neutral | It has this very cute sticker with a clover. |

### StorageConstraint (53 lines)

*DESCRIPTION CLUE - removed by the mod*

| Term | Category | Mood | English |
|---|---|---|---|
| `StorageConstraint_0` | None | Neutral | I ordered a box. No, there's nothing in it, I just need the box. For me. |
| `StorageConstraint_1` | None | Neutral | My package is full of balls of yarn, I can't wait to play with them! |
| `StorageConstraint_2` | None | Excited | Please tell me that you received my fishing rod! |
| `StorageConstraint_3` | None | Neutral | I think you have a package for me. |
| `StorageConstraint_4` | None | Neutral | I believe you have a box for me. |
| `StorageConstraint_5` | None | Anxious | Do you have my package? It's, hmm, nothing special. |
| `StorageConstraint_6` | None | Neutral | I'm here to get my package. |
| `StorageConstraint_7` | None | Grumpy | My package is supposed to be here and it better not be damaged. |
| `StorageConstraint_8` | None | Neutral | I have a shipment on its way from $REGION$, do you have it? |
| `StorageConstraint_9` | None | Grumpy | I am waiting for a package. I hope The Captain didn't lose it at sea. |
| `StorageConstraint_10` | None | Neutral | My package is a cuckoo clock. Not the whole clock, just the little bird that comes out. Someone… ate the last one. |
| `StorageConstraint_11` | None | Excited | My delivery? Do you have it? |
| `StorageConstraint_12` | None | Neutral | I ordered a fancy laptop to nap—I mean, to work. Do you have my work laptop? |
| `StorageConstraint_13` | None | Neutral | You should have received my package, it's been a while since I ordered it. |
| `StorageConstraint_14` | None | Neutral | I'm expecting a delivery. |
| `StorageConstraint_15` | None | Neutral | I have a precious package on its way, do you have it? |
| `StorageConstraint_16` | Hot | Neutral | My package is supposed to stay warm for a better preservation. |
| `StorageConstraint_17` | Hot | Grumpy | I ordered some lasagna, I hope you kept them warm! |
| `StorageConstraint_18` | Hot | Grumpy | You don't need to know what's in my package, just that it has to stay warm!! |
| `StorageConstraint_19` | Hot | Neutral | My package contains special hot sauces that have to stay warm. |
| `StorageConstraint_20` | Hot | Neutral | My package is a bunch of special fire dragon scales. |
| `StorageConstraint_21` | Hot | Anxious | Please tell me that you stored my hot package properly. |
| `StorageConstraint_22` | Hot | Neutral | My package is from $REGION$, I hope the trip didn't let it cool down. |
| `StorageConstraint_23` | Hot | Friendly | Be careful, my package may be very hot. |
| `StorageConstraint_24` | Hot | Neutral | I am waiting for my delivery from $REGION$, it needs to be stored in a hot room. |
| `StorageConstraint_25` | Cold | Neutral | My package is supposed to stay cold otherwise it will rot. |
| `StorageConstraint_26` | Cold | Neutral | I am waiting for my delivery from $REGION$, I hope you kept it cold enough. |
| `StorageConstraint_27` | Cold | Neutral | I ordered special catnip seeds, they need to be refrigerated so they don't sprout. |
| `StorageConstraint_28` | Cold | Neutral | My package is a special fish delivery. |
| `StorageConstraint_29` | Cold | Neutral | My package is a box of ice creams. |
| `StorageConstraint_30` | Cold | Grumpy | I won't tell you what's in my package, just that it has to stay cold!! |
| `StorageConstraint_31` | Cold | Neutral | Have you stored my cold package properly? |
| `StorageConstraint_32` | Cold | Neutral | My package is supposed to be refrigerated. |
| `StorageConstraint_33` | Cold | Anxious | Please tell me that you stored my cold package properly. |
| `StorageConstraint_34` | Cold | Grumpy | My package contains ice cubes. Don't ask me why I ordered that. |
| `StorageConstraint_35` | Bright | Neutral | My package is supposed to stay in a bright room. |
| `StorageConstraint_36` | Bright | Neutral | I ordered sunflowers, they need a lot of light. |
| `StorageConstraint_37` | Bright | Neutral | My package contains very special crystals, they have to stay in a bright environment. |
| `StorageConstraint_38` | Bright | Neutral | My package is from $REGION$, it should be stored in a room with a large amount of light. |
| `StorageConstraint_39` | Bright | Neutral | My package is very special, it needs to stay in a bright environment. |
| `StorageConstraint_40` | Bright | Anxious | My package contains very rare plants from $REGION$, they absolutely need to stay in a bright room! |
| `StorageConstraint_41` | Bright | Neutral | Have you stored my bright package properly? |
| `StorageConstraint_42` | Bright | Neutral | My package contains flowers that always need some light. |
| `StorageConstraint_43` | Bright | Grumpy | You don't need to know what's in my package, just that it has to stay in a bright room!! |
| `StorageConstraint_44` | Dark | Neutral | My package is supposed to stay in a dark room. |
| `StorageConstraint_45` | Dark | Neutral | I ordered midnight spores, they need to stay away from the light. |
| `StorageConstraint_46` | Dark | Neutral | My package contains photographic films, they have to stay in a dark room. |
| `StorageConstraint_47` | Dark | Anxious | My package is from $REGION$, it should be stored in a room without a single ray of light. |
| `StorageConstraint_48` | Dark | Neutral | My package is very special, it needs to stay in a dark environment. |
| `StorageConstraint_49` | Dark | Anxious | My package contains very rare seeds from $REGION$, they absolutely need to stay in the dark! |
| `StorageConstraint_50` | Dark | Neutral | Have you stored my dark package properly? |
| `StorageConstraint_51` | Dark | Grumpy | My package contains photographic films, I hope you know it needs to be stored in the dark. |
| `StorageConstraint_52` | Dark | Grumpy | You don't need to know what's in my package, just that it has to stay in a dark room!! |

### BehaviorConstraint (15 lines)

*DESCRIPTION CLUE - removed by the mod*

| Term | Category | Mood | English |
|---|---|---|---|
| `BehaviorConstraint_0` | ConstraintHeavy | Friendly | Watch your back, it's heavy. |
| `BehaviorConstraint_1` | ConstraintHeavy | Neutral | I hope you have someone to help you because it's heavy. |
| `BehaviorConstraint_2` | ConstraintHeavy | Neutral | You look strong enough; it's really not lightweight. |
| `BehaviorConstraint_3` | ConstraintHeavy | Grumpy | You think you can carry this package with those arms? |
| `BehaviorConstraint_4` | ConstraintHeavy | Anxious | Please be careful it is heavy! |
| `BehaviorConstraint_5` | Fragile | Neutral | Be cautious, it's a fragile box. |
| `BehaviorConstraint_6` | Fragile | Grumpy | You better not be clumsy with this box. |
| `BehaviorConstraint_7` | Fragile | Anxious | Please be careful. There is a fragile item inside. |
| `BehaviorConstraint_8` | Fragile | Friendly | It's fragile, be careful handling it. |
| `BehaviorConstraint_9` | Fragile | Anxious | I hope it was stored properly, it's fragile. |
| `BehaviorConstraint_10` | ContactForbidden | Neutral | It's a very specific item that can't be close to similar objects. |
| `BehaviorConstraint_11` | ContactForbidden | Anxious | Be careful, it's a bit unstable and can produce strange reactions all around itself. |
| `BehaviorConstraint_12` | ContactForbidden | Grumpy | I believe you've been smart enough not to store it next to objects that could damage it. |
| `BehaviorConstraint_13` | ContactForbidden | Neutral | It's the kind of parcel that becomes unstable near other boxes. |
| `BehaviorConstraint_14` | ContactForbidden | Neutral | My package can't be stored next to some other boxes. |

### Link (10 lines)

*CONNECTOR - removed by the mod*

| Term | Category | Mood | English |
|---|---|---|---|
| `Link_0` | - | - | By the way, |
| `Link_1` | - | - | So, |
| `Link_2` | - | - | Also, |
| `Link_3` | - | - | First, |
| `Link_4` | - | - | Finally, |
| `Link_5` | - | - | But, |
| `Link_6` | - | - | For all I know, |
| `Link_7` | - | - | Another point is that |
| `Link_8` | - | - | And |
| `Link_9` | - | - | On top of that, |

### Hello (22 lines)

*structural (greeting/farewell) - kept*

| Term | Category | Mood | English |
|---|---|---|---|
| `Hello_0` | - | Friendly | Hello! |
| `Hello_1` | - | Neutral | Good morning. |
| `Hello_2` | - | Friendly | Hey! |
| `Hello_3` | - | Neutral | Sup? |
| `Hello_4` | - | Neutral | Good evening. |
| `Hello_5` | - | Friendly | What's up? |
| `Hello_6` | - | Neutral | Greetings. |
| `Hello_7` | - | Neutral | How do you do? |
| `Hello_8` | - | Neutral | Hi. |
| `Hello_9` | - | Friendly | Yo! |
| `Hello_10` | - | Friendly | Hello, nice to see you. |
| `Hello_11` | - | Friendly | Hello there. |
| `Hello_12` | - | Excited | Hiiii! |
| `Hello_13` | - | Neutral | Good day. |
| `Hello_14` | - | Friendly | Good to see you. |
| `Hello_15` | - | Friendly | G'day. |
| `Hello_16` | - | Shy | <size=60%>… Hello… Sorry to bother you…</size> |
| `Hello_17` | - | Anxious | Uh… Hello… |
| `Hello_18` | - | Shy | <size=60%>… Hi…</size> |
| `Hello_19` | - | Grumpy | Huh? Oh, hi. |
| `Hello_20` | - | Grumpy | You again. |
| `Hello_21` | - | Shy | <size=60%>… Hi… Do you have my package?</size> |

### HelloDeposit (10 lines)

*structural (greeting/farewell) - kept*

| Term | Category | Mood | English |
|---|---|---|---|
| `HelloDeposit_0` | - | Neutral | Hi! Here you are. Take care of this shipment please. |
| `HelloDeposit_1` | - | Neutral | Hello, can you take care of that? |
| `HelloDeposit_2` | - | Neutral | Hi, I'd like to send that. |
| `HelloDeposit_3` | - | Excited | Hiiii, my dad told me to ask you to send this! |
| `HelloDeposit_4` | - | Grumpy | Huh… I need to send that. |
| `HelloDeposit_5` | - | Shy | <size=60%>Hello… Sorry to bother you, I have this shipment to send.</size> |
| `HelloDeposit_6` | - | Friendly | Hello, how are you? Can you please take care of that? |
| `HelloDeposit_7` | - | Anxious | Uh… Hi… Can you please take extra care of this shipment? |
| `HelloDeposit_8` | - | Neutral | Good day, I'd like to send this shipment. |
| `HelloDeposit_9` | - | Neutral | Hello, I've brought something to ship. |

### Goodbye (10 lines)

*structural (greeting/farewell) - kept*

| Term | Category | Mood | English |
|---|---|---|---|
| `Goodbye_0` | - | Friendly | Thank you, goodbye! |
| `Goodbye_1` | - | Neutral | Thanks, have a nice day. |
| `Goodbye_2` | - | Friendly | Thank you, bye! |
| `Goodbye_3` | - | Friendly | See ya! |
| `Goodbye_4` | - | Grumpy | About time. |
| `Goodbye_5` | - | Shy | <size=60%>Thank you very much… Goodbye… </size> |
| `Goodbye_6` | - | Friendly | Thank you, my dear, have a heavenly day. |
| `Goodbye_7` | - | Friendly | Great, keep up the good work! |
| `Goodbye_8` | - | Grumpy | Finally. |
| `Goodbye_9` | - | Friendly | Thank you! Bye-bye! |

### GoodbyeRefused (5 lines)

*structural (greeting/farewell) - kept*

| Term | Category | Mood | English |
|---|---|---|---|
| `GoodbyeRefused_0` | - | Friendly | No worries, I'll come another time. See ya! |
| `GoodbyeRefused_1` | - | Neutral | It's ok, see you next time! |
| `GoodbyeRefused_2` | - | Grumpy | As if I had all the time in the world. |
| `GoodbyeRefused_3` | - | Anxious | Oh… No problem… I guess? |
| `GoodbyeRefused_4` | - | Anxious | Oh no, I hope there is no issue with my parcel… I'll come back later. |

### ByeDeposit (6 lines)

*structural (greeting/farewell) - kept*

| Term | Category | Mood | English |
|---|---|---|---|
| `ByeDeposit_0` | - | Neutral | Thank you! |
| `ByeDeposit_1` | - | Neutral | Please be careful with it. Bye! |
| `ByeDeposit_2` | - | Grumpy | You better be cautious. |
| `ByeDeposit_3` | - | Anxious | Thanks! I hope it won't get damaged… |
| `ByeDeposit_4` | - | Friendly | Thank you very much, take care of that! |
| `ByeDeposit_5` | - | Friendly | If you can send this as soon as you can, thank you, my dear. |

### RequestWrongParcel (8 lines)

*structural (greeting/farewell) - kept*

| Term | Category | Mood | English |
|---|---|---|---|
| `RequestWrongParcel_0` | - | Neutral | That one isn't for me. |
| `RequestWrongParcel_1` | - | Neutral | I believe this is the wrong parcel. |
| `RequestWrongParcel_2` | - | Neutral | That doesn't look like my parcel… |
| `RequestWrongParcel_3` | - | Neutral | Are you sure this is my parcel? Because I'm sure it isn't.  |
| `RequestWrongParcel_4` | - | Grumpy | Can you please focus? I don't have all the time.  |
| `RequestWrongParcel_5` | - | Shy | <size=60%>Uh… I'm really sorry… but it's not my parcel… sorry… </size> |
| `RequestWrongParcel_6` | - | Anxious | Oh, that's not my package. I hope mine didn't get lost… |
| `RequestWrongParcel_7` | - | Friendly | Oops, I'm sorry but this is not my parcel. |

---

## Why the mod keys on `DialogueType`

A customer request is assembled as a list of `DialogueData` lines: a greeting,
then one or more clue lines, optionally joined by `Link` connectors.
The parcel itself is chosen first; the dialogue only *describes* it.

Every parcel is labelled with a customer name (`EntityProperties.CustomerNameIndex`,
`CustomerSurnameIndex`, `CustomerLastNameInitial`, rendered into `CustomerNameText`),
so a name-based request is always solvable. That is what makes it safe to drop
the description clues.
