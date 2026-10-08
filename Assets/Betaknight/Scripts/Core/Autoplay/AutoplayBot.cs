using System;
using System.Collections.Generic;
using System.Linq;
using Betaknight.Core.Arena;
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
            if (s == null) return BotAction.Nothing("keine Session");
            if (s.IsGameOver) return BotAction.Nothing("Game Over");
            SyncTurn(s);

            BotAction action = Overflow(s) ?? Encounter(s) ?? Offer(s) ?? Shop(s) ?? Portal(s);
            if (action != null) return action;
            if (s.IsBusy) return BotAction.Nothing("Session wartet auf eine unbekannte Entscheidung");

            if (s.CanChangeLoadout && _loadoutActions < MaxLoadoutActionsPerTurn)
            {
                action = Loadout(s);
                if (action != null)
                {
                    _loadoutActions++;
                    return action;
                }
            }

            return Move(s) ?? BotAction.Nothing("kein begehbares Feld");
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
                return Try(BotActionKind.Overflow, $"Inventar voll: {s.PendingItem.Name} liegen lassen", s.RejectPendingItem)
                    ?? Try(BotActionKind.Overflow, $"Inventar voll: {s.PendingItem.Name} liegen lassen (2)", s.RejectPendingItem);
            if (s.PendingRune != null)
                return Try(BotActionKind.Overflow, $"Runen-Inventar voll: {s.PendingRune.Name} liegen lassen", s.RejectPendingRune)
                    ?? Try(BotActionKind.Overflow, $"Runen-Inventar voll: {s.PendingRune.Name} liegen lassen (2)", s.RejectPendingRune);
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
                bool canUpgrade = s.Runes.BestUpgradeTarget() >= 0;
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
                BotAction a = Try(BotActionKind.Encounter, $"{prompt.Definition.Title}: {options[i].Text}",
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
                    case EffectKind.Heal: return "Heilung";
                    case EffectKind.UpgradeRune: return "Runen-Stufe";
                    case EffectKind.Shards: return "Runensplitter";
                    case EffectKind.Gold: return "Gold";
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
                Add(score, Try(BotActionKind.Offer, $"Angebot: Modul {ModuleName(s, id)}", () => s.TakeModule(index), "Modul"));
            }
            for (int i = 0; i < offer.SkillIds.Count; i++)
            {
                int index = i;
                string id = offer.SkillIds[i];
                if (!s.CanTakeSkill(i)) continue;
                int score = s.IsImprovementSkill(id) ? 33 : 23;
                Add(score, Try(BotActionKind.Offer, $"Angebot: Skill {SkillName(s, id)}", () => s.TakeSkill(index), "Skill"));
            }
            for (int i = 0; i < offer.ItemIds.Count; i++)
            {
                int index = i;
                if (!s.Items.TryGet(offer.ItemIds[i], out EquipmentDefinition item) || !s.CanTakeItem(i)) continue;
                bool better = s.IsImprovement(item) || Score(s.StatsWith(item)) > Score(s.StatsNow()) + 0.5;
                int score = better ? 32 : 22;
                Add(score, Try(BotActionKind.Offer, $"Angebot: Teil {item.Name}", () => s.TakeItem(index), "Teil"));
            }
            for (int i = 0; i < offer.Options.Count; i++)
            {
                int index = i;
                RuneDefinition rune = offer.Options[i];
                bool room = !s.Runes.IsFull || !s.RuneInventory.IsFull || s.OwnsRune(rune);
                if (!room) continue;
                int score = s.IsImprovement(rune) ? 31 : 21;
                Add(score, Try(BotActionKind.Offer, $"Angebot: Rune {rune.Name}", () => s.TakeRune(index), "Rune"));
            }
            if (offer.BoardExpansion)
                Add(35, Try(BotActionKind.Offer, "Angebot: Tafel-Erweiterung", s.TakeBoardExpansion, "Tafel-Erweiterung"));

            if (candidates.Count > 0) return candidates.OrderByDescending(c => c.score).First().action;
            return Try(BotActionKind.Offer, "Angebot: verzichten (Gold)", s.SkipRuneOffer, "Gold");
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

            if (HpPercent(s) < 80 && s.CanBuyHeal) a = Try(BotActionKind.Shop, "Shop: heilen", s.BuyHeal, "Heilung");

            for (int i = 0; a == null && i < stock.ModuleIds.Count; i++)
            {
                int index = i;
                if (s.CanBuyShopModule(i))
                    a = Try(BotActionKind.Shop, $"Shop: Modul {ModuleName(s, stock.ModuleIds[i])}", () => s.BuyShopModule(index), "Modul");
            }
            for (int i = 0; a == null && i < stock.SkillIds.Count; i++)
            {
                int index = i;
                string id = stock.SkillIds[i];
                if (s.CanBuyShopSkill(i) && (!s.OwnsSkill(id) || s.IsImprovementSkill(id)))
                    a = Try(BotActionKind.Shop, $"Shop: Skill {SkillName(s, id)}", () => s.BuyShopSkill(index), "Skill");
            }
            for (int i = 0; a == null && i < stock.ItemIds.Count; i++)
            {
                int index = i;
                if (!s.Items.TryGet(stock.ItemIds[i], out EquipmentDefinition item) || !s.CanBuyShopItem(i)) continue;
                if (s.IsImprovement(item) || Score(s.StatsWith(item)) > Score(s.StatsNow()) + 0.5)
                    a = Try(BotActionKind.Shop, $"Shop: Teil {item.Name}", () => s.BuyShopItem(index), "Teil");
            }
            if (a == null && s.CanBuyRuneSlot) a = Try(BotActionKind.Shop, "Shop: Tafel-Zeile", s.BuyRuneSlot, "Tafel-Erweiterung");
            for (int i = 0; a == null && i < stock.Runes.Count; i++)
            {
                int index = i;
                if (s.CanBuyShopRune(i) && !s.Runes.IsFull)
                    a = Try(BotActionKind.Shop, $"Shop: Rune {stock.Runes[i].Name}", () => s.BuyShopRune(index), "Rune");
            }

            return a ?? Try(BotActionKind.Shop, "Shop verlassen", () =>
            {
                s.LeaveShop();
                return s.PendingShop == null;
            });
        }

        // ------------------------------------------------------------------ Portal

        private BotAction Portal(OverworldSession s) =>
            s.CanEnterPortal ? Try(BotActionKind.Portal, $"Durchs Portal zu Akt {s.Act + 1}", s.EnterPortal) : null;

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
                BotAction a = Try(BotActionKind.Inventory, $"Anlegen: {s.Inventory[cell].Name}", () => s.EquipFromInventory(cell));
                if (a != null) return a;
            }

            // Runen aus dem Inventar in freie Zeilen.
            if (!s.Runes.IsFull)
            {
                for (int i = 0; i < s.RuneInventory.Count; i++)
                {
                    int index = i;
                    BotAction a = Try(BotActionKind.Build, $"Rune einsetzen: {s.RuneInventory[i].Name}", () => s.EquipRuneFromInventory(index));
                    if (a != null) return a;
                }
            }

            // Freie Skills in Zeilen ohne Skill oder mit Basisangriff.
            List<SkillInstance> free = s.Skills.Free.OrderByDescending(k => k.Level).ToList();
            if (free.Count > 0)
            {
                for (int row = 0; row < s.Runes.Rows.Count; row++)
                {
                    SkillInstance current = s.Runes.Rows[row].Skill;
                    if (current != null && !current.IsBasicAttack) continue;
                    SkillInstance skill = free[0];
                    int r = row;
                    BotAction a = Try(BotActionKind.Build, $"Skill setzen: {skill.NameFrom(s.SkillCatalog)} → Zeile {row + 1}",
                        () => s.PlaceSkill(skill.InstanceId, r));
                    if (a != null) return a;
                }
            }

            // Freie Module an den ersten passenden Ort (Skill der Zeile, sonst Baustein).
            foreach (ModuleInstance module in s.Modules.Free)
            {
                for (int row = 0; row < s.Runes.Rows.Count; row++)
                {
                    RuneSlot slot = s.Runes.Rows[row];
                    int r = row;
                    string name = module.NameFrom(s.ModuleCatalog);
                    if (slot.Skill != null && !slot.Skill.IsBasicAttack && s.CanPlaceModule(module, slot.Skill))
                    {
                        SkillInstance skill = slot.Skill;
                        BotAction a = Try(BotActionKind.Build, $"Modul setzen: {name} → Skill Zeile {row + 1}",
                            () => s.PlaceModuleOnSkill(module.InstanceId, skill.InstanceId));
                        if (a != null) return a;
                    }
                    if (s.CanPlaceModule(module, slot))
                    {
                        BotAction a = Try(BotActionKind.Build, $"Modul setzen: {name} → Baustein Zeile {row + 1}",
                            () => s.PlaceModuleOnRow(module.InstanceId, r));
                        if (a != null) return a;
                    }
                }
            }

            // Gesetzte Auslöser ohne Ziel bekommen eines.
            foreach (ModuleInstance module in s.Modules.All)
            {
                if (module.ModuleId != ModuleIds.Trigger || module.IsFree || module.Target.HasValue) continue;
                BotAction a = Try(BotActionKind.Build, $"Auslöser-Ziel setzen: Modul #{module.InstanceId}",
                    () => s.CycleTriggerTarget(module.InstanceId) && module.Target.HasValue, setsTrigger: true);
                if (a != null) return a;
            }

            return null;
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
                BotAction a = Toward(s, raid.Coord, "Mine verteidigen", raid.TurnsLeft(s.Turns.CurrentTurn) + 1);
                if (a != null) return a;
            }

            // 2. Bekannte, freie Minen in der Nähe erobern.
            foreach (HexCell mine in KnownCells(s, CellContent.GoldMine).Where(c => !c.IsResolved && c.Coord.DistanceTo(here) <= MineDetour))
            {
                BotAction a = Toward(s, mine.Coord, "Mine erobern");
                if (a != null) return a;
            }

            // 3. Mit Gold zu einem bekannten, noch nicht besuchten Shop.
            if (s.Stats.Gold >= ShopGold || HpPercent(s) < LowHpPercent && s.Stats.Gold >= s.ShopPrices.Heal)
            {
                foreach (HexCell shop in KnownCells(s, CellContent.Shop).Where(c => c.VisitCount == 0).OrderBy(c => c.Coord.DistanceTo(here)))
                {
                    BotAction a = Toward(s, shop.Coord, "zum Shop");
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
                BotAction a = StepTo(s, pick.Coord, "erkunden");
                if (a != null) return a;
            }

            // 5. Zum nächsten unbesuchten, erreichbaren Feld.
            foreach (HexCell target in s.Map.Cells
                .Where(c => c.VisitCount == 0 && c.IsWalkable && c.Visibility != CellVisibility.Hidden && !(avoidFights && IsKnownHostile(c)))
                .OrderBy(c => c.Coord.DistanceTo(here)).Take(12))
            {
                BotAction a = Toward(s, target.Coord, "zum nächsten unbekannten Feld");
                if (a != null) return a;
            }

            // 6. Alles erkundet: umherlaufen, bis der Boss kommt.
            List<HexCell> wander = avoidFights ? steps.Where(c => !IsKnownHostile(c)).ToList() : steps;
            if (wander.Count == 0) wander = steps;
            HexCell any = wander[_random.Next(wander.Count)];
            return StepTo(s, any.Coord, "warten auf den Boss") ?? StepTo(s, steps[0].Coord, "irgendwohin");
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
            return Try(BotActionKind.Move, $"Reise nach ({last.Q},{last.R}), {route.Count} Felder: {why}", () =>
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
            return Try(BotActionKind.Move, $"Schritt nach ({step.Q},{step.R}): {why}", () => s.TryStep(step).Success, step: step);
        }
    }
}
