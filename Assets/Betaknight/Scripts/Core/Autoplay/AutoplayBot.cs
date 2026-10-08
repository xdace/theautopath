using System;
using System.Collections.Generic;
using System.Linq;
using Betaknight.Core.Arena;
using Betaknight.Core.Circuit;
using Betaknight.Core.Encounters;
using Betaknight.Core.Gear;
using Betaknight.Core.Hex;
using Betaknight.Core.Map;
using Betaknight.Core.Modules;
using Betaknight.Core.Run;
using Betaknight.Core.Runes;
using Betaknight.Core.Skills;

namespace Betaknight.Core.Autoplay
{
    /// <summary>
    /// Automatischer Testspieler mit der Strategie «einfach, aber vollständig»: wählt ein Kit, erkundet bevorzugt
    /// unbekannte Felder, nimmt Angebote nach einfacher Wertung (Verbesserung &gt; neue Karte &gt; Gold), rüstet bessere
    /// Teile aus, setzt freie Skills, Runen und Module ein, legt mindestens einen Auslöser, nutzt Lagerfeuer, Shop und
    /// Minen und geht nach dem Boss durchs Portal. Pro Aufruf von <see cref="Decide"/> genau eine Aktion; ausgeführt
    /// wird sie über die öffentlichen Session-Methoden, die auch die UI benutzt. Spiellogik steckt hier keine.
    /// </summary>
    public sealed class AutoplayBot
    {
        /// <summary>Unter diesem HP-Anteil (Prozent) ruht der Bot am Lagerfeuer, heilt im Shop und meidet Kämpfe.</summary>
        public const int LowHpPercent = 60;

        /// <summary>Unter diesem HP-Anteil werden Gegnerfelder gemieden, solange es eine Alternative gibt.</summary>
        public const int AvoidFightPercent = 40;

        /// <summary>Höchstens so viele Tafel- und Inventar-Aktionen pro Zug (Schutz gegen Hin-und-her).</summary>
        public const int MaxLoadoutActionsPerTurn = 24;

        /// <summary>Unbesetzte, bekannte Minen in dieser Entfernung steuert der Bot gezielt an.</summary>
        public const int MineDetour = 4;

        /// <summary>Ab so viel Gold steuert der Bot einen bekannten, noch nicht besuchten Shop an.</summary>
        public const int ShopGold = 12;

        private readonly Random _random;
        private readonly HashSet<string> _failed = new HashSet<string>();
        private int _failedTurn = -1;
        private int _loadoutTurn = -1;
        private int _loadoutActions;

        public AutoplayBot(int seed)
        {
            _random = new Random(unchecked(seed * 7919 + 17));
        }

        /// <summary>Kit nach Seed: verschiedene Seeds spielen verschiedene Ritter.</summary>
        public static KnightKit ChooseKit(IReadOnlyList<KnightKit> kits, int seed)
        {
            if (kits == null || kits.Count == 0) return null;
            return kits[(int)((uint)seed % (uint)kits.Count)];
        }

        /// <summary>Meldet, dass eine Aktion abgelehnt wurde; sie wird in diesem Zug nicht wiederholt.</summary>
        public void MarkFailed(OverworldSession session, BotAction action)
        {
            SyncTurn(session);
            if (action != null) _failed.Add(action.Description);
        }

        /// <summary>Die nächste Aktion für diesen Zustand, oder <see cref="BotActionKind.None"/>, wenn nichts geht.</summary>
        public BotAction Decide(OverworldSession s)
        {
            if (s == null) return BotAction.Nothing(AutoplayTexts.NoSession);
            if (s.IsGameOver) return BotAction.Nothing(AutoplayTexts.EndGameOver);
            SyncTurn(s);

            BotAction action = Overflow(s) ?? Encounter(s) ?? Offer(s) ?? Shop(s) ?? Portal(s);
            if (action != null) return action;
            if (s.IsBusy) return BotAction.Nothing(AutoplayTexts.SessionBusy);

            if (s.CanChangeLoadout && _loadoutActions < MaxLoadoutActionsPerTurn)
            {
                action = Loadout(s);
                if (action != null)
                {
                    _loadoutActions++;
                    return action;
                }
            }

            return Move(s) ?? BotAction.Nothing(AutoplayTexts.NoWalkableTile);
        }

        private void SyncTurn(OverworldSession s)
        {
            int turn = s.Turns.CurrentTurn;
            if (_failedTurn != turn)
            {
                _failed.Clear();
                _failedTurn = turn;
            }
            if (_loadoutTurn != turn)
            {
                _loadoutActions = 0;
                _loadoutTurn = turn;
            }
        }

        private BotAction Try(BotActionKind kind, string description, Func<bool> execute, string reward = null,
            HexCoord? step = null, bool setsTrigger = false) =>
            _failed.Contains(description) ? null : new BotAction(kind, description, execute, reward, step, setsTrigger);

        private static int HpPercent(OverworldSession s) => s.Stats.MaxHp <= 0 ? 0 : s.Stats.Hp * 100 / s.Stats.MaxHp;

        // ------------------------------------------------------------------ Überlauf

        private BotAction Overflow(OverworldSession s)
        {
            if (s.PendingItem != null)
                return Try(BotActionKind.Overflow, AutoplayTexts.DiscardItem(s.PendingItem.Name, false), s.RejectPendingItem)
                    ?? Try(BotActionKind.Overflow, AutoplayTexts.DiscardItem(s.PendingItem.Name, true), s.RejectPendingItem);
            if (s.PendingRune != null)
                return Try(BotActionKind.Overflow, AutoplayTexts.DiscardRune(s.PendingRune.Name, false), s.RejectPendingRune)
                    ?? Try(BotActionKind.Overflow, AutoplayTexts.DiscardRune(s.PendingRune.Name, true), s.RejectPendingRune);
            return null;
        }

        // ------------------------------------------------------------------ Events

        private BotAction Encounter(OverworldSession s)
        {
            EncounterPrompt prompt = s.PendingEncounter;
            if (prompt == null) return null;
            IReadOnlyList<EncounterOption> options = prompt.Definition.Options;
            bool low = HpPercent(s) < LowHpPercent;

            var order = new List<int>();
            if (prompt.Definition.Id == "campfire")
            {
                // Lagerfeuer: ruhen bei wenig HP, sonst eine Rune verstärken (falls eine steigen kann).
                bool canUpgrade = s.Board.BestUpgradeTarget() >= 0;
                order.Add(low || !canUpgrade ? 0 : 1);
            }
            else
            {
                // Optionen mit HP-Kosten nur, wenn genug HP da sind.
                for (int i = 0; i < options.Count; i++)
                    if (!low || !options[i].Effects.Any(e => e.Kind == EffectKind.Damage)) order.Add(i);
            }
            for (int i = 0; i < options.Count; i++) if (!order.Contains(i)) order.Add(i);

            foreach (int i in order)
            {
                if (!options[i].IsAvailable(s.Stats)) continue;
                int index = i;
                BotAction a = Try(BotActionKind.Encounter, AutoplayTexts.EncounterChoice(prompt.Definition.Title, options[i].Text),
                    () => s.ChooseEncounterOption(index) != null, RewardOf(options[i]));
                if (a != null) return a;
            }
            return null;
        }

        private static string RewardOf(EncounterOption option)
        {
            foreach (EncounterEffect e in option.Effects)
            {
                switch (e.Kind)
                {
                    case EffectKind.Heal: return AutoplayTexts.RewardHealing;
                    case EffectKind.UpgradeRune: return AutoplayTexts.RewardRuneLevel;
                    case EffectKind.Shards: return AutoplayTexts.RewardShards;
                    case EffectKind.Gold: return AutoplayTexts.RewardGold;
                }
            }
            return null;
        }

        // ------------------------------------------------------------------ Angebote

        private BotAction Offer(OverworldSession s)
        {
            RuneOffer offer = s.PendingRuneOffer;
            if (offer == null) return null;

            bool hasTrigger = s.Modules.Owns(ModuleIds.Trigger);
            var candidates = new List<(int score, BotAction action)>();
            void Add(int score, BotAction a)
            {
                if (a != null) candidates.Add((score, a));
            }

            // Wertung: Verbesserung 30 > neue Karte 20 > Gold; innerhalb gleicher Stufe Modul > Skill > Teil > Rune.
            for (int i = 0; i < offer.ModuleIds.Count; i++)
            {
                int index = i;
                string id = offer.ModuleIds[i];
                if (!s.CanTakeModule(i)) continue;
                bool upgrade = s.ModuleUpgradeTarget(id) != null;
                int score = id == ModuleIds.Trigger && !hasTrigger ? 40 : upgrade ? 34 : 24;
                Add(score, Try(BotActionKind.Offer, AutoplayTexts.OfferModule(ModuleName(s, id)), () => s.TakeModule(index), AutoplayTexts.RewardModule));
            }
            for (int i = 0; i < offer.SkillIds.Count; i++)
            {
                int index = i;
                string id = offer.SkillIds[i];
                if (!s.CanTakeSkill(i)) continue;
                int score = s.IsImprovementSkill(id) ? 33 : 23;
                Add(score, Try(BotActionKind.Offer, AutoplayTexts.OfferSkill(SkillName(s, id)), () => s.TakeSkill(index), AutoplayTexts.RewardSkill));
            }
            for (int i = 0; i < offer.ItemIds.Count; i++)
            {
                int index = i;
                if (!s.Items.TryGet(offer.ItemIds[i], out EquipmentDefinition item) || !s.CanTakeItem(i)) continue;
                bool better = s.IsImprovement(item) || Score(s.StatsWith(item)) > Score(s.StatsNow()) + 0.5;
                int score = better ? 32 : 22;
                Add(score, Try(BotActionKind.Offer, AutoplayTexts.OfferItem(item.Name), () => s.TakeItem(index), AutoplayTexts.RewardItem));
            }
            for (int i = 0; i < offer.Options.Count; i++)
            {
                int index = i;
                RuneDefinition rune = offer.Options[i];
                bool room = !s.Board.IsFull || !s.RuneInventory.IsFull || s.OwnsRune(rune);
                if (!room) continue;
                int score = s.IsImprovement(rune) ? 31 : 21;
                Add(score, Try(BotActionKind.Offer, AutoplayTexts.OfferRune(rune.Name), () => s.TakeRune(index), AutoplayTexts.RewardRune));
            }
            if (offer.BoardExpansion)
                Add(35, Try(BotActionKind.Offer, AutoplayTexts.OfferBoardExpansion, s.TakeBoardExpansion, AutoplayTexts.RewardBoardExpansion));

            if (candidates.Count > 0) return candidates.OrderByDescending(c => c.score).First().action;
            return Try(BotActionKind.Offer, AutoplayTexts.OfferSkip, s.SkipRuneOffer, AutoplayTexts.RewardGold);
        }

        private static string ModuleName(OverworldSession s, string id) =>
            s.ModuleCatalog.TryGet(id, out ModuleDefinition d) ? d.Name : id;

        private static string SkillName(OverworldSession s, string id) =>
            s.SkillCatalog.TryGet(id, out SkillDefinition d) ? d.Name : id;

        // ------------------------------------------------------------------ Shop

        private BotAction Shop(OverworldSession s)
        {
            if (s.PendingShop == null) return null;
            var stock = s.PendingShop.Inventory;
            BotAction a = null;

            if (HpPercent(s) < 80 && s.CanBuyHeal) a = Try(BotActionKind.Shop, AutoplayTexts.ShopHeal, s.BuyHeal, AutoplayTexts.RewardHealing);

            for (int i = 0; a == null && i < stock.ModuleIds.Count; i++)
            {
                int index = i;
                if (s.CanBuyShopModule(i))
                    a = Try(BotActionKind.Shop, AutoplayTexts.ShopModule(ModuleName(s, stock.ModuleIds[i])), () => s.BuyShopModule(index), AutoplayTexts.RewardModule);
            }
            for (int i = 0; a == null && i < stock.SkillIds.Count; i++)
            {
                int index = i;
                string id = stock.SkillIds[i];
                if (s.CanBuyShopSkill(i) && (!s.OwnsSkill(id) || s.IsImprovementSkill(id)))
                    a = Try(BotActionKind.Shop, AutoplayTexts.ShopSkill(SkillName(s, id)), () => s.BuyShopSkill(index), AutoplayTexts.RewardSkill);
            }
            for (int i = 0; a == null && i < stock.ItemIds.Count; i++)
            {
                int index = i;
                if (!s.Items.TryGet(stock.ItemIds[i], out EquipmentDefinition item) || !s.CanBuyShopItem(i)) continue;
                if (s.IsImprovement(item) || Score(s.StatsWith(item)) > Score(s.StatsNow()) + 0.5)
                    a = Try(BotActionKind.Shop, AutoplayTexts.ShopItem(item.Name), () => s.BuyShopItem(index), AutoplayTexts.RewardItem);
            }
            if (a == null && s.CanBuyBoardExpansion) a = Try(BotActionKind.Shop, AutoplayTexts.ShopBoardRow, s.BuyBoardExpansion, AutoplayTexts.RewardBoardExpansion);
            for (int i = 0; a == null && i < stock.Runes.Count; i++)
            {
                int index = i;
                if (s.CanBuyShopRune(i) && !s.Board.IsFull)
                    a = Try(BotActionKind.Shop, AutoplayTexts.ShopRune(stock.Runes[i].Name), () => s.BuyShopRune(index), AutoplayTexts.RewardRune);
            }

            for (int i = 0; a == null && i < stock.ChipIds.Count; i++)
            {
                int index = i;
                if (s.CanBuyShopChip(i) && s.ChipCatalog.TryGet(stock.ChipIds[i], out ChipDefinition chip))
                    a = Try(BotActionKind.Shop, AutoplayTexts.ShopChip(chip.Name), () => s.BuyShopChip(index), AutoplayTexts.RewardChip);
            }

            return a ?? Try(BotActionKind.Shop, AutoplayTexts.ShopLeave, () =>
            {
                s.LeaveShop();
                return s.PendingShop == null;
            });
        }

        // ------------------------------------------------------------------ Portal

        private BotAction Portal(OverworldSession s) =>
            s.CanEnterPortal ? Try(BotActionKind.Portal, AutoplayTexts.EnterPortal(s.Act + 1), s.EnterPortal) : null;

        // ------------------------------------------------------------------ Tafel und Ausrüstung

        /// <summary>Einfache Wertung eines Builds für «besser oder nicht» (keine Spiellogik, nur Gewichtung der Anzeige-Werte).</summary>
        public static double Score(BuildStats b)
        {
            if (b == null) return 0;
            return b.MaxHp
                + b.WeaponDamage * b.AttacksPerSecond * 4.0
                + b.Armor * 2.0
                + (b.DodgeBp + b.BlockBp + b.CritBp + b.AccuracyBp + b.AreaDamageBp) / 200.0
                + b.Bonuses.Count * 3.0;
        }

        private BotAction Loadout(OverworldSession s)
        {
            // Bessere Teile aus dem Inventar anlegen.
            double now = Score(s.StatsNow());
            int bestCell = -1;
            double best = now + 0.5;
            for (int cell = 0; cell < s.Inventory.Capacity; cell++)
            {
                EquipmentDefinition item = s.Inventory[cell];
                if (item == null || !s.CanEquipFromInventory(cell)) continue;
                double score = Score(s.StatsWith(item));
                if (score > best)
                {
                    best = score;
                    bestCell = cell;
                }
            }
            if (bestCell >= 0)
            {
                int cell = bestCell;
                BotAction a = Try(BotActionKind.Inventory, AutoplayTexts.Equip(s.Inventory[cell].Name), () => s.EquipFromInventory(cell));
                if (a != null) return a;
            }

            // Runen aus dem Inventar als Relais auf freie Zellen (neben unversorgte Komponenten).
            if (!s.Board.IsFull)
            {
                for (int i = 0; i < s.RuneInventory.Count; i++)
                {
                    int index = i;
                    BotAction a = Try(BotActionKind.Build, AutoplayTexts.PlaceRune(s.RuneInventory[i].Name), () => s.EquipRuneFromInventory(index));
                    if (a != null) return a;
                }
            }

            // Unversorgte Komponenten an ein Relais legen, das sie versorgen kann.
            for (int i = 0; i < s.Board.Components.Count; i++)
            {
                ComponentSlot c = s.Board.Components[i];
                if (s.IsPowered(c)) continue;
                SkillInstance skill = c.Skill;
                if (skill == null || !SpotFor(s, skill, out Cell at, out bool turned, c)) continue;
                BotAction a = Try(BotActionKind.Build, AutoplayTexts.PlaceSkill(skill.NameFrom(s.SkillCatalog), at.X + 1, at.Y + 1),
                    () => s.PlaceSkill(skill.InstanceId, at, turned) && s.IsPowered(s.Board.ComponentOf(skill)));
                if (a != null) return a;
            }

            // Freie Skills (grösste zuerst) neben ein Relais, das sie versorgen kann, bevorzugt am Kern.
            foreach (SkillInstance skill in s.Skills.Free.OrderByDescending(k => s.Board.ShapeOfSkill(k.SkillId).Cells).ThenByDescending(k => k.Level))
            {
                if (!SpotFor(s, skill, out Cell at, out bool turned)) continue;
                BotAction a = Try(BotActionKind.Build, AutoplayTexts.PlaceSkill(skill.NameFrom(s.SkillCatalog), at.X + 1, at.Y + 1),
                    () => s.PlaceSkill(skill.InstanceId, at, turned));
                if (a != null) return a;
            }

            // Freie Module an den ersten passenden Ort (Skill einer Komponente, sonst Relais).
            foreach (ModuleInstance module in s.Modules.Free)
            {
                string name = module.NameFrom(s.ModuleCatalog);
                for (int i = 0; i < s.Board.Components.Count; i++)
                {
                    SkillInstance skill = s.Board.Components[i].Skill;
                    if (skill == null || !s.CanPlaceModule(module, skill)) continue;
                    BotAction a = Try(BotActionKind.Build, AutoplayTexts.PlaceModuleOnSkill(name, i + 1),
                        () => s.PlaceModuleOnSkill(module.InstanceId, skill.InstanceId));
                    if (a != null) return a;
                }
                for (int i = 0; i < s.Board.Relays.Count; i++)
                {
                    int r = i;
                    if (!s.CanPlaceModule(module, s.Board.Relays[i])) continue;
                    BotAction a = Try(BotActionKind.Build, AutoplayTexts.PlaceModuleOnRune(name, i + 1),
                        () => s.PlaceModuleOnRelay(module.InstanceId, r));
                    if (a != null) return a;
                }
            }

            // Chips aus dem Inventar dorthin, wo sie am meisten verbinden (A-20); sonst bleiben sie liegen.
            for (int i = 0; i < s.ChipInventory.Count && s.CanEditChips; i++)
            {
                if (!ChipSpot(s, s.ChipInventory[i], out Cell at, out int turns)) continue;
                int index = i;
                BotAction a = Try(BotActionKind.Build, AutoplayTexts.PlaceChip(s.ChipInventory[i].Name, at.X + 1, at.Y + 1),
                    () => s.PlaceChip(index, at, turns));
                if (a != null) return a;
            }

            // Gesetzte Auslöser ohne Ziel bekommen eines.
            foreach (ModuleInstance module in s.Modules.All)
            {
                if (module.ModuleId != ModuleIds.Trigger || module.IsFree || module.Target.HasValue) continue;
                BotAction a = Try(BotActionKind.Build, AutoplayTexts.SetTriggerTarget(module.InstanceId),
                    () => s.CycleTriggerTarget(module.InstanceId) && module.Target.HasValue, setsTrigger: true);
                if (a != null) return a;
            }

            return null;
        }

        /// <summary>
        /// Beste freie Zelle und Drehung für einen Chip: probiert jede Lage auf der Platine aus und nimmt die mit dem
        /// höchsten Wert (<see cref="ChipValue"/>), nur wenn sie besser ist als ohne den Chip. Erste in Lesereihenfolge gewinnt.
        /// </summary>
        private static bool ChipSpot(OverworldSession s, ChipDefinition chip, out Cell at, out int turns)
        {
            at = default;
            turns = 0;
            CircuitBoard board = s.Board;
            int best = ChipValue(s.CompileBoard());
            bool found = false;
            int turnCount = chip.Openings.Count == 0 || chip.Kind == ChipKind.TraceCross || chip.Kind == ChipKind.Capacitor ? 1 : 4;
            for (int y = 0; y < board.Height; y++)
                for (int x = 0; x < board.Width; x++)
                {
                    var cell = new Cell(x, y);
                    if (!board.IsFree(new CellRect(cell, Shape.One))) continue;
                    for (int turn = 0; turn < turnCount; turn++)
                    {
                        BoardChip trial = board.AddChip(chip, cell, turn);
                        if (trial == null) continue;
                        int value = ChipValue(s.CompileBoard());
                        board.RemoveChip(trial);
                        if (value <= best) continue;
                        best = value;
                        at = cell;
                        turns = turn;
                        found = true;
                    }
                }
            return found;
        }

        /// <summary>Wert der Verdrahtung: jede Pulsverbindung zählt 1, jede von einem bereiten Gatter versorgte Komponente 2.</summary>
        private static int ChipValue(LogicBoard board)
        {
            int value = board.Links.Count;
            foreach (LogicRelay relay in board.Relays)
                if (relay.Gate != null && relay.IsGateReady) value += 2 * relay.Powered.Count;
            return value;
        }

        /// <summary>
        /// Freie Lage für ein Exemplar neben einem Relais, das gross genug ist: bevorzugt eine, die den Kern berührt, sonst
        /// die erste in Lesereihenfolge. <paramref name="ignore"/> ist die Komponente selbst, wenn sie umziehen soll.
        /// </summary>
        private static bool SpotFor(OverworldSession s, SkillInstance skill, out Cell at, out bool turned, ComponentSlot ignore = null)
        {
            at = default;
            turned = false;
            CircuitBoard board = s.Board;
            Shape shape = board.ShapeOfSkill(skill.SkillId);
            bool found = false;
            bool foundCore = false;
            foreach (RelayChip relay in board.Relays)
            {
                if (s.RelayMaxCells(relay) < shape.Cells) continue;
                foreach (bool turn in shape.IsSquare ? new[] { false } : new[] { false, true })
                {
                    Shape sh = shape.Turned(turn);
                    for (int y = 0; y < board.Height; y++)
                        for (int x = 0; x < board.Width; x++)
                        {
                            var rect = new CellRect(new Cell(x, y), sh);
                            if (!rect.Touches(relay.Rect) || !board.IsFree(rect, ignore)) continue;
                            bool core = rect.Touches(board.CoreRect);
                            if (found && (foundCore || !core)) continue;
                            at = rect.Origin;
                            turned = turn;
                            found = true;
                            foundCore = core;
                        }
                }
            }
            return found;
        }

        // ------------------------------------------------------------------ Bewegung

        private BotAction Move(OverworldSession s)
        {
            HexCoord here = s.Player.Position;
            bool avoidFights = HpPercent(s) < AvoidFightPercent;

            // 1. Überfallene Minen verteidigen, wenn sie rechtzeitig erreichbar sind.
            foreach (MineRaid raid in s.Raids.Where(r => !r.IsLost).OrderBy(r => r.Coord.DistanceTo(here)))
            {
                if (avoidFights) break;
                BotAction a = Toward(s, raid.Coord, AutoplayTexts.WhyDefendMine, raid.TurnsLeft(s.Turns.CurrentTurn) + 1);
                if (a != null) return a;
            }

            // 2. Bekannte, freie Minen in der Nähe erobern.
            foreach (HexCell mine in KnownCells(s, CellContent.GoldMine).Where(c => !c.IsResolved && c.Coord.DistanceTo(here) <= MineDetour))
            {
                BotAction a = Toward(s, mine.Coord, AutoplayTexts.WhyCaptureMine);
                if (a != null) return a;
            }

            // 3. Mit Gold zu einem bekannten, noch nicht besuchten Shop.
            if (s.Stats.Gold >= ShopGold || HpPercent(s) < LowHpPercent && s.Stats.Gold >= s.ShopPrices.Heal)
            {
                foreach (HexCell shop in KnownCells(s, CellContent.Shop).Where(c => c.VisitCount == 0).OrderBy(c => c.Coord.DistanceTo(here)))
                {
                    BotAction a = Toward(s, shop.Coord, AutoplayTexts.WhyToShop);
                    if (a != null) return a;
                }
            }

            // 4. Unbekannte Nachbarfelder; Gegner bei wenig HP meiden.
            List<HexCell> steps = s.Map.GetNeighbors(here).Where(c => s.CanStepTo(c.Coord)).ToList();
            if (steps.Count == 0) return null;
            List<HexCell> fresh = steps.Where(c => c.VisitCount == 0).ToList();
            if (avoidFights)
            {
                List<HexCell> safe = fresh.Where(c => !IsKnownHostile(c)).ToList();
                if (safe.Count > 0 || fresh.Count > 0 && steps.Any(c => !IsKnownHostile(c))) fresh = safe;
            }
            if (fresh.Count > 0)
            {
                int top = fresh.Max(c => StepValue(c));
                List<HexCell> pool = fresh.Where(c => StepValue(c) == top).ToList();
                HexCell pick = pool[_random.Next(pool.Count)];
                BotAction a = StepTo(s, pick.Coord, AutoplayTexts.WhyExplore);
                if (a != null) return a;
            }

            // 5. Zum nächsten unbesuchten, erreichbaren Feld.
            foreach (HexCell target in s.Map.Cells
                .Where(c => c.VisitCount == 0 && c.IsWalkable && c.Visibility != CellVisibility.Hidden && !(avoidFights && IsKnownHostile(c)))
                .OrderBy(c => c.Coord.DistanceTo(here)).Take(12))
            {
                BotAction a = Toward(s, target.Coord, AutoplayTexts.WhyNextUnknown);
                if (a != null) return a;
            }

            // 6. Alles erkundet: umherlaufen, bis der Boss kommt.
            List<HexCell> wander = avoidFights ? steps.Where(c => !IsKnownHostile(c)).ToList() : steps;
            if (wander.Count == 0) wander = steps;
            HexCell any = wander[_random.Next(wander.Count)];
            return StepTo(s, any.Coord, AutoplayTexts.WhyWaitForBoss) ?? StepTo(s, steps[0].Coord, AutoplayTexts.WhyAnywhere);
        }

        private static IEnumerable<HexCell> KnownCells(OverworldSession s, CellContent content) =>
            s.Map.Cells.Where(c => c.IsContentKnown && c.Content == content && c.IsWalkable);

        private static bool IsKnownHostile(HexCell c) => c.IsContentKnown && c.HasPendingEvent && c.Content.IsHostile();

        /// <summary>Lieber Felder mit bekanntem Inhalt (Truhe, Event, Mine) als leere, sonst Unbekanntes.</summary>
        private static int StepValue(HexCell c)
        {
            if (!c.IsContentKnown) return 2;
            if (!c.HasPendingEvent) return 1;
            switch (c.Content)
            {
                case CellContent.Treasure:
                case CellContent.GoldMine:
                case CellContent.Shop:
                    return 4;
                default:
                    return 3;
            }
        }

        private BotAction Toward(OverworldSession s, HexCoord target, string why, int maxLength = int.MaxValue)
        {
            if (target == s.Player.Position) return null;
            List<HexCoord> route = s.PlanRoute(target);
            if (route == null || route.Count == 0 || route.Count > maxLength) return null;
            if (route.Count == 1) return StepTo(s, route[0], why);
            return TravelTo(s, route, why);
        }

        /// <summary>Reist wie der Spieler die ganze Route in einem Zug, bis ein Feld die Reise anhält.</summary>
        private BotAction TravelTo(OverworldSession s, List<HexCoord> route, string why)
        {
            HexCoord last = route[route.Count - 1];
            if (!s.CanStepTo(route[0])) return null;
            return Try(BotActionKind.Move, AutoplayTexts.Travel(last.Q, last.R, route.Count, why), () =>
            {
                bool moved = false;
                for (int i = 0; i < route.Count; i++)
                {
                    if (!s.CanStepTo(route[i])) break;
                    StepResult result = s.TryTravelStep(route[i], i == route.Count - 1);
                    moved |= result.Success;
                    if (result.InterruptsTravel || s.IsBusy || s.IsGameOver) break;
                }
                s.FinishTravel();
                return moved;
            }, step: route[0]);
        }

        private BotAction StepTo(OverworldSession s, HexCoord step, string why)
        {
            if (!s.CanStepTo(step)) return null;
            return Try(BotActionKind.Move, AutoplayTexts.Step(step.Q, step.R, why), () => s.TryStep(step).Success, step: step);
        }
    }
}
