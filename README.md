# Lucky Ace Casino

A social casino for **Android phones** (primary), **Linux**, and **Windows via Steam**, built in Unity 6.3 LTS.
You walk around a **3D casino** and play **blackjack, slots, roulette, baccarat, craps, sic bo, Ultimate Texas
Hold'em and video poker** at their tables and machines in the room itself. The
casino, UI, cards, chips, music and sound are generated in code at runtime, so the project has almost no image or
audio assets. The exception is the staff: realistic rigged 3D people (MakeHuman, CC0) animated with CC0
motion-capture clips. The game stays small and runs on low-end phones.

Previews of the generated art, the 3D casino and the adaptive music are in `Docs/previews/`.

## Open and run

1. Install **Unity 6000.3.25f1** (6.3 LTS) with the **Android Build Support** and **Linux Build Support** modules
   (plus Windows Build Support for the Steam build). With the Unity CLI: `unity install 6000.3.25f1`.
2. Open this folder in Unity Hub. On first open the project creates `Assets/Scenes/Main.unity`, adds it to the build,
   and applies player settings (landscape, IL2CPP + ARMv7/ARM64, gamma colour space).
3. Press **Play**. The game boots itself from code (`App.Bootstrap`); the scene stays empty.
4. Builds: menu **Casino > Build** (Android APK / App Bundle / Gradle project, Linux, Windows (Steam)), or headless:
   `Unity -batchmode -quit -projectPath . -executeMethod Casino.EditorTools.CasinoBuild.Linux`.
   For Android, see "Android builds" below.

In the Editor and in development builds, purchases and ads are **simulated** (a confirmation dialog and a 5-second
placeholder). No money changes hands.

## What's in the game

| Feature | Where |
|---|---|
| Neutral age gate (date of birth on an on-screen keypad, so the phone keyboard never covers it). Under 18: blocked, the block persists, the app closes | `Screens/Onboarding.cs`, `Core/Player/AgeGate.cs` |
| Walkable 3D casino (first person): stick + drag-to-look, or tap anywhere to walk there. No windows; chandeliers, neon coves, chasing marquee bulbs, glowing bottle wall, flashing slots | `World/*`, `Screens/LobbyScreen.cs` |
| Cashier booth at the entrance, the bar along the west wall, 4 blackjack tables, a row of slot machines, a roulette table, an Ultimate Hold'em table, a baccarat table, a craps table, a sic bo table, a row of video poker machines along the back wall, a cocktail waitress walking the floor | `World/CasinoWorld.Build.cs` |
| Every game is played **in the room**: you sit down (the camera glides to the seat), cards and chips lie on the felt, the reels spin on the machine's screen, the wheel and board lie on the roulette table. The real dealer or croupier deals with their hands and talks to you; only the buttons are on the screen | `Screens/WorldTable.cs`, `Screens/*Screen.cs` |
| Realistic 3D staff: motion-captured idles, talking and walking; eye contact and head turning, relaxed hands, the waitress's tray on her palm, the host's wave (IK); jaw moves while talking; hair physics (a tiny spring-chain engine); soft synthesised voice; warm small talk | `World/StaffFigure.cs`, `Core/Physics/SpringChain.cs`, `Audio/Sfx.cs` |
| Gender choice sets the staff style (man → female staff, woman → male staff, prefer not to say → mixed); changeable in Settings | `Core/Player/Profile.cs`, `Screens/Staff.cs` |
| Blackjack: 6 decks, 3:2 blackjack, dealer stands on 17, double, split (up to 4 hands), insurance, dealer peek | `Core/Blackjack/*` |
| 4 tables: Bronze 10–500, Silver 100–5K, Gold 1K–50K, Diamond 10K–500K | `Core/Economy/TableCatalog.cs` |
| Slots "Lucky Ace 7s": 5 reels, 10 lines, wild and scatter; exact payback 95.8% shown in the paytable; no staged near-misses, and a return smaller than the bet is shown as "returned", never as a win | `Core/Slots/SlotMachine.cs`, `Screens/SlotsScreen.cs` |
| European roulette: straight up, columns, dozens, red/black, odd/even, low/high; the same 2.7% edge on every bet | `Core/Roulette/Roulette.cs`, `Screens/RouletteScreen.cs` |
| Ultimate Texas Hold'em against the dealer: equal Ante and Blind, one raise (4×/3× before the flop, 2× after it, 1× at the river or fold), the dealer qualifies with a pair, the Blind pays a bonus from a straight up; ≈2% house edge played well | `Core/Poker/UltimateHoldem.cs`, `Screens/HoldemScreen.cs` |
| Baccarat (punto banco, 8 decks, no choices): Player 1:1, Banker 1:1 less 5% commission, Tie 8:1; house edges: Banker 1.06%, Player 1.23%, Tie 14.1% | `Core/Baccarat/Baccarat.cs`, `Screens/BaccaratScreen.cs` |
| Craps: Pass and Don't Pass with a point, free Odds behind the Pass Line at true odds (3-4-5×), the Field, Place bets, and one-roll bets; Pass edge ≈1.4% | `Core/Dice/Craps.cs`, `Screens/CrapsScreen.cs` |
| Sic bo: Small/Big and Odd/Even at 1:1 (2.8% edge, the best bets on the table), totals, doubles, triples and single-die bets; every bet's edge is exact over the 216 rolls | `Core/Dice/SicBo.cs`, `Screens/SicBoScreen.cs` |
| Video poker "Jacks or Better", full-pay 9/6: five cards, hold any, draw once; ≈99.5% back with perfect play — skill matters | `Core/VideoPoker/VideoPoker.cs`, `Screens/VideoPokerScreen.cs` |
| 1,000 free starting chips; rewarded ads for +250 chips (12/day, 90 s cooldown) | `GameConfig.cs`, `Purchases.cs` |
| Chip packs and food/drinks for real money, priced per country | `Core/Economy/*`, `Screens/ShopModal.cs` |
| Energy bar: drains while playing (45 min), refills slowly while away (8 h), food refills it instantly | `Core/Player/EnergyModel.cs` |
| Adaptive music: bigger bets (absolute, relative to the table, relative to bankroll) make it faster and harder | `Core/Player/MusicIntensity.cs`, `Audio/SynthEngine.cs` |
| Dealers plead "one more hand" when you leave a table, more insistently each time; LEAVE is always on screen and is also what Back and a controller's first focus do | `Screens/BlackjackScreen.cs` (`PersuasionModal`) |
| Beginner's Luck welcome boost (see below) | `Core/Blackjack/DrawStrategies.cs`, `Core/Player/BeginnerLuck.cs` |
| **Lucky Streak** (real money): after Beginner's Luck, packs of blackjack hands at a fixed boost (25 hands at 20%, 50 at 30%) on bets up to 1,000 chips, shown on screen with the expected win rate; offered once when Beginner's Luck ends, and in the shop's Luck tab | `Core/Player/LuckBoost.cs`, `Screens/ShopModal.cs` |
| **Daily streak** (Duolingo style): a 🔥 counter that grows on each day you play 10 rounds of anything; streak freezes cover a missed day (one earned every 7 days, one per rewarded video, or bought with chips); the staff tease you about it, Duolingo style; milestone rewards at 3, 7, 14, 30… days | `Core/Player/Progression.cs`, `UI/Widgets/ProgressWidgets.cs` |
| **Daily bonus wheel**: one free spin a day, prizes grow with the streak (up to ×2); the chance of every prize is printed under the wheel | `Screens/DailyModal.cs` |
| **Free chips every 3 hours**, no ad needed (the fallback when ads are blocked) | `Game/Progress.cs`, `Screens/LobbyScreen.cs` |
| **Levels and XP**: XP per round played (never per amount bet); level-ups pay chips and unlock the Silver (3), Gold (6) and Diamond (10) tables | `Core/Player/Progression.cs` |
| **Daily missions**: three a day, one per game, with chip and XP rewards and a bonus for finishing all three | `Core/Player/Progression.cs` |
| **Reminders** (Android, opt-in): "free chips ready" and an evening nudge before a streak would end; at most two, never while playing, off switch in Settings | `Platform/Reminders.cs` |
| **Controllers**: Xbox, PlayStation and Nintendo-style pads (USB or Bluetooth) through Unity's Input System; the family is detected for button names, and Nintendo pads confirm with A on the right. Left stick walks, right stick looks, D-pad moves between any buttons (HUD and the 3D tables), Menu opens Settings. On-screen hints, which can be turned off | `Platform/Pad.cs`, `UI/PadNavigator.cs` |
| **Vibration**: phone (Android vibrator, with strength) and controller rumble; Auto moves it to the controller while one is connected; separate on/off and strength for each. Only taps, wins and warnings vibrate, never losses | `Platform/Haptics.cs` |
| **Graphics quality = realism, then cinema**: each step up adds to the one below. Low is the baked look on its own; Medium makes polished marble, wood, lacquer and gold reflect the room; High is the film look: 2× texture detail, pools of light on the floor and soft beams of light coming down through a faint haze, HDR bloom with filmic tone mapping, a film grade (cool shadows, warm highlights, gentle S-curve), grain and a vignette, and staff lit by the lamp above them with warm light under the skin and the casino glinting in their eyes; Ultra is the cinema camera: depth of field with an autofocus that pulls smoothly to the person or game you are about to use, or else to whatever you look at (the room melts away behind a face or the cards), anamorphic lens streaks on neon and bulbs, faint colour fringes at the frame edges, dust glinting in the beams, a slight handheld drift, 4× texture detail, sharper reflections, a finer, wider bloom and everyone animated however far away | `World/CasinoWorld.Look.cs`, `World/WorldPost.cs`, `Resources/Shaders` |
| **Settings** tabs: General, Graphics (quality, resolution, anti-aliasing, 30/60/120 FPS, field of view, FPS counter, window mode and v-sync on computers), Sound (master, music, effects, staff voices, mute in background), Touch (sensitivity, invert X/Y), Controller (only once one is detected: layout, stick sensitivity, invert X/Y, hints) and Vibration | `Screens/SettingsModal.cs` |
| **89 languages**: the whole game is translated; the language follows the device (or Steam) language, matching region variants, and a picker in Settings > General overrides it. Right-to-left languages are supported, each script gets the right font, and anything missing falls back to English | `Game/Locale/*`, `Tools/lang/`, `Tools/build_locale.py` |

All tunable numbers (prices, energy timings, ad rewards, boost length, ad and Steam IDs) are in `Assets/Scripts/Game/GameConfig.cs`
and `Core/Economy/ProductCatalog.cs`.

### Beginner's Luck (the hook)

New players win almost every hand at first: for the first 3 hands the shoe is stacked in the player's favour, and
over the next 12 the boost fades to **standard, fair blackjack** (a house edge of about 0.6%, measured in the tests).
The boost is **shown on screen** with a meter and an explanation, and it can only ever help the player.
The odds are never secretly pushed below fair; see "Decisions" at the end.

### Regional pricing

Prices scale with each country's economy: `multiplier = (GDP per capita / US GDP per capita)^0.35`, clamped to
0.2–1.0, using World Bank WDI data for 250 countries and territories (`Tools/build_price_table.py`). Examples for the $4.99 pack:
Germany ≈ $4.29, Bulgaria ≈ $2.89, India ≈ $1.39.

Which country applies:
1. **Store country** (what actually gets charged). Google Play and Steam tie this to the account's payment method,
   so a VPN does not change it.
2. On phones, the **GPS location**, matched to a country fully offline against bundled Natural Earth borders
   (`Resources/Geo/countries.bytes`, `Core/Region/CountryLookup.cs`). Nothing is sent anywhere.
3. If location is declined: the **cell network's country** (also VPN-proof), otherwise standard (full) price.
4. **IP lookup only on hardware with no location sensor and no cellular radio** (desktops), as you specified.

Two modes in `GameConfig.Pricing`:
- `StoreNative` (default, recommended): one SKU per product; set per-country prices in Play Console and Steamworks
  from the sheet exported by **Casino > Pricing > Export Regional Price Sheet**.
- `LocationTiers`: five SKUs per product (`chips_5k_t1` … `_t5`); the verified location picks the tier.

### The 3D casino on a potato

- No real-time lights or shadows: about 120 lights are baked into vertex colours once at load (`World/MeshKit.cs`).
- Reflections come from one cubemap of the room, rendered once at load (and again only when the quality changes) and box-projected onto the walls, so the floor reflects them in the right place for the cost of a texture read. Low skips it entirely; the film pass (bloom, tone mapping, grade, grain) only runs on High and Ultra, and the depth of field (which needs a depth pre-pass), lens streaks and dust only on Ultra.
- Every light halo in the room is one mesh, expanded to face the camera in the shader (`Resources/Shaders/CasinoGlow.shader`): one draw call.
- The whole room is about 10 draw calls plus one per staff member and sign. Small vertex-lit shaders in `Resources/Shaders`.
- Each staff member is one skinned mesh with phone-sized textures (ASTC); far-away staff stop animating.
- Collisions are 2D circles and boxes on the floor plan (`World/PlayerRig.cs`), so the physics module isn't needed.
- The casino is built once (about 1 s on a desktop, a few seconds on a slow phone, behind an "Opening the doors" screen), then kept and hidden while you're at a table.

## Android builds

Android builds use the standard tools. Unity exports a Gradle project, and `Tools/android_build.sh` does the rest:

| Task | Tool |
|---|---|
| Build the APKs (one per CPU type, to sideload) and the app bundle | Gradle (`assembleRelease`, `bundleRelease`), Unity's bundled Gradle and JDK |
| Install Android SDK packages | `sdkmanager` (Unity's SDK, `cmdline-tools`) |
| Validate the bundle, build the APK set a phone gets, report download sizes | `bundletool` |
| Install and debug on a phone | `adb` (`--install`, `--log`) |
| Upload and manage Play Store releases | `gpc` (Google Play Console CLI), after `gpc setup` with your Play service account |

```
Tools/android_build.sh --export --install --log
```

`--export` runs **Casino > Build > Android Gradle project** headless (a test build with the test store and test ads;
add `-release` for a store build). Outputs go to `Builds/Android/`. Store uploads need an upload key
(Player Settings > Publishing Settings); local builds are signed with the debug key.

## Turning on real stores and ads

Install the package, then tick the integration under **Casino > Integrations** (this sets a scripting define).
All three adapters have been compile-checked against the real package APIs. **AdMob is already installed** (OpenUPM
packages in `Packages/manifest.json`) and switched on for Android: sideload builds show Google's real test ads.

| Integration | Package | Define |
|---|---|---|
| Google Play billing | Unity IAP 5.x (`com.unity.purchasing`) | `CASINO_UNITY_IAP` |
| Rewarded ads + GDPR consent | Google Mobile Ads Unity plugin 11.x | `CASINO_ADMOB` |
| Steam purchases (Windows + Linux) | Steamworks.NET 2025.x | `CASINO_STEAMWORKS` |

- **Google Play:** create the 14 consumable products (IDs in `ProductCatalog.cs`) in Play Console. Replace the AdMob
  test IDs in `GameConfig` (the build copies the app ID into the AdMob plugin settings and the Android manifest).
- **Steam:** purchases use the Steam Inventory Service. Create item definitions 1001–1006 (chips), 2001–2006 (food) and
  3001–3002 (luck packs) with price categories; Steam localises the prices per region. Set `GameConfig.SteamAppId`.
- **Linux outside Steam** has no payment store, so purchases are disabled in release builds there.

## The staff (realistic 3D people)

The twelve staff models in `Assets/Resources/Staff/` (host, cashier, server and three dealers, each as a woman and a
man) are generated by `Tools/make_staff.py` in Blender 4.2 with the MPFB2 add-on (MakeHuman):

```
blender -b --python Tools/make_staff.py -- <out_dir> [names] [--preview]
```

The script builds each person (proportions, skin, eyes, hair, a generated cocktail dress or a suit), uses
MakeHuman's detailed body with face shaping (glamorous for the women, handsome for the men), thins the triangles below the
neck (the head keeps all of them), rigs it for Unity, and exports the FBX and textures. `Editor/StaffImporter.cs` imports them as
Humanoid and builds the shared animator from the CC0 clips in `Assets/Characters/Animations/`. Blender and MPFB live
in `Tools/.external/` (not in the repository). The add-on is only used to make the models and is not shipped.

## Replacing the generated art and music

- **Staff portraits** (used in dialogs): drop PNGs into `Assets/Resources/Dealers/Female/` and `Assets/Resources/Dealers/Male/`. They
  replace the generated portraits automatically.
- **Music:** add loops named `intensity_0`, `intensity_1`, … (same tempo and length) to `Assets/Resources/Music/`. They
  crossfade by intensity and replace the synthesizer.

## Translations

The English text lives in the code (in `Loc.T/F/N` and `Tr.T/F/Mark` calls). The build tools collect and check it:

```
Tools/.external/pyenv/bin/python Tools/build_locale.py [extract|check|build]
```

- `extract` writes the English phrases to `Tools/lang/en.txt` — one `id<TAB>English<TAB>where` line per phrase. The id
  is a hash of the English, so a translation follows its phrase wherever the code moves.
- A language is a file `Tools/lang/<code>.txt` with `id<TAB>translation` lines; only the phrases that differ from
  English need a line, anything missing falls back to English at runtime.
- `check` reports, per language, missing or stale phrases and broken `{0}` placeholders or tags.
- `build` regenerates `Assets/Resources/Lang/` (keys, one file per language, the fonts) and the language list in
  `Assets/Scripts/Game/Locale/LanguageTable.cs`.

The game picks the device (or Steam) language on first launch, matching region variants (e.g. `pt-BR`, `zh-Hant`), and
Settings > General has a picker that overrides it. Arabic, Persian, Hebrew, Pashto and Urdu run right-to-left.

## Tests

The game logic is engine-independent (`Assets/Scripts/Core`, asmdef `Casino.Core`) and has a test suite that runs
without Unity:

```
cd Tests/CoreTests && dotnet run -c Release
```

It has 52 tests: payouts, insurance, splits, doubles, the age-gate boundaries, energy, pricing, the offline country
lookup (20 cities), simulations of 400,000 fair hands (≈43.5% wins, ≈0.6% house edge) and of the boost, the slot
machine's exact payback (enumerated, then confirmed by simulation), the roulette edge on every bet type, the hair
physics (swings, settles, never explodes), and the comeback systems: streak days, freezes and milestones, the level
curve, the wheel's real odds matching the printed ones, and daily missions. The newer games are covered the same way:
hold'em payouts and its edge with good play (≈2% of the ante), baccarat's drawing rules and exact edges, video poker
hand evaluation and its payback with the simple strategy (98.8%), sic bo's exact edges over the 216 rolls, and craps
rules and house edges.

`-casinoTour <folder>` on a desktop build plays through every screen and the 3D casino (walking to a table by tapping
it, staff close-ups, a few hands of blackjack, slot spins, a roulette round, and a round each of hold'em, video poker,
sic bo, craps and baccarat) and saves screenshots plus an error log. Add `-tourLanguages bg,ar,hi` to screenshot the
main screens in those languages too.

## Before launch (checklist)

- **Receipt validation** on a server before granting purchases (marked `TODO` in `UnityIapStore.cs`). The chip
  balance is stored locally with tamper detection only; a real-money economy should be server-authoritative.
- **Privacy policy:** location (country only, processed on the device), the IP lookup on desktop, and AdMob data.
  Fill in Google Play's **Data safety** form, including approximate location.
- In Player Settings > Android, tick **Low Accuracy Location** so the app requests approximate rather than precise location.
- **Age rating:** complete the IARC questionnaire (simulated gambling). Keep the "no cash value" wording; it is
  shown on the age gate, in the lobby footer, and in the shop.
- Replace the placeholder name, the bundle ID `com.luckyace.casino`, and the test ad and Steam IDs.

## Roadmap

- **Weekly tournaments and leaderboards** (e.g. biggest slot win multiple, most blackjack wins), with chip prizes.
- **Achievements** for milestones across all games.
- **A progressive slot jackpot**, with its odds published in the paytable like everything else.

## Decisions that differ from the original brief

- **The win-rate system is visible and never goes below fair.** The brief asked for a hidden system that starts at
  100% wins and drops to 25%. Secretly rigging cards in a game where people buy chips with real money deceives the
  customer; it breaks Google Play's deceptive-behaviour policy and exposes the publisher to consumer-protection
  claims. Beginner's Luck gives the same strong first impression honestly.
- **"Prefer not to say"** was added as a third gender option (it gives mixed staff), and the staff style can be changed in Settings.
- **Staff are glamorous, not explicit**: curvy figures in short cocktail dresses (and handsome, fitted suits for the
  male staff), with hair physics but no chest physics. Google Play's sexual-content rules apply to game characters,
  and AdMob limits ads in sexually suggestive apps.
- **Staff charm is never used to sell.** Their warm, flirty small talk is kept apart from buy and bet prompts. The one
  place they ask you to keep playing is the dealer's "one more hand" when you leave a table (always on, by the
  owner's choice); LEAVE is always on screen, and Back or a controller's first press leaves.
- **The comeback systems reward showing up, not spending.** XP comes per round, not per amount bet; the wheel prints
  its real odds; streak freezes are earned by playing, from a rewarded video, or bought with chips; there is no paid
  "streak repair"; and reminders are opt-in, at most two, and never sent while playing. Missions never require a
  purchase, and the staff's streak teasing is about showing up, never about betting more, buying or staying.
- **Paid luck says exactly what it does and is priced like chips.** A Lucky Streak shows its boost, its hands, its
  bet cap and the win rate it gives (measured by simulation and re-checked by the tests), never drops below fair blackjack
  when it ends, and is priced so its expected chips at the cap match what the same money buys as chips (also tested).
  The offer appears once, when Beginner's Luck ends, and a controller's first focus on it is "No thanks".
- **Ads can be blocked by the player's network** (DNS ad blockers block Google's ad servers). The game detects this,
  says so, and offers the timed free chips instead. It does not try to get around ad blockers: AdMob forbids it, and
  it's the player's choice.
- **Energy recovers slowly while the game is closed**, so it is never a hard paywall. Set
  `EnergyHoursToFullWhileAway` to 0 to disable this.
- **The location permission is explained before Android asks for it.** This also serves as Google Play's required
  prominent disclosure.

## Licence

Lucky Ace Casino is free software, licensed under the **GNU General Public License, version 2** (GPL-2.0-only); see
`LICENSE`. Bundled third-party assets keep their own licences (listed under Credits): the fonts are under the SIL Open
Font License (texts in `Assets/Resources/Fonts/`), and the staff models and animations are CC0.

## Credits

Fonts: Bebas Neue and Poppins (SIL Open Font License, included). Borders: Natural Earth (public domain).
Price data: World Bank WDI (CC BY 4.0). Staff models: made with MakeHuman / MPFB2; the MakeHuman base mesh and assets
are CC0. Animations: Quaternius Universal Animation Library 1 and 2 (CC0).
