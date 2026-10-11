# Lucky Ace Casino

A social casino for **Android phones** (primary), **Linux**, and **Windows via Steam**, built in Unity 6.3 LTS.
You walk around a **3D casino** and play **blackjack, slots and roulette** at their tables in the room itself. The
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
| Cashier booth at the entrance, the bar along the west wall, 4 blackjack tables, a row of slot machines, a roulette table, a cocktail waitress walking the floor | `World/CasinoWorld.Build.cs` |
| Every game is played **in the room**: you sit down (the camera glides to the seat), cards and chips lie on the felt, the reels spin on the machine's screen, the wheel and board lie on the roulette table. The real dealer or croupier deals with their hands and talks to you; only the buttons are on the screen | `Screens/WorldTable.cs`, `Screens/*Screen.cs` |
| Realistic 3D staff: motion-captured idles, talking and walking; eye contact and head turning, relaxed hands, the waitress's tray on her palm, the host's wave (IK); jaw moves while talking; hair physics (a tiny spring-chain engine); soft synthesised voice; warm small talk | `World/StaffFigure.cs`, `Core/Physics/SpringChain.cs`, `Audio/Sfx.cs` |
| Gender choice sets the staff style (man → female staff, woman → male staff, prefer not to say → mixed); changeable in Settings | `Core/Player/Profile.cs`, `Screens/Staff.cs` |
| Blackjack: 6 decks, 3:2 blackjack, dealer stands on 17, double, split (up to 4 hands), insurance, dealer peek | `Core/Blackjack/*` |
| 4 tables: Bronze 10–500, Silver 100–5K, Gold 1K–50K, Diamond 10K–500K | `Core/Economy/TableCatalog.cs` |
| Slots "Lucky Ace 7s": 5 reels, 10 lines, wild and scatter; exact payback 95.9% shown in the paytable; no staged near-misses, and a return smaller than the bet is shown as "returned", never as a win | `Core/Slots/SlotMachine.cs`, `Screens/SlotsScreen.cs` |
| European roulette: straight up, columns, dozens, red/black, odd/even, low/high; the same 2.7% edge on every bet | `Core/Roulette/Roulette.cs`, `Screens/RouletteScreen.cs` |
| 1,000 free starting chips; rewarded ads for +250 chips (12/day, 90 s cooldown) | `GameConfig.cs`, `Purchases.cs` |
| Chip packs and food/drinks for real money, priced per country | `Core/Economy/*`, `Screens/ShopModal.cs` |
| Energy bar: drains while playing (45 min), refills slowly while away (8 h), food refills it instantly | `Core/Player/EnergyModel.cs` |
| Adaptive music: bigger bets (absolute, relative to the table, relative to bankroll) make it faster and harder | `Core/Player/MusicIntensity.cs`, `Audio/SynthEngine.cs` |
| Dealers plead "one more hand" when you leave: **opt-in** in Settings, off by default, Leave always works | `Screens/BlackjackScreen.cs` (`PersuasionModal`) |
| Beginner's Luck welcome boost (see below) | `Core/Blackjack/DrawStrategies.cs`, `Core/Player/BeginnerLuck.cs` |

All tunable numbers (prices, energy timings, ad rewards, boost length, ad and Steam IDs) are in `Assets/Scripts/Game/GameConfig.cs`
and `Core/Economy/ProductCatalog.cs`.

### Beginner's Luck (the hook)

New players win almost every hand at first: for the first 3 hands the shoe is stacked in the player's favour, and
over the next 12 the boost fades to **standard, fair blackjack** (a house edge of about 0.6%, measured in the tests).
The boost is **shown on screen** with a meter and an explanation, and it can only ever help the player.
The odds are never secretly pushed below fair; see "Decisions" at the end.

### Regional pricing

Prices scale with each country's economy: `multiplier = (GDP per capita / US GDP per capita)^0.35`, clamped to
0.2–1.0, using World Bank 2025 data for 214 countries (`Tools/build_price_table.py`). Examples for the $4.99 pack:
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

- **Google Play:** create the 12 consumable products (IDs in `ProductCatalog.cs`) in Play Console. Replace the AdMob
  test IDs in `GameConfig` (the build copies the app ID into the AdMob plugin settings and the Android manifest).
- **Steam:** purchases use the Steam Inventory Service. Create item definitions 1001–1006 (chips) and 2001–2006 (food)
  with price categories; Steam localises the prices per region. Set `GameConfig.SteamAppId`.
- **Linux outside Steam** has no payment store, so purchases are disabled in release builds there.

## The staff (realistic 3D people)

The twelve staff models in `Assets/Resources/Staff/` (host, cashier, server and three dealers, each as a woman and a
man) are generated by `Tools/make_staff.py` in Blender 4.2 with the MPFB2 add-on (MakeHuman):

```
blender -b --python Tools/make_staff.py -- <out_dir> [names] [--preview]
```

The script builds each person (proportions, skin, eyes, hair, a generated cocktail dress or a suit), switches to
a low-poly body, rigs it for Unity, and exports the FBX and textures. `Editor/StaffImporter.cs` imports them as
Humanoid and builds the shared animator from the CC0 clips in `Assets/Characters/Animations/`. Blender and MPFB live
in `Tools/.external/` (not in the repository). The add-on is only used to make the models and is not shipped.

## Replacing the generated art and music

- **Staff portraits** (used in dialogs): drop PNGs into `Assets/Resources/Dealers/Female/` and `Assets/Resources/Dealers/Male/`. They
  replace the generated portraits automatically.
- **Music:** add loops named `intensity_0`, `intensity_1`, … (same tempo and length) to `Assets/Resources/Music/`. They
  crossfade by intensity and replace the synthesizer.

## Tests

The game logic is engine-independent (`Assets/Scripts/Core`, asmdef `Casino.Core`) and has a test suite that runs
without Unity:

```
cd Tests/CoreTests && dotnet run -c Release
```

It has 33 tests: payouts, insurance, splits, doubles, the age-gate boundaries, energy, pricing, the offline country
lookup (20 cities), simulations of 400,000 fair hands (≈43.5% wins, ≈0.6% house edge) and of the boost, the slot
machine's exact payback (enumerated, then confirmed by simulation), the roulette edge on every bet type, and the hair
physics (swings, settles, never explodes).

`-casinoTour <folder>` on a desktop build plays through every screen and the 3D casino (walking to a table by tapping
it, staff close-ups, a few hands of blackjack, slot spins, a roulette round) and saves screenshots plus an error log.

## Before launch (checklist)

- **Receipt validation** on a server before granting purchases (marked `TODO` in `UnityIapStore.cs`). The chip
  balance is stored locally with tamper detection only; a real-money economy should be server-authoritative.
- **Privacy policy:** location (country only, processed on the device), the IP lookup on desktop, and AdMob data.
  Fill in Google Play's **Data safety** form, including approximate location.
- In Player Settings > Android, tick **Low Accuracy Location** so the app requests approximate rather than precise location.
- **Age rating:** complete the IARC questionnaire (simulated gambling). Keep the "no cash value" wording; it is
  shown on the age gate, in the lobby footer, and in the shop.
- Replace the placeholder name, the bundle ID `com.luckyace.casino`, and the test ad and Steam IDs.

## Decisions that differ from the original brief

- **The win-rate system is visible and never goes below fair.** The brief asked for a hidden system that starts at
  100% wins and drops to 25%. Secretly rigging cards in a game where people buy chips with real money deceives the
  customer; it breaks Google Play's deceptive-behaviour policy and exposes the publisher to consumer-protection
  claims. Beginner's Luck gives the same strong first impression honestly.
- **"Prefer not to say"** was added as a third gender option (it gives mixed staff), and the staff style can be changed in Settings.
- **Staff are glamorous, not explicit**: curvy figures in short cocktail dresses (and handsome, fitted suits for the
  male staff), with hair physics but no chest physics. Google Play's sexual-content rules apply to game characters,
  and AdMob limits ads in sexually suggestive apps.
- **Staff charm is never used to sell.** Their warm, flirty small talk is kept apart from buy and bet prompts, and they
  never use affection to keep someone playing or paying.
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
