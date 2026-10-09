namespace Betaknight.Core.Autoplay
{
    /// <summary>
    /// Alle Texte des Testspielers (Autoplay) an einem Ort: Endgründe, Bot-Aktionen, Belohnungsarten,
    /// Zusammenfassungen, Log-Zeilen und Statuszeile. JSON-Feldnamen stehen nicht hier, sie bleiben im Bericht.
    /// </summary>
    public static class AutoplayTexts
    {
        // ------------------------------------------------------------------ Endgründe (werden in Tests verglichen)

        public const string EndGameOver = "Game Over";
        public const string EndTurnLimit = "Turn limit";
        public const string EndTooManyExceptions = "Too many exceptions";
        public const string EndStartException = "Exception on start";
        public const string EndHang = "Hang";

        /// <summary>«Act 2 reached».</summary>
        public static string EndActReached(int act) => $"Act {act} reached";

        // ------------------------------------------------------------------ Belohnungsarten (Schlüssel im Bericht)

        public const string RewardHealing = "Healing";
        public const string RewardRuneLevel = "Rune Level";
        public const string RewardShards = "Rune Shards";
        public const string RewardGold = "Gold";
        public const string RewardModule = "Module";
        public const string RewardSkill = "Skill";
        public const string RewardItem = "Item";
        public const string RewardRune = "Rune";
        public const string RewardBoardExpansion = "Board Expansion";
        public const string RewardChip = "Chip";

        // ------------------------------------------------------------------ Bot: keine Aktion

        public const string NoSession = "No session";
        public const string SessionBusy = "Session is waiting for an unknown decision";
        public const string NoWalkableTile = "No walkable tile";
        public const string NoKit = "none";

        // ------------------------------------------------------------------ Bot-Aktionen

        public static string DiscardItem(string name, bool retry) => $"Inventory full: discard {name}{(retry ? " (2)" : string.Empty)}";
        public static string DiscardRune(string name, bool retry) => $"Rune inventory full: discard {name}{(retry ? " (2)" : string.Empty)}";
        public static string EncounterChoice(string title, string option) => $"{title}: {option}";

        public static string OfferModule(string name) => $"Offer: Module {name}";
        public static string OfferSkill(string name) => $"Offer: Skill {name}";
        public static string OfferItem(string name) => $"Offer: Item {name}";
        public static string OfferRune(string name) => $"Offer: Rune {name}";
        public const string OfferBoardExpansion = "Offer: Board Expansion";
        public const string OfferSkip = "Offer: skip (Gold)";
        public static string Salvage(string kind, string name) => $"Salvage: {kind} {name}";
        public const string SalvageSkip = "Salvage: leave the rest";

        public const string ShopHeal = "Shop: heal";
        public static string ShopModule(string name) => $"Shop: Module {name}";
        public static string ShopSkill(string name) => $"Shop: Skill {name}";
        public static string ShopItem(string name) => $"Shop: Item {name}";
        public const string ShopBoardRow = "Shop: Board Expansion";
        public static string ShopRune(string name) => $"Shop: Rune {name}";
        public static string ShopChip(string name) => $"Shop: Chip {name}";
        public const string ShopLeave = "Leave shop";

        public static string EnterPortal(int act) => $"Through the portal to Act {act}";

        public static string Equip(string name) => $"Equip: {name}";
        public static string PlaceRune(string name) => $"Place rune: {name}";
        public static string PlaceSkill(string name, int column, int row) => $"Place component: {name} → column {column}, row {row}";
        public static string ChipRow(string name, int column, int row) => $"Chip {name} (column {column}, row {row})";
        public static string PlaceChip(string name, int column, int row) => $"Place chip: {name} → column {column}, row {row}";
        public static string PlaceModuleOnSkill(string name, int component) => $"Place module: {name} → component #{component}";
        public static string PlaceModuleOnRune(string name, int relay) => $"Place module: {name} → Relay {relay}";
        public static string SetTriggerTarget(int instanceId) => $"Set trigger target: Module #{instanceId}";

        public const string WhyDefendMine = "defend mine";
        public const string WhyCaptureMine = "capture mine";
        public const string WhyToShop = "to the shop";
        public const string WhyExplore = "explore";
        public const string WhyNextUnknown = "to the nearest unknown tile";
        public const string WhyWaitForBoss = "wait for the boss";
        public const string WhyAnywhere = "anywhere";

        public static string Travel(int q, int r, int tiles, string why) => $"Travel to ({q},{r}), {tiles} tiles: {why}";
        public static string Step(int q, int r, string why) => $"Step to ({q},{r}): {why}";

        // ------------------------------------------------------------------ Optionen

        public static string ExpectsPositiveNumber(string option) => $"{option} expects a number > 0";
        public static string ExpectsPath(string option) => $"{option} expects a path";
        public static string ExpectsInteger(string option) => $"{option} expects a whole number";

        // ------------------------------------------------------------------ Recorder / Headless

        public const string EmptyRow = "empty";
        public static string DecisionException(string error) => $"Decision: {error}";
        public const string DecisionThrowsRepeatedly = "Decision throws repeatedly";
        public static string ActionsWithoutNewTurn(int actions, string last) => $"{actions} actions without a new turn, last \"{last}\"";
        public static string HangAt(int act, int turn, string what) => $"Act {act}, Turn {turn}: {what}";

        // ------------------------------------------------------------------ Bericht

        public const string Ok = "OK";
        public const string Failed = "FAILED";

        public static string RunSummary(int seed, string kit, string endReason, int act, int turns, int fightsWon, int fightsLost,
            int elitesWon, int elitesLost, int bossesSurvived, int bosses, int boardRows, int modules, int triggers, int duos,
            string basicShare, string basicShareFromAct2, string duration, string fps, int exceptions, int errorLogs, int hangs, bool ok) =>
            $"Seed {seed}, {kit}: {endReason} in Act {act} after {turns} turns. Fights {fightsWon}:{fightsLost}, " +
            $"Elite {elitesWon}:{elitesLost}, Boss {bossesSurvived}/{bosses}, Board {boardRows} parts, " +
            $"Modules {modules}, Triggers {triggers}, Duos {duos}, Basic Attack {basicShare} " +
            $"(from Act 2: {basicShareFromAct2}), {duration} s{fps}. " +
            $"Exceptions {exceptions}, Error logs {errorLogs}, Hangs {hangs} → {(ok ? Ok : Failed)}";

        public static string Fps(string average, string min) => $", FPS avg {average} / min {min}";

        public static string TotalSummary(int runs, int runsOk, int maxAct, int fightsWon, int fightsLost, int exceptions,
            int errorLogs, int hangs, int exitCode) =>
            $"Autoplay: {runs} runs, {runsOk} OK, highest Act {maxAct}, Fights {fightsWon}:{fightsLost}, " +
            $"Exceptions {exceptions}, Error logs {errorLogs}, Hangs {hangs}, Exit code {exitCode}";

        public const string RuneTableHeader = "Runes in bot fights (basis for difficulty):";

        public static string RuneTableLine(string symbol, string name, string runeId, int fights, string metPercent, int met,
            int fired, string firedPerMinute) =>
            $"{symbol,-4} {name} [{runeId}]: {fights} fights, met in {metPercent} % ({met}×), fired {fired}× = {firedPerMinute}/min";

        // ------------------------------------------------------------------ Runner (Log und Statuszeile)

        public const string LogPrefix = "[Autoplay] ";
        public const string RandomSeed = "random";

        public static string LogStart(int runs, string seed, string speed, int targetAct, bool quit) =>
            $"Start: {runs} run(s), Seed {seed}, speed {speed}×, until Act {targetAct}{(quit ? ", then quit" : string.Empty)}.";

        public static string LogRun(int run, int runs, int seed) => $"Run {run}/{runs}, Seed {seed}";
        public static string LogKit(string kit) => $"Kit: {kit}";
        public static string LogReport(string path) => $"Report: {path}";
        public static string LogReportFailed(string path, string error) => $"Could not write report ({path}): {error}";
        public static string LogQuit(int exitCode) => $"Quitting with exit code {exitCode}";
        public static string LogHang(string text) => $"Hang: {text}";

        public const string ProgressRunStarts = "Run starting";
        public static string ProgressKit(string kit) => $"Kit {kit}";
        public static string ProgressAct(int act) => $"Act {act}";
        public const string ProgressArenaContinue = "Arena: Continue";
        public const string ProgressHangRecovery = "Recovery attempt after hang";

        public const string NoSessionWhere = "no session";
        public static string WhereActTurn(int act, int turn) => $"Act {act}, Turn {turn}";
        public static string NoActionFor(string where, float seconds, string last) => $"{where}: no action for {seconds:0} s, last \"{last}\"";

        public static string StatusDone(string summary, string path) => $"<b>Autoplay done</b>\n{summary}\nReport: {path}";
        public static string StatusRunEnded(int run, int runs, string reason) => $"<b>Autoplay</b>  Run {run}/{runs} ended: {reason}";

        public static string StatusRunning(int run, int runs, int seed, string kit, string speed, string lastAction) =>
            $"<b>Autoplay</b>  Run {run}/{runs}, Seed {seed}, {kit}, speed {speed}×\n{lastAction}";
    }
}
