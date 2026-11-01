using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Overlink.Cards;
using Overlink.Core;
using UnityEngine;

namespace Overlink.Match
{
    /// <summary>
    /// Ties <see cref="HandController"/>, <see cref="MatchEngine"/> and <see cref="SimpleAI"/> together and
    /// runs the local single-player slice.
    /// <para>
    /// Ownership rules: the Core <see cref="MatchState"/> is authoritative for hand contents, NP, slots and
    /// legality. The hand is a pure view of it, resynced with <see cref="SyncHand"/>, and a drag in the hand
    /// only *requests* a play through <see cref="TryPlayFromUI"/> — never a state mutation by itself.
    /// </para>
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class GameCoordinator : MonoBehaviour
    {
        [Header("References")]
        public HandController handController;

        [Header("Match Setup")]
        public bool enableAI = true;
        public int aiSeed = 42;

        [Tooltip("Seed for the match's own randomness: both deck shuffles, the opening lineup offers and the " +
                 "coin flip all draw from this one die, so a run is reproducible only when it is set. Zero " +
                 "takes a fresh seed every play. It is separate from aiSeed on purpose - that one is the " +
                 "opponent's decision noise, this one is the table's.")]
        public int matchSeed;

        [Tooltip("How long the opponent holds its Combat step so the player can commit blockers, in seconds. " +
                 "Zero skips the pause entirely and the opponent attacks in one frame, which is how the slice " +
                 "behaved before blocking was clickable.")]
        public float blockWindowSeconds = 4f;

        [Header("Keyboard controls")]
        [Tooltip("1-5: attack from that battlefield's Active unit (1 = Core, 2-5 = Node 1-4). P: pass phase. E: end turn. L: log state. Q: concede.")]
        public KeyCode attackCoreKey = KeyCode.Alpha1;
        public KeyCode attackNode1Key = KeyCode.Alpha2;
        public KeyCode attackNode2Key = KeyCode.Alpha3;
        public KeyCode attackNode3Key = KeyCode.Alpha4;
        public KeyCode attackNode4Key = KeyCode.Alpha5;
        public KeyCode passPhaseKey = KeyCode.P;
        public KeyCode endTurnKey = KeyCode.E;
        public KeyCode logStateKey = KeyCode.L;
        public KeyCode concedeKey = KeyCode.Q;

        private MatchState matchState;
        private MatchEngine matchEngine;
        private SimpleAI aiOpponent;
        private bool matchStarted;
        private bool aiBusy;
        private bool aiBlocksDeclared;
        private bool initializing;
        private BattlefieldPresenter presenter;
        private BattlefieldHud battlefieldHud;

        [Header("Phase flow")]
        [Tooltip("Upkeep, Draw, End and Cleanup advance automatically. Main and Combat remain player-controlled.")]
        public bool autoAdvanceNonInteractivePhases = true;

        // Serialized on purpose: unlike the fields above it survives a domain reload, which is how we
        // tell "the match has not booted yet" apart from "the match booted but its state was wiped".
        [SerializeField] private bool matchBooted;

        // CardData identity must stay stable across syncs or HandController.SetHand rebuilds every view.
        private readonly Dictionary<int, CardData> dataByInstance = new Dictionary<int, CardData>();
        private readonly Dictionary<CardData, int> instanceByData = new Dictionary<CardData, int>();

        void Awake()
        {
            if (presenter == null)
            {
                presenter = GetComponent<BattlefieldPresenter>();
            }
            WireHand();
            WireHud();
        }

        /// <summary>
        /// Resolves the HUD and subscribes to every button on its rail. Idempotent for the same reason
        /// <see cref="WireHand"/> is: the events are not serializable, so a domain reload while the game is
        /// running silently drops them and the rail keeps drawing buttons that fire nothing. Called again from
        /// <see cref="Update"/>'s recovery branch, which is where a reload is noticed.
        /// </summary>
        void WireHud()
        {
            battlefieldHud = BattlefieldHud.Ensure();
            if (battlefieldHud != null)
            {
                battlefieldHud.EndPhaseRequested -= EndPlayerPhase;
                battlefieldHud.EndPhaseRequested += EndPlayerPhase;
                battlefieldHud.RerollLineupRequested -= RerollLineup;
                battlefieldHud.RerollLineupRequested += RerollLineup;
                battlefieldHud.ConfirmLineupRequested -= SubmitLineup;
                battlefieldHud.ConfirmLineupRequested += SubmitLineup;
                battlefieldHud.RailPressed -= OnRailPressed;
                battlefieldHud.RailPressed += OnRailPressed;
            }
        }

        /// <summary>
        /// Binds the hand view to this match. Idempotent on purpose: <c>playResolver</c> and the
        /// <c>CardPlayed</c> event are not serializable, so a domain reload while playing (an editor
        /// script hot-reload) silently drops them and the hand would fall back to its standalone
        /// behaviour with no match validation at all.
        /// </summary>
        void WireHand()
        {
            if (handController == null)
            {
                handController = FindObjectOfType<HandController>();
            }
            if (presenter == null)
            {
                presenter = GetComponent<BattlefieldPresenter>();
                if (presenter == null)
                {
                    presenter = FindObjectOfType<BattlefieldPresenter>();
                }
            }

            if (handController == null)
            {
                return;
            }

            // The match owns the hand: no self-deal from DeckController, no debug draw/discard keys.
            handController.autoDraw = false;
            handController.playResolver = TryPlayFromUI;
            handController.playResolverWithSlot = TryPlayFromUISlot;
            handController.dropTargetResolver = ResolveDropTarget;
            handController.nominateResolver = NominateLineupCard;
            handController.dropTargetCleared = ClearDropTarget;
            handController.pointerOverControls = position =>
                (battlefieldHud != null && battlefieldHud.IsPointerOverControls(position))
                || (presenter != null && presenter.IsPointerOverInspect(position));
            handController.CardPlayed -= OnCardPlayedAnimated;
            handController.CardPlayed += OnCardPlayedAnimated;
            handController.CardNominated -= OnCardNominated;
            handController.CardNominated += OnCardNominated;
        }

        void OnDestroy()
        {
            if (battlefieldHud != null)
            {
                battlefieldHud.EndPhaseRequested -= EndPlayerPhase;
                battlefieldHud.RerollLineupRequested -= RerollLineup;
                battlefieldHud.ConfirmLineupRequested -= SubmitLineup;
                battlefieldHud.RailPressed -= OnRailPressed;
            }
            if (handController != null)
            {
                handController.CardPlayed -= OnCardPlayedAnimated;
                handController.CardNominated -= OnCardNominated;
            }
        }

        void Start()
        {
            StartCoroutine(InitializeMatch());
        }

        IEnumerator InitializeMatch()
        {
            initializing = true;
            Debug.Log("[GameCoordinator] Initializing match...");

            // Wait for scene to settle
            yield return new WaitForSeconds(0.5f);

            List<CardDefinition> pool = PrototypeCardPool.All();
            List<CardDefinition> playerDeck = PrototypeCardPool.BuildDeck();
            List<CardDefinition> opponentDeck = PrototypeCardPool.BuildDeck();

            // Surface pool problems in the console instead of failing silently at play time.
            ValidationResult poolValidation = CardValidator.ValidatePool(pool);
            foreach (ValidationIssue issue in poolValidation.Issues)
            {
                if (issue.Severity == Severity.Error)
                {
                    Debug.LogError($"[GameCoordinator] Prototype pool invalid: {issue}");
                }
                else if (issue.Severity == Severity.Warning)
                {
                    Debug.LogWarning($"[GameCoordinator] Prototype pool: {issue}");
                }
            }

            // The match opens on a lineup rather than on whoever drew a Unit first: both seats are shown five
            // Units costing 3 or less and field which of them stands on which Active socket. Nothing is playable
            // and no Upkeep runs until setup closes, which it does on the second confirmation - so this
            // coroutine's job between here and the wait is to take the toss, let the opponent field, and hold
            // the board still until the player answers.
            matchState = matchSeed > 0 ? new MatchState(matchSeed) : new MatchState();
            matchState.InitializeSetup(playerDeck, opponentDeck);

            // The boot flag is set before the wait, not after it: a script recompile mid-setup wipes the state
            // and kills this coroutine, and Update's restart guard can only fire if it can tell "the match has
            // not booted" from "it booted and lost its state on the way through".
            matchBooted = true;

            // Pass the pool so EffectType.Summon can resolve a ReferenceId to a real definition.
            matchEngine = new MatchEngine(matchState, pool);

            if (enableAI)
            {
                aiOpponent = new SimpleAI(matchEngine, matchState, PlayerIndex.Opponent, aiSeed);
                Debug.Log("[GameCoordinator] AI opponent created");
            }

            // The toss is taken under a cup: rolled here, before either lineup is fielded, and revealed only
            // once setup closes, so neither seat can see who goes first while choosing what to stand on it.
            // CompleteSetup would take it on demand if this line were missing, which is why no front end can
            // open a match without a first player; taking it here keeps the die out of the closing step.
            matchState.FlipCoin();

            // The opponent fields straight away - it has no screen to choose on - and only the player's seat
            // waits. With the AI switched off, its seat is filled mechanically too, or setup would never close.
            if (aiOpponent != null)
            {
                aiOpponent.ChooseLineup();
            }
            else
            {
                FieldOfferMechanically(PlayerIndex.Opponent);
            }

            initializing = false;
            yield return WaitForPlayerLineup();

            if (matchState == null)
            {
                // Domain reload during the wait. Update's boot guard restarts the match, so this coroutine
                // simply stops touching what is no longer there.
                yield break;
            }

            // Setup has closed: what nobody fielded went back into its deck, both opening hands are drawn, and
            // the coin winner's first Upkeep has already run - so the hand is synced before the player is
            // allowed to act, and the toss is finally shown.
            SyncHand();
            matchStarted = true;
            RevealCoinFlip();
            Debug.Log($"[GameCoordinator] Match started - {matchState.FirstPlayer} took the first turn");
            LogMatchState();
        }

        /// <summary>
        /// Holds the match open until the player's opening lineup is confirmed, which is the second of the two
        /// confirmations that close setup.
        /// <para>
        /// The wait is written against the match state rather than a flag this class owns, because
        /// <c>CompleteSetup</c> is internal and the engine's confirm path is its only caller: if the state says
        /// the lineup is confirmed, whatever confirmed it did the whole job, including the hands and the first
        /// Upkeep. So this coroutine does not need to know that the picker exists - it publishes the offer and
        /// then waits for the rules to say setup is over.
        /// </para>
        /// </summary>
        IEnumerator WaitForPlayerLineup()
        {
            ShowLineupOffer();

            while (matchState != null && matchState.SetupInProgress)
            {
                yield return null;
            }
        }

        /// <summary>
        /// Fields a seat's whole offer, in the order it was dealt, onto the Active sockets in battlefield order.
        /// This is for the seat with nothing to choose on - the opponent's, when the AI is switched off - and it
        /// exists because a seat nobody fills is a setup that never closes and therefore a match that never
        /// opens. The player's seat never comes through here: they drag their own five.
        /// </summary>
        void FieldOfferMechanically(PlayerIndex who)
        {
            PlayerState player = matchState.GetPlayer(who);
            if (player.LineupConfirmed)
            {
                return;
            }

            int[] ids = new int[ConfirmLineupCommand.RequiredLength];
            for (int i = 0; i < ids.Length && i < player.LineupOffer.Count; i++)
            {
                ids[i] = player.LineupOffer[i].InstanceId;
            }

            CommandResult result = matchEngine.ExecuteCommand(new ConfirmLineupCommand(who, ids));
            if (result != CommandResult.Success)
            {
                Debug.LogError($"[GameCoordinator] {who}'s opening lineup was rejected ({result}) " +
                               "- setup cannot close and the match will not open.");
            }
        }

        /// <summary>
        /// Shows the player the toss the whole setup has been holding. The face of the coin is not stored
        /// anywhere, so this names the outcome and the compensation rather than claiming a head or a tail; the
        /// engine's own log line, which does name the face, is already in the log panel.
        /// </summary>
        void RevealCoinFlip()
        {
            if (matchState == null || !matchState.CoinFlipped)
            {
                return;
            }

            ShowNotice(matchState.FirstPlayer == PlayerIndex.Player
                ? "Coin flip - you take the first turn."
                : "Coin flip - the opponent takes the first turn, and you open with " +
                  $"+{MatchState.SecondPlayerNPBonus} NP.");
        }

        // ---- Opening lineup picker -----------------------------------------------------
        // What the player has chosen before the match opens lives here, and none of it is a rule. The engine
        // holds no partial nomination: a previewed socket is still an empty socket as far as the battle is
        // concerned, until ConfirmLineupCommand submits the whole array in one go.

        /// <summary>
        /// The nomination array: one offered card id per Active socket in battlefield order (the Core rank, then
        /// each Node's Active rank), and 0 for a socket the player has left empty. It is sized and ordered
        /// exactly as <see cref="ConfirmLineupCommand"/> reads it back, which is the whole reason the order is
        /// duplicated here rather than derived per call.
        /// </summary>
        private readonly int[] lineupNomination = new int[ConfirmLineupCommand.RequiredLength];

        /// <summary>
        /// Puts the offer in front of the player: the fan shows the Units they have not nominated yet, the board
        /// previews the ones they have, and the rail recounts. Called once when setup opens and again after
        /// every nominate, return or reroll, because all three change what is on show.
        /// </summary>
        void ShowLineupOffer()
        {
            if (matchState == null)
            {
                return;
            }

            PlayerState player = matchState.Players[0];
            var shown = new List<CardData>(player.LineupOffer.Count);
            foreach (CardInstance offer in player.LineupOffer)
            {
                if (offer != null && !IsNominated(offer.InstanceId))
                {
                    shown.Add(DataFor(offer));
                }
            }

            if (handController != null)
            {
                handController.SetHand(shown);
            }

            if (presenter != null)
            {
                // A copy, so the board previews a snapshot the coordinator owns rather than a live array it can
                // mutate underneath a paint.
                presenter.SetLineupPreview((int[])lineupNomination.Clone());
            }

            RefreshSetupBar(player);
        }

        /// <summary>
        /// Writes the rail's two setup buttons. Confirm carries the count of fielded sockets because a partial
        /// lineup is a legal decision rather than a mistake, and Reroll carries its own price because it
        /// escalates by one NP per use - a player who memorised "2 NP" would be wrong on the second click.
        /// </summary>
        void RefreshSetupBar(PlayerState player)
        {
            if (battlefieldHud == null)
            {
                return;
            }

            int fielded = 0;
            for (int i = 0; i < lineupNomination.Length; i++)
            {
                if (lineupNomination[i] != 0)
                {
                    fielded++;
                }
            }

            int cost = matchState.LineupRerollCost(PlayerIndex.Player);
            battlefieldHud.SetSetupBar(
                matchState.SetupInProgress && !player.LineupConfirmed,
                $"REROLL - {cost} NP",
                player.CurrentNP >= cost,
                // "CONFIRM LINEUP  n/5" was 19 characters in a 162 pixel slab at 17pt, and the rail's labels are
                // raw TextMeshProUGUI with TMP's default Overlay overflow, so the surplus was not truncated - it
                // was drawn under the button, on the tabletop, where the shipped scene showed it. The counter is
                // the half worth keeping: LINEUP is what the banner says, and CONFIRM is what the button does.
                $"CONFIRM  {fielded}/{lineupNomination.Length}");
        }

        bool IsNominated(int instanceId)
        {
            for (int i = 0; i < lineupNomination.Length; i++)
            {
                if (lineupNomination[i] == instanceId)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The nomination index an Active socket carries, in battlefield order: the Core rank first, then each
        /// Node's Active rank, exactly as <c>MatchEngine.ActiveSlotsInBattlefieldOrder</c> walks them. -1 for
        /// anything else, which is the picker's way of saying that a Bench, a Protocol or an enemy socket is not
        /// a place an opening Unit can stand.
        /// <para>
        /// Public and static for the same reason <see cref="BattlefieldPresenter.TryParseSlotIdentity"/> is:
        /// this is the one place where the socket a player dropped onto and the slot the engine will read are
        /// the same number, and a mismatch there silently fields the lineup one socket to the side. The drag
        /// that reaches it cannot be driven from a test, so the mapping is tested on its own instead.
        /// </para>
        /// </summary>
        public static int NominationIndexFor(BoardSlot slot)
        {
            if (slot == null || !slot.IsActive())
            {
                return -1;
            }

            return slot.Type == SlotType.CoreActive
                ? slot.Index
                : Board.CoreActiveSlots + slot.NodeIndex * Board.NodeActiveSlots + slot.Index;
        }

        /// <summary>
        /// What a nomination index is called to a player, in the board's own words. The node is derived by the
        /// same division <see cref="NominationIndexFor"/> multiplies, so the label cannot drift from the socket
        /// it names if the Actives ever grow past one slot each.
        /// </summary>
        static string SocketName(int index)
        {
            if (index < Board.CoreActiveSlots)
            {
                return "CORE";
            }

            return $"NODE {(index - Board.CoreActiveSlots) / Board.NodeActiveSlots + 1}";
        }

        /// <summary>
        /// The drop gate for a nomination, and the setup half of <see cref="ResolveDropTarget"/>. Any of the
        /// player's Active sockets is a target - including one already carrying a nomination, because replacing
        /// what stands there is friendlier than refusing the drop and making the player clear it first.
        /// </summary>
        BoardSlot ResolveLineupDropTarget(CardData cardData, Vector3 worldPos)
        {
            BoardSlot hit = presenter.FindDropTargetAtScreenByWorld(worldPos);
            if (hit == null)
            {
                return null;
            }

            PlayerState player = matchState.Players[0];
            BoardSlot real = player.Board.GetSlot(hit.Type, hit.Index, hit.NodeIndex);
            CardInstance card = FindOfferCard(player, cardData);

            // IsFree is a guard rather than a rule here: nothing occupies a socket while setup is open, and if
            // that ever stops being true the picker should refuse it rather than stack two previews on one tile.
            bool legal = real != null && card != null
                && real.IsActive()
                && real.IsFree()
                && real.CanHost(card.Definition.Type)
                && !matchState.IsMatchOver();

            if (!legal)
            {
                presenter.ClearDropHighlight();
                return null;
            }

            return real;
        }

        /// <summary>
        /// The fan's nomination gate: records which offered Unit the player dropped onto which Active socket, and
        /// asks the engine nothing at all - no NP, no placement, no card movement - so a nomination stays
        /// takeable-back for as long as setup is open.
        /// <para>
        /// The repaint deliberately does not happen in this call. The dragged view is still in the fan's own list
        /// while the resolver is being asked - it retires the view only once this returns true - so rebuilding
        /// the hand from here would destroy that view as an unwanted one and leave the fan about to destroy it a
        /// second time. <see cref="OnCardNominated"/> runs after the retirement, which is the first moment a
        /// rebuild is safe.
        /// </para>
        /// </summary>
        bool NominateLineupCard(CardData cardData, BoardSlot slot)
        {
            if (matchState == null || !matchState.SetupInProgress || cardData == null || slot == null)
            {
                return false;
            }

            if (NominationIndexFor(slot) < 0)
            {
                return false;
            }

            CardInstance card = FindOfferCard(matchState.Players[0], cardData);
            if (card == null)
            {
                // The fan is showing a Unit that is no longer offered, which means a reroll landed underneath an
                // in-flight drag. Dropping that drag is honest; naming a card the deck has taken back is not.
                return false;
            }

            lineupNomination[NominationIndexFor(slot)] = card.InstanceId;
            return true;
        }

        /// <summary>
        /// After a nomination lands: the fan closes up over the remaining offer, the board previews what was
        /// fielded, and the rail recounts. The notice says where the Unit went, because the socket it landed on
        /// can be behind the card the player is still holding.
        /// </summary>
        void OnCardNominated(CardData cardData, BoardSlot slot)
        {
            ShowLineupOffer();

            int index = NominationIndexFor(slot);
            if (index >= 0 && cardData != null)
            {
                ShowNotice($"{cardData.title} fielded on {SocketName(index)} - click it to send it back.");
            }
        }

        /// <summary>
        /// Sends a nominated Unit back to the offer. The player asked for it by clicking the preview standing on
        /// its socket, and a choice that cannot be taken back is not a choice.
        /// <para>
        /// The card is the argument rather than the socket, because the front end that saw the click knows which
        /// card the board is showing and does not know how the nomination array is ordered - that ordering
        /// belongs to <c>ConfirmLineupCommand</c> and to nothing else.
        /// </para>
        /// </summary>
        public void ReturnLineupNomination(CardInstance card)
        {
            if (matchState == null || !matchState.SetupInProgress || card == null)
            {
                return;
            }

            for (int i = 0; i < lineupNomination.Length; i++)
            {
                if (lineupNomination[i] != card.InstanceId)
                {
                    continue;
                }

                lineupNomination[i] = 0;
                ShowLineupOffer();
                ShowNotice($"{card.Definition.Name} is back in the offer.");
                return;
            }
        }

        /// <summary>
        /// Buys a whole new offer for the escalating price. Every nomination is dropped along with it, because
        /// the Units it named go back into the deck: a nomination pointing at a card the offer no longer holds
        /// would be refused on confirm and read as a broken picker rather than as a reroll.
        /// </summary>
        public void RerollLineup()
        {
            if (matchState == null || matchEngine == null || !matchState.SetupInProgress
                || matchState.Players[0].LineupConfirmed)
            {
                return;
            }

            PlayerState player = matchState.Players[0];
            int cost = matchState.LineupRerollCost(PlayerIndex.Player);
            CommandResult result = matchEngine.ExecuteCommand(new RerollLineupCommand(PlayerIndex.Player));
            if (result == CommandResult.InsufficientNP)
            {
                ShowNotice($"A reroll costs {cost} NP and you have {player.CurrentNP}.");
                return;
            }

            if (result != CommandResult.Success)
            {
                ShowNotice($"The reroll was refused ({result}).");
                return;
            }

            System.Array.Clear(lineupNomination, 0, lineupNomination.Length);
            ShowLineupOffer();
        }

        /// <summary>
        /// Submits the nomination, which is the only thing that closes setup. Everything the opening owes the
        /// player - the unchosen Units back into the deck, both hands, the readiness waiver, the coin winner's
        /// first Upkeep - runs inside that one command, so this method's whole job is to hand over the array and
        /// get out of the way. The boot coroutine is waiting on the state, not on this call.
        /// </summary>
        public void SubmitLineup()
        {
            if (matchState == null || matchEngine == null || !matchState.SetupInProgress
                || matchState.Players[0].LineupConfirmed)
            {
                return;
            }

            CommandResult result = matchEngine.ExecuteCommand(new ConfirmLineupCommand(
                PlayerIndex.Player, (int[])lineupNomination.Clone()));
            if (result != CommandResult.Success)
            {
                ShowNotice($"The lineup was refused ({result}).");
                return;
            }

            // Setup has closed, so the preview is now a picture of Units that are on the board, in a hand, or
            // back in the deck. The real paint takes over from here.
            if (presenter != null)
            {
                presenter.SetLineupPreview(null);
            }

            if (battlefieldHud != null)
            {
                battlefieldHud.SetSetupBar(false, null, false, null);
            }
        }

        /// <summary>
        /// The offered <see cref="CardInstance"/> behind a view's <see cref="CardData"/>, pinned to the exact
        /// instance the way <see cref="FindHandCard"/> pins a hand card: several copies of one definition can be
        /// on show, and a stale drag must never nominate a different physical card than the one it lifted.
        /// </summary>
        private CardInstance FindOfferCard(PlayerState player, CardData cardData)
        {
            if (cardData == null)
            {
                return null;
            }

            if (instanceByData.TryGetValue(cardData, out int instanceId))
            {
                return player.LineupOffer.Find(c => c.InstanceId == instanceId);
            }

            return string.IsNullOrEmpty(cardData.id)
                ? null
                : player.LineupOffer.Find(c => c.Definition.Id == cardData.id);
        }

        void Update()
        {
            // A script recompile while the game is running reloads the domain: Awake/Start are not
            // called again and every non-serialized field above is wiped, so the match would stay
            // bricked with a null state forever. The serialized boot flag survives, which lets us
            // detect the loss and rebuild the match instead of failing silently on every action.
            if (matchBooted && matchState == null && !initializing)
            {
                Debug.LogWarning("[GameCoordinator] Match state was lost (domain reload); restarting the match.");
                matchStarted = false;
                WireHand();
                WireHud();
                StartCoroutine(InitializeMatch());
                return;
            }

            if (!matchStarted || matchState == null)
            {
                return;
            }

            // A finished match has no verb left to press. Without this the rail freezes on whatever the last
            // live frame published, which reads as live buttons with no match behind them, and the result line
            // is the only thing the player should be able to act on or read at that point.
            if (matchState.IsMatchOver())
            {
                if (battlefieldHud != null)
                {
                    battlefieldHud.HideRail();
                }
                return;
            }

            AdvanceAutomaticPhases();
            RefreshActionRail();

            bool inCombat = matchState.CurrentPhase == TurnPhase.Combat;

            // AI defends on the player's Combat step; blocks only live for that step.
            if (enableAI && aiOpponent != null && inCombat && matchState.ActivePlayer == PlayerIndex.Player && !aiBlocksDeclared)
            {
                aiOpponent.DeclareBlocks();
                aiBlocksDeclared = true;
            }
            else if (!inCombat)
            {
                aiBlocksDeclared = false;
            }

            // Handle AI turn
            if (enableAI && aiOpponent != null && matchState.ActivePlayer == PlayerIndex.Opponent && !aiBusy)
            {
                StartCoroutine(ExecuteAITurn());
            }

            // P and E both end up in methods that guard on the player's own turn, which the old unconditional
            // form of these two lines did not: they fired whatever ActivePlayer happened to be, so during the
            // opponent's turn - now four seconds long while the block window is open - they drove the
            // opponent's phases for it. The guard moved inside because the rail's buttons call the same methods
            // and do not pass through this block at all. Concede stays ungated, because conceding is legal on
            // either player's turn (GDD 2.1.8).
            if (Input.GetKeyDown(passPhaseKey)) EndPlayerPhase();
            if (Input.GetKeyDown(endTurnKey)) EndPlayerTurn();

            if (Input.GetKeyDown(attackCoreKey)) TryPlayerAttack(-1);
            if (Input.GetKeyDown(attackNode1Key)) TryPlayerAttack(0);
            if (Input.GetKeyDown(attackNode2Key)) TryPlayerAttack(1);
            if (Input.GetKeyDown(attackNode3Key)) TryPlayerAttack(2);
            if (Input.GetKeyDown(attackNode4Key)) TryPlayerAttack(3);

            if (Input.GetKeyDown(logStateKey)) LogMatchState();

            // Concession (GDD 2.1.8) is legal on either player's turn, so unlike the two above it is not gated
            // on the active player at all.
            if (Input.GetKeyDown(concedeKey)) ConcedeMatch();
        }

        /// <summary>
        /// Ends the player's turn. The body of the E key, and what the rail's END TURN button presses; both
        /// guards are inside rather than at the call sites because the button path does not pass through
        /// <see cref="Update"/>.
        /// </summary>
        void EndPlayerTurn()
        {
            if (!matchStarted || matchState == null || matchEngine == null
                || matchState.ActivePlayer != PlayerIndex.Player || matchState.IsMatchOver())
            {
                return;
            }

            matchEngine.ExecuteCommand(new EndTurnCommand(PlayerIndex.Player));
            SyncHand();
            LogMatchState();
        }

        /// <summary>
        /// Concedes the match. The body of the Q key, and what the rail's CONCEDE button presses. Legal on
        /// either player's turn (GDD 2.1.8), so it checks the match rather than the active player, and a
        /// refusal from the engine is silent here for the same reason the key's was: there is nothing the
        /// player can do about it except keep playing.
        /// </summary>
        void ConcedeMatch()
        {
            if (!matchStarted || matchState == null || matchEngine == null || matchState.IsMatchOver())
            {
                return;
            }

            CommandResult conceded = matchEngine.ExecuteCommand(new ConcedeCommand(PlayerIndex.Player));
            if (conceded == CommandResult.Success)
            {
                SyncHand();
                LogMatchState();
            }
        }

        /// <summary>
        /// Answers the rail buttons that fire a command on the board. The picked-up unit's skill, the two pile
        /// steps and letting the opponent's attacks land are answered by <see cref="BattlefieldInput"/>, which
        /// subscribes to the same event: those four act on dialogue state this class does not hold, and a button
        /// does not know or care who answers it.
        /// </summary>
        void OnRailPressed(BattlefieldHud.RailKey key)
        {
            switch (key)
            {
                case BattlefieldHud.RailKey.SwingCore:
                    TryPlayerAttack(-1);
                    break;
                case BattlefieldHud.RailKey.SwingNode1:
                    TryPlayerAttack(0);
                    break;
                case BattlefieldHud.RailKey.SwingNode2:
                    TryPlayerAttack(1);
                    break;
                case BattlefieldHud.RailKey.SwingNode3:
                    TryPlayerAttack(2);
                    break;
                case BattlefieldHud.RailKey.SwingNode4:
                    TryPlayerAttack(3);
                    break;
                case BattlefieldHud.RailKey.EndTurn:
                    EndPlayerTurn();
                    break;
                case BattlefieldHud.RailKey.State:
                    LogMatchState();
                    break;
                case BattlefieldHud.RailKey.Concede:
                    ConcedeMatch();
                    break;
            }
        }

        /// <summary>Passes only the non-interactive phases. Main and Combat are deliberately left visible and manual.</summary>
        void AdvanceAutomaticPhases()
        {
            if (!autoAdvanceNonInteractivePhases || matchState.ActivePlayer != PlayerIndex.Player)
            {
                return;
            }

            int guard = 0;
            while (guard++ < 8 && !matchState.IsMatchOver()
                && matchState.CurrentPhase != TurnPhase.Main
                && matchState.CurrentPhase != TurnPhase.Combat)
            {
                CommandResult result = matchEngine.ExecuteCommand(new PassPhaseCommand(PlayerIndex.Player));
                if (result != CommandResult.Success)
                {
                    break;
                }
            }
        }

        /// <summary>Callback for the in-game End Phase button.</summary>
        public void EndPlayerPhase()
        {
            if (!matchStarted || matchState == null || matchEngine == null
                || matchState.ActivePlayer != PlayerIndex.Player || matchState.IsMatchOver())
            {
                return;
            }

            if (matchState.CurrentPhase == TurnPhase.Main || matchState.CurrentPhase == TurnPhase.Combat)
            {
                matchEngine.ExecuteCommand(new PassPhaseCommand(PlayerIndex.Player));
                SyncHand();
                LogMatchState();
                AdvanceAutomaticPhases();
            }
        }

        /// <summary>
        /// Publishes the rail's board-facing keys. Every one is derived from the match state here rather than
        /// remembered from whichever event last fired, because a lit button is a promise the player acts on and
        /// a cached flag that missed a transition is the cheapest way to break it. <see
        /// cref="BattlefieldHud.SetRail"/> dedupes, so deriving this sixty times a second writes nothing until
        /// something actually changes.
        /// <para>
        /// This is also the only writer of EndPhase now. It used to be published from <see
        /// cref="AdvanceAutomaticPhases"/>, which returns early on the opponent's turn and so left the button
        /// standing on screen while its own slot was being handed to DoneBlocking - two buttons in one rect, one
        /// of them unclickable. One slot needs one writer, and a per-frame derivation is that writer.
        /// </para>
        /// </summary>
        void RefreshActionRail()
        {
            if (battlefieldHud == null)
            {
                return;
            }

            bool mine = matchState.ActivePlayer == PlayerIndex.Player;
            bool combat = mine && matchState.CurrentPhase == TurnPhase.Combat;

            battlefieldHud.SetRail(BattlefieldHud.RailKey.EndPhase, mine
                && (matchState.CurrentPhase == TurnPhase.Main
                    || matchState.CurrentPhase == TurnPhase.Combat));
            battlefieldHud.SetRail(BattlefieldHud.RailKey.DoneBlocking, PlayerBlockWindowOpen);
            battlefieldHud.SetRail(BattlefieldHud.RailKey.EndTurn, mine);

            // STATE and CONCEDE outlive the turn: reading the match is always legal, and conceding is legal on
            // either player's turn (GDD 2.1.8) - the one rule the key handler never gated either.
            battlefieldHud.SetRail(BattlefieldHud.RailKey.State, true);
            battlefieldHud.SetRail(BattlefieldHud.RailKey.Concede, true);

            // The swings are the number keys, and they are only ever fireable in Combat. Five verbs dimmed for
            // the rest of the turn would advertise a choice that refuses, so the strip leaves instead.
            battlefieldHud.SetRail(BattlefieldHud.RailKey.SwingCore, combat, CanSwingBattlefield(-1));
            battlefieldHud.SetRail(BattlefieldHud.RailKey.SwingNode1, combat, CanSwingBattlefield(0));
            battlefieldHud.SetRail(BattlefieldHud.RailKey.SwingNode2, combat, CanSwingBattlefield(1));
            battlefieldHud.SetRail(BattlefieldHud.RailKey.SwingNode3, combat, CanSwingBattlefield(2));
            battlefieldHud.SetRail(BattlefieldHud.RailKey.SwingNode4, combat, CanSwingBattlefield(3));
        }

        /// <summary>
        /// Whether a battlefield's Active socket holds a unit that may declare an attack. It resolves the socket
        /// exactly as <see cref="TryPlayerAttack"/> does, which is the point: the button dims when the key would
        /// refuse, and the two cannot drift into disagreeing about what "ready to swing" means. Legality itself
        /// stays delegated to <see cref="CanPlayerAttackWith"/> - a click layer that recomputes the rule is a
        /// click layer that can be wrong about it.
        /// </summary>
        bool CanSwingBattlefield(int nodeIndex)
        {
            Board board = matchState.Players[0].Board;
            BoardSlot slot = nodeIndex < 0
                ? (board.CoreActive.Length > 0 ? board.CoreActive[0] : null)
                : (nodeIndex < Board.NodeCount && board.NodeActive[nodeIndex].Length > 0
                    ? board.NodeActive[nodeIndex][0] : null);

            return slot != null && slot.IsOccupied() && CanPlayerAttackWith(slot.Card);
        }

        IEnumerator ExecuteAITurn()
        {
            aiBusy = true;
            yield return new WaitForSeconds(0.5f);

            if (enableAI && aiOpponent != null)
            {
                Debug.Log("[GameCoordinator] AI taking turn...");

                if (blockWindowSeconds > 0f)
                {
                    // Walk the opponent up to the threshold of its Combat step, hold the door open for the
                    // player to commit blockers, then let it swing. Combat resolves immediately in this
                    // engine, so without the pause the whole turn lands in one frame and the defender's block
                    // rule - the one rule that is theirs to use on the opponent's turn - is unreachable.
                    aiOpponent.TakeTurnUntilCombat();
                    yield return WaitForBlocks();
                    SetBlockWindow(false);
                    aiOpponent.FinishTurn();
                }
                else
                {
                    aiOpponent.TakeTurn();
                }
            }

            yield return new WaitForSeconds(0.5f);
            aiBusy = false;

            // The AI's EndTurn flips the active player back, which runs the player's Draw phase and adds a
            // card to the hand - so the view has to be resynced before control returns to the player.
            SyncHand();
            LogMatchState();
        }

        /// <summary>
        /// Holds the opponent's Combat step until the player has had their say: every legal blocker is
        /// offered, and the window closes on the done-blocking key or the timeout, whichever comes first.
        /// <para>
        /// Nothing else closes it. A committed block leaves it open, because a player with two units in two
        /// contested battlefields has two answers to give, and cancelling a selection must not be the same
        /// gesture as giving up the rest of them.
        /// </para>
        /// <para>
        /// A timeout rather than an unconditional wait, because a player with no blockers or no interest in
        /// losing four seconds per turn would feel it as a stall. The window is never left open past the
        /// opponent's Combat step, so a leaked flag cannot wedge the match.
        /// </para>
        /// </summary>
        IEnumerator WaitForBlocks()
        {
            if (matchState == null || matchState.IsMatchOver()
                || matchState.CurrentPhase != TurnPhase.Combat
                || matchState.ActivePlayer != PlayerIndex.Opponent)
            {
                yield break;
            }

            PlayerState player = matchState.Players[0];
            List<CardInstance> ready = ReadyBlockers();

            if (ready.Count == 0)
            {
                yield break;
            }

            SetBlockWindow(true);
            if (presenter != null)
            {
                presenter.ShowBlocks(ready, null);
                presenter.ShowNotice("Ready to block? Click one of your green units, then the attacker it answers.");
            }

            float deadline = Time.unscaledTime + blockWindowSeconds;
            while (Time.unscaledTime < deadline && !blockWindowClosed)
            {
                yield return null;
            }

            blockWindowClosed = false;
        }

        /// <summary>
        /// Set by the click layer to end the block window early: the player is done committing blockers and
        /// does not want to sit through the timeout.
        /// </summary>
        public void CloseBlockWindow()
        {
            blockWindowClosed = true;
        }

        private bool blockWindowClosed;

        void LogMatchState()
        {
            if (matchState == null) return;

            PlayerState player = matchState.Players[0];
            PlayerState opponent = matchState.Players[1];
            int playerCap = PlayerState.NpCap(matchState.CountControlledNodes(PlayerIndex.Player));
            int opponentCap = PlayerState.NpCap(matchState.CountControlledNodes(PlayerIndex.Opponent));

            Debug.Log($"=== MATCH STATE ===");
            Debug.Log($"Turn {matchState.TurnNumber} - Phase: {matchState.CurrentPhase} - Active: {matchState.ActivePlayer}");
            Debug.Log($"Player: {player.CurrentNP}/{playerCap} NP, Core: {player.CoreDurability}, Hand: {player.Hand.Count}, Deck: {player.Deck.Count}");
            Debug.Log($"Opponent: {opponent.CurrentNP}/{opponentCap} NP, Core: {opponent.CoreDurability}, Hand: {opponent.Hand.Count}, Deck: {opponent.Deck.Count}");

            if (matchState.IsMatchOver())
            {
                Debug.Log($"<color=yellow>MATCH OVER - Result: {matchState.Result}</color>");
            }
        }

        /// <summary>
        /// Rebuilds the hand view from the player's authoritative hand, reusing the cached
        /// <see cref="CardData"/> per card instance so views tween instead of popping.
        /// </summary>
        public void SyncHand()
        {
            if (handController == null || matchState == null)
            {
                return;
            }

            PlayerState player = matchState.Players[0];
            var data = new List<CardData>(player.Hand.Count);
            foreach (CardInstance card in player.Hand)
            {
                data.Add(DataFor(card));
            }

            handController.SetHand(data);
        }

        /// <summary>
        /// The presentation record for a live card, creating it on first sight and returning the same object
        /// every time after that.
        /// <para>
        /// Views ask for this instead of building their own because reuse downstream depends on reference
        /// identity: <c>HandController.SetHand</c> keeps a card view alive across syncs by spotting the same
        /// <see cref="CardData"/> it saw last time, and the board's pooled cards key their existence on the same
        /// trick. Two records for one <c>CardInstance</c> would not merely waste memory, it would make a card look
        /// like a different object in the fan and on the tabletop, which is the thing this whole seam exists to
        /// stop.
        /// </para>
        /// <para>
        /// The record carries the running <c>CardInstance</c> rather than a snapshot of it, so a face projected
        /// from it reads the engine's current attack, health and shield. That is what lets a board card refresh
        /// itself off <see cref="CardView.Refresh"/> instead of the presenter learning how a card is drawn.
        /// </para>
        /// </summary>
        public CardData DataFor(CardInstance card)
        {
            if (card == null)
            {
                return null;
            }

            if (dataByInstance.TryGetValue(card.InstanceId, out CardData existing))
            {
                return existing;
            }

            CardDefinition def = card.Definition;
            CardData made = new CardData(
                def.Id,
                def.Name,
                def.Cost,
                def.AbilityText,
                AccentFor(def.Type));
            made.AttachSource(def, card);

            dataByInstance[card.InstanceId] = made;
            instanceByData[made] = card.InstanceId;
            return made;
        }

        private static Color AccentFor(CardType type)
        {
            switch (type)
            {
                case CardType.Ability: return new Color(0.80f, 0.55f, 0.30f);
                case CardType.Relay: return new Color(0.30f, 0.65f, 0.50f);
                case CardType.Protocol: return new Color(0.55f, 0.40f, 0.75f);
                default: return new Color(0.50f, 0.55f, 0.70f);
            }
        }

        /// <summary>
        /// The HandController's play gate. Runs when a card is released over a highlighted socket or above
        /// the play line, decides whether the play is legal and, if it is, executes it as a validated
        /// <see cref="PlayCardCommand"/> and resyncs the hand. Returns false so the card bounces back to the
        /// hand untouched.
        /// </summary>
        public bool TryPlayFromUI(CardData cardData)
        {
            return TryPlayFromUISlot(cardData, null);
        }

        /// <summary>
        /// As <see cref="TryPlayFromUI"/>, but honours an explicit destination socket when the player
        /// dropped the card on one. A null <paramref name="chosenSlot"/> keeps the original behaviour of
        /// letting <see cref="Board.TryFindFreeSlot"/> spread the unit across the battlefields.
        /// </summary>
        public bool TryPlayFromUISlot(CardData cardData, BoardSlot chosenSlot)
        {
            if (matchState == null || matchEngine == null || cardData == null)
            {
                // Never fail silently here: this guard is invisible in the console otherwise, which
                // turns every rejected drag into an unexplained "[Hand] rejected" message.
                return Reject(
                    $"play gate not ready (matchState {(matchState == null ? "NULL" : "set")}, " +
                    $"matchEngine {(matchEngine == null ? "NULL" : "set")}, cardData {(cardData == null ? "NULL" : "set")}).");
            }

            if (matchState.IsMatchOver())
            {
                return Reject("the match is already over.");
            }

            // A card released above the play line rather than on a lit socket arrives here with no slot, and
            // during setup that is the only way a drag can reach the play gate at all: the nomination gate has
            // already taken every drop that landed on a socket. Say what the opening actually wants.
            if (matchState.SetupInProgress)
            {
                return Reject("the opening is a lineup, not a play - drop a Unit onto one of the five lit " +
                              "Active sockets, then press CONFIRM.");
            }

            if (matchState.ActivePlayer != PlayerIndex.Player)
            {
                return Reject("it is not your turn.");
            }

            // GDD 2.1.2 allows plays in the Main phase only, and a turn opens in Upkeep, so the very first
            // drag of a match is illegal until the phase has been passed twice. This used to be the whole
            // difference between "the game is broken" and "I need to press P".
            if (matchState.CurrentPhase != TurnPhase.Main)
            {
                return Reject($"{cardData.title} cannot be played during {matchState.CurrentPhase} - cards are played in the Main phase (press P).");
            }

            PlayerState player = matchState.Players[0];
            CardInstance card = FindHandCard(player, cardData);

            if (card == null)
            {
                // A view/state desync rather than a player mistake, so it stays an error in the console.
                Debug.LogError($"[GameCoordinator] Card {cardData.title} not found in player's hand.");
                ShowNotice($"{cardData.title} is not in your hand.");
                return false;
            }

            if (!player.CanAfford(card.Definition.Cost))
            {
                return Reject($"not enough NP for {card.Definition.Name}: needs {card.Definition.Cost}, has {player.CurrentNP}.");
            }

            SlotType targetSlotType;
            int targetSlotIndex;
            int targetNodeIndex;

            if (card.Definition.Type == CardType.Ability || card.Definition.Type == CardType.Protocol)
            {
                // Neither occupies a Core/Node slot: Abilities take none at all and Protocols take the single
                // player-side Protocol slot, which the engine checks itself. Slot fields are ignored.
                targetSlotType = SlotType.CoreActive;
                targetSlotIndex = 0;
                targetNodeIndex = -1;
            }
            else
            {
                // A drop onto a highlighted socket is honoured exactly: the player aimed at that slot, so
                // routing the unit somewhere else would make the highlight a lie. Only with no nominated
                // slot (a plain fling above the play line) does the auto-deploy spread decide.
                BoardSlot slot = chosenSlot != null
                    ? player.Board.GetSlot(chosenSlot.Type, chosenSlot.Index, chosenSlot.NodeIndex)
                    : null;

                // PR-05: If a slot was nominated but resolution returned null, reject rather than auto-placing.
                // An invalid nominated socket should not silently deploy elsewhere.
                if (chosenSlot != null && slot == null)
                {
                    return Reject($"The chosen slot is no longer available.");
                }

                if (slot != null && !slot.CanHost(card.Definition.Type))
                {
                    return Reject($"{card.Definition.Name} cannot be deployed to that socket.");
                }

                if (slot == null && !TryChooseSlot(player, card.Definition.Type, out slot))
                {
                    return Reject($"no free slot is available for {card.Definition.Name}.");
                }

                targetSlotType = slot.Type;
                targetSlotIndex = slot.Index;
                targetNodeIndex = slot.NodeIndex;
            }

            var command = new PlayCardCommand(
                PlayerIndex.Player, card.InstanceId, targetSlotType, targetSlotIndex, targetNodeIndex);

            CommandResult result = matchEngine.ExecuteCommand(command);
            if (result != CommandResult.Success)
            {
                return Reject($"play of {card.Definition.Name} was rejected by the engine ({result}).");
            }

            Debug.Log($"[GameCoordinator] Player played {card.Definition.Name}");
            if (presenter != null && card.Definition.Type != CardType.Ability)
            {
                presenter.PlayDeployAnimation(card);
            }
            SyncHand();
            LogMatchState();
            return true;
        }

        /// <summary>
        /// Attacks from the Active unit of one battlefield: <paramref name="nodeIndex"/> -1 selects the
        /// Core battlefield, 0-3 select Node 1-4. The defender is the enemy Active in the same
        /// battlefield when one exists, otherwise the enemy Core. Uses
        /// <see cref="DeclareAttackCommand"/> like every other actor, so blocking/validation stays in the
        /// engine.
        /// </summary>
        public void TryPlayerAttack(int nodeIndex)
        {
            if (matchState == null || matchEngine == null || matchState.IsMatchOver())
            {
                return;
            }

            if (matchState.ActivePlayer != PlayerIndex.Player || matchState.CurrentPhase != TurnPhase.Combat)
            {
                Debug.LogWarning("[GameCoordinator] You can only attack during your own Combat phase.");
                return;
            }

            Board board = matchState.Players[0].Board;
            BoardSlot slot;

            if (nodeIndex < 0)
            {
                slot = board.CoreActive.Length > 0 ? board.CoreActive[0] : null;
            }
            else if (nodeIndex < Board.NodeCount)
            {
                slot = board.NodeActive[nodeIndex].Length > 0 ? board.NodeActive[nodeIndex][0] : null;
            }
            else
            {
                return;
            }

            string where = nodeIndex < 0 ? "Core Active" : $"Node {nodeIndex + 1} Active";

            if (slot == null || !slot.IsOccupied() || !slot.Card.CanAct())
            {
                Debug.LogWarning($"[GameCoordinator] {where} has no ready unit to attack with.");
                return;
            }

            PlayerState opponent = matchState.Players[1];

            // Prefer the enemy Active in the same battlefield (only it can block this attack); fall back
            // to any surviving enemy Active, and then to the Core.
            Board opponentBoard = opponent.Board;
            BoardSlot mirror = nodeIndex < 0
                ? (opponentBoard.CoreActive.Length > 0 ? opponentBoard.CoreActive[0] : null)
                : (nodeIndex < Board.NodeCount && opponentBoard.NodeActive[nodeIndex].Length > 0
                    ? opponentBoard.NodeActive[nodeIndex][0]
                    : null);

            CardInstance target = mirror != null && mirror.IsOccupied() && mirror.Card.IsAlive()
                ? mirror.Card
                : opponentBoard.GetActiveUnits(opponent.Index)
                    .Where(u => u.IsAlive())
                    .OrderBy(u => u.CurrentHp)
                    .FirstOrDefault();

            int defenderInstanceId = target != null ? target.InstanceId : -1;

            var command = new DeclareAttackCommand(PlayerIndex.Player, slot.Card.InstanceId, defenderInstanceId);
            CommandResult result = matchEngine.ExecuteCommand(command);

            if (result == CommandResult.Success)
            {
                string targetName = target != null ? target.Definition.Name : $"{opponent.Index} Core";
                Debug.Log($"[GameCoordinator] {slot.Card.Definition.Name} ({where}) attacks {targetName}");
                if (presenter != null)
                {
                    presenter.PlayAttackAnimation(slot.Card, target);
                }
            }
            else
            {
                Debug.LogWarning($"[GameCoordinator] Attack rejected: {result}");
            }

            SyncHand();

            if (matchState.IsMatchOver())
            {
                if (presenter != null)
                {
                    presenter.ClearDropHighlight();
                }
                LogMatchState();
            }
        }

        // ---- Point-and-click combat ---------------------------------------------------
        // The keyboard path above attacks whatever the coordinator chooses for the player, which is fine for
        // a smoke test and useless as a game. These are the entry points the click layer drives: what may act,
        // what it may hit, and then the same DeclareAttackCommand the AI already goes through.
        //
        // The legality arithmetic itself lives in CombatLegality, in the headless Core assembly: it is derived
        // state with no Unity dependency, so it is unit-testable without a scene, and netplay will want the
        // same enumeration when it needs to validate a remote player's options. What is left here is the match
        // plumbing - whose state it is, and where a rejection gets surfaced.

        /// <summary>See <see cref="CombatLegality.CanAttack"/>. The player's view of it.</summary>
        public bool CanPlayerAttackWith(CardInstance card)
        {
            return CombatLegality.CanAttack(matchState, PlayerIndex.Player, card);
        }

        /// <summary>
        /// See <see cref="CombatLegality.CanActivate"/>. The player's view of it.
        /// <para>
        /// This used to restate the rule inline, and the restatement was subtly wrong: it omitted the
        /// Active-slot requirement the engine enforces by resolving the id through
        /// <c>FindActiveCardInstance</c>, so a [Activate] unit sitting on the Bench lit up as activatable and
        /// then failed with <c>UnitCannotAct</c>. Delegating is the fix, and the reason the arithmetic lives in
        /// Core — a click layer that recomputes legality is a click layer that can disagree with the engine.
        /// </para>
        /// </summary>
        public bool CanPlayerActivateWith(CardInstance card)
        {
            return CombatLegality.CanActivate(matchState, PlayerIndex.Player, card);
        }

        public bool TryPlayerActivateWith(CardInstance card)
        {
            if (!CanPlayerActivateWith(card))
            {
                return Reject(card == null ? "there is no unit selected to activate."
                    : $"{card.Definition.Name} cannot activate its skill right now.");
            }

            CommandResult result = matchEngine.ExecuteCommand(
                new ActivateAbilityCommand(PlayerIndex.Player, card.InstanceId));
            if (result != CommandResult.Success)
            {
                return Reject($"skill activation was rejected by the engine ({result}).");
            }

            Debug.Log($"[GameCoordinator] Player activated {card.Definition.Name}'s skill");
            SyncHand();
            return true;
        }

        /// <summary>See <see cref="CombatLegality.CanRetreat"/>. The player's view of it.</summary>
        public bool CanPlayerRetreatWith(CardInstance active, CardInstance bench)
        {
            return CombatLegality.CanRetreat(matchState, PlayerIndex.Player, active, bench);
        }

        /// <summary>See <see cref="CombatLegality.RetreatChoices"/>. The player's view of it.</summary>
        public List<CardInstance> RetreatChoicesForPlayer(CardInstance active)
        {
            return CombatLegality.RetreatChoices(matchState, PlayerIndex.Player, active);
        }

        /// <summary>
        /// Trades an Active unit out for one of the player's Bench units, for
        /// <see cref="MatchState.RetreatNP"/>, during Combat. This is the second of the two things an Active
        /// unit may do on its controller's Combat step, which is why it is asked and answered exactly like an
        /// attack: re-derive legality from the rules rather than trusting the mark the board is showing, hand
        /// the engine both instance ids, and surface the rejection reason on failure.
        /// </summary>
        public bool TryPlayerRetreatWith(CardInstance active, CardInstance bench)
        {
            if (matchState == null || matchEngine == null)
            {
                return false;
            }

            if (active == null)
            {
                return Reject("there is no unit selected to retreat.");
            }

            if (!CanPlayerRetreatWith(active, bench))
            {
                return Reject(bench == null
                    ? $"{active.Definition.Name} has nowhere to retreat to right now."
                    : $"{active.Definition.Name} cannot retreat for {bench.Definition.Name} right now.");
            }

            CommandResult result = matchEngine.ExecuteCommand(
                new RetreatCommand(PlayerIndex.Player, active.InstanceId, bench.InstanceId));
            if (result != CommandResult.Success)
            {
                return Reject($"retreat was rejected by the engine ({result}).");
            }

            Debug.Log($"[GameCoordinator] {active.Definition.Name} retreats for {bench.Definition.Name}");
            SyncHand();
            return true;
        }

        /// <summary>See <see cref="CombatLegality.AttackTargets"/>. The player's view of it.</summary>
        public List<CardInstance> TargetsForPlayerAttack(CardInstance attacker)
        {
            return CombatLegality.AttackTargets(matchState, PlayerIndex.Player, attacker);
        }

        /// <summary>
        /// True when the direct Core strike is worth offering alongside the unit targets. See
        /// <see cref="CombatLegality.CanAttackCore"/> for why this is not "the opponent has nothing to block".
        /// </summary>
        public bool CanPlayerAttackCore(CardInstance attacker)
        {
            return CombatLegality.CanAttackCore(matchState, PlayerIndex.Player, attacker);
        }

        /// <summary>
        /// Declares an attack through the engine. <paramref name="target"/> null means the direct Core hit
        /// of GDD 2.1.6 Step 4. Returns false, and paints the reason on the board, when the engine refuses.
        /// </summary>
        public bool TryPlayerAttackWith(CardInstance attacker, CardInstance target)
        {
            if (matchState == null || matchEngine == null)
            {
                return false;
            }

            if (attacker == null)
            {
                return Reject("there is no unit selected to attack with.");
            }

            // Re-ask the rules rather than trusting the click: the marks were computed from this same query
            // when the player picked the unit up, and anything that happened since - the unit dying to an
            // effect, the turn changing hands - makes them stale. The engine's declare-attack path checks the
            // phase and the attacker's readiness but not <em>whose</em> turn it is, so a command issued off a
            // stale highlight would otherwise go through on the opponent's Combat step.
            if (!CanPlayerAttackWith(attacker))
            {
                return Reject($"{attacker.Definition.Name} cannot attack right now.");
            }

            var command = new DeclareAttackCommand(PlayerIndex.Player, attacker.InstanceId,
                target != null ? target.InstanceId : -1);

            CommandResult result = matchEngine.ExecuteCommand(command);
            if (result != CommandResult.Success)
            {
                return Reject(AttackFailure(attacker, target, result));
            }

            string what = target != null ? target.Definition.Name : "the enemy Core";
            Debug.Log($"[GameCoordinator] {attacker.Definition.Name} attacks {what}");
            if (presenter != null)
            {
                presenter.PlayAttackAnimation(attacker, target);
            }
            SyncHand();

            if (matchState.IsMatchOver())
            {
                if (presenter != null)
                {
                    presenter.ClearDropHighlight();
                }
                LogMatchState();
            }

            return true;
        }

        static string AttackFailure(CardInstance attacker, CardInstance target, CommandResult result)
        {
            string what = target != null ? target.Definition.Name : "the enemy Core";
            switch (result)
            {
                case CommandResult.InvalidPhase: return $"{attacker.Definition.Name} cannot attack outside the Combat phase.";
                case CommandResult.UnitCannotAct: return $"{attacker.Definition.Name} cannot act - it is exhausted, summoned this step, or not in an Active socket.";
                case CommandResult.InvalidTarget: return $"{attacker.Definition.Name} cannot attack {what}.";
                default: return $"attack by {attacker.Definition.Name} was rejected by the engine ({result}).";
            }
        }

        /// <summary>
        /// True when the player may commit this card as a blocker right now. See
        /// <see cref="CombatLegality.CanBlock"/> for the rules; this is the match-plumbing wrapper.
        /// </summary>
        public bool CanPlayerBlockWith(CardInstance card)
        {
            return CombatLegality.CanBlock(matchState, matchEngine, PlayerIndex.Player, card);
        }

        /// <summary>
        /// The enemy attacks one of our units could legally answer. See
        /// <see cref="CombatLegality.BlockableAttackers"/> - the placement rule lives there so it can be
        /// tested headlessly against the engine that enforces it on consumption.
        /// </summary>
        public List<CardInstance> AttackersBlockableBy(CardInstance blocker)
        {
            return CombatLegality.BlockableAttackers(matchState, matchEngine, PlayerIndex.Player, blocker);
        }

        /// <summary>
        /// Every unit the player could commit as a blocker right now <em>and</em> that has an attack to answer.
        /// <para>
        /// The second half of that is what makes it worth being a method rather than a loop at the one call
        /// site: a Ready unit in a battlefield where nothing is swinging cannot block anything, so offering it
        /// would light a socket whose target list is empty. Both the window-opening coroutine and the click
        /// layer ask this, which is why committing one block and re-offering the rest cannot disagree with
        /// how the window was opened in the first place.
        /// </para>
        /// </summary>
        public List<CardInstance> ReadyBlockers()
        {
            var ready = new List<CardInstance>();

            // Deliberately not gated on PlayerBlockWindowOpen: CanPlayerBlockWith already encodes the rules
            // (whose turn, which phase, which slots), and the window flag is about whether the click layer is
            // listening. Asking this before opening the window is exactly what decides whether the window is
            // worth opening at all.
            if (matchState == null)
            {
                return ready;
            }

            foreach (CardInstance card in matchState.Players[0].Board.GetActiveUnits(PlayerIndex.Player))
            {
                if (CanPlayerBlockWith(card) && AttackersBlockableBy(card).Count > 0)
                {
                    ready.Add(card);
                }
            }

            return ready;
        }

        /// <summary>Commits a block through the engine. See <see cref="TryPlayerAttackWith"/> for the shape.</summary>
        public bool TryPlayerBlockWith(CardInstance blocker, CardInstance attacker)
        {
            if (matchState == null || matchEngine == null || blocker == null || attacker == null)
            {
                return false;
            }

            // Same stale-highlight guard as the attack path, and it matters more here: the block window is a
            // four-second gap in which the board can change underneath the player, and the engine's own
            // declaration check happily records a block from the wrong battlefield that
            // <c>ConsumePendingBlocker</c> will then silently ignore.
            if (!PlayerBlockWindowOpen || !CanPlayerBlockWith(blocker)
                || !AttackersBlockableBy(blocker).Contains(attacker))
            {
                return Reject($"{blocker.Definition.Name} cannot answer that attack from where it stands.");
            }

            CommandResult result = matchEngine.ExecuteCommand(
                new DeclareBlockCommand(PlayerIndex.Player, blocker.InstanceId, attacker.InstanceId));

            if (result != CommandResult.Success)
            {
                return result switch
                {
                    CommandResult.CannotBlock => Reject($"{blocker.Definition.Name} cannot block (exhausted, or it has [Cannot Block])."),
                    CommandResult.InvalidPhase => Reject("blocking happens during the opponent's Combat phase."),
                    CommandResult.InvalidCommand => Reject($"{blocker.Definition.Name} is already committed, or {attacker.Definition.Name} already has a blocker."),
                    _ => Reject($"block by {blocker.Definition.Name} was rejected by the engine ({result})."),
                };
            }

            Debug.Log($"[GameCoordinator] Player readies {blocker.Definition.Name} to block {attacker.Definition.Name}");
            if (presenter != null)
            {
                presenter.PlayBlockAnimation(blocker, attacker);
            }
            return true;
        }

        /// <summary>
        /// True while the player is entitled to declare blocks: it is the opponent's turn, they are in their
        /// Combat phase, and the coordinator has opened the window rather than letting the AI attack straight
        /// through it. The click layer reads this to decide whether a click on one of the player's units
        /// picks a blocker or just opens the card.
        /// </summary>
        public bool PlayerBlockWindowOpen { get; private set; }

        /// <summary>
        /// Opens or closes the player's block window. Called by the AI-turn coroutine, which pauses on it:
        /// without a pause the opponent's whole turn runs in one frame and the player never gets to commit a
        /// blocker, which is the reason blocking was AI-only until the click layer arrived.
        /// </summary>
        void SetBlockWindow(bool open)
        {
            PlayerBlockWindowOpen = open;
            if (!open && presenter != null)
            {
                presenter.ClearMarks();
            }
        }

        private CardInstance FindHandCard(PlayerState player, CardData cardData)
        {
            if (cardData == null)
            {
                return null;
            }

            // Preferred path: the cached CardData we handed to the view, which pins the exact instance even
            // when the hand holds several copies of the same definition.
            if (instanceByData.TryGetValue(cardData, out int instanceId))
            {
                CardInstance exact = player.Hand.Find(c => c.InstanceId == instanceId);
                // PR-03: If the cached instance is gone, return null rather than falling back to another copy.
                // A stale cached action should not select a different physical card.
                if (exact != null)
                {
                    return exact;
                }
                return null;
            }

            // Fallback keeps the coordinator usable if the view was built outside SyncHand and the CardData
            // was never in our identity map.
            return string.IsNullOrEmpty(cardData.id)
                ? null
                : player.Hand.Find(c => c.Definition.Id == cardData.id);
        }

        /// <summary>
        /// Picks the destination for a Unit or Relay: Core Active, then Core Bench, then Node Active, then
        /// Node Bench. Relays skip Core because <see cref="BoardSlot.CanHost"/> is Unit-only there.
        /// </summary>
        private static bool TryChooseSlot(PlayerState player, CardType type, out BoardSlot slot)
        {
            return player.Board.TryFindFreeSlot(type, out slot);
        }

        /// <summary>
        /// The single exit for a rejected play: warns in the console <em>and</em> paints the reason on the
        /// board, then returns false so the caller can simply <c>return Reject(...)</c>. A drag that
        /// bounces back with no message at all reads as a broken game rather than as an illegal move, which
        /// is exactly the confusion this gate has to avoid.
        /// </summary>
        private bool Reject(string reason)
        {
            Debug.LogWarning($"[GameCoordinator] Rejected {reason}");
            ShowNotice(reason);
            return false;
        }

        /// <summary>Writes a transient line on the battlefield. No-op when the view is absent.</summary>
        public void ShowNotice(string message)
        {
            if (presenter != null)
            {
                presenter.ShowNotice(message);
            }
        }

        /// <summary>
        /// Resolves the socket a dragged card is hovering over, and keeps the highlight only when a play
        /// there would actually be legal. The presenter is pure geometry and judges no rules, so the card
        /// type, occupancy, turn, phase and affordability checks all live here, next to the match state.
        /// <para>
        /// A null return means "no target": the hand then falls back to the plain play-line gesture and no
        /// socket lights up. That absence is itself the signal, and the reason is painted on the board by
        /// <see cref="Reject"/> when the player releases anyway.
        /// </para>
        /// <para>
        /// Note that a lit Bench socket is a promise about the <em>play</em>, not the resting place: Core
        /// <see cref="MatchState.ResolveDestructionAndPromotion"/> runs after every command and never lets an
        /// Active socket stay empty, so targeting a Bench while your Active is vacant still ends with the
        /// unit in Active. Refusing such a drop would cost the player their action for no gain, so it is
        /// accepted and the promotion is left to narrate itself in the board's event log.
        /// </para>
        /// </summary>
        private BoardSlot ResolveDropTarget(CardData cardData, Vector3 worldPos)
        {
            if (presenter == null || matchState == null)
            {
                return null;
            }

            // While the opening lineup is being chosen, a drag means "field this Unit on that Active socket"
            // rather than "play this card now". The two want different targets (the five Actives only), different
            // legality (no cost, no phase, no turn) and a different consequence (a preview the player can take
            // back, not a placement), so setup gets its own gate instead of a flag threaded through this one.
            if (matchState.SetupInProgress)
            {
                return ResolveLineupDropTarget(cardData, worldPos);
            }

            BoardSlot hit = presenter.FindDropTargetAtScreenByWorld(worldPos);
            if (hit == null)
            {
                return null;
            }

            PlayerState player = matchState.Players[0];
            CardInstance card = FindHandCard(player, cardData);
            BoardSlot real = player.Board.GetSlot(hit.Type, hit.Index, hit.NodeIndex);

            bool legal = card != null && real != null
                && real.IsFree()
                && real.CanHost(card.Definition.Type)
                && !matchState.IsMatchOver()
                && matchState.ActivePlayer == PlayerIndex.Player
                && matchState.CurrentPhase == TurnPhase.Main
                && player.CanAfford(card.Definition.Cost);

            if (!legal)
            {
                presenter.ClearDropHighlight();
                return null;
            }

            return real;
        }

        private void ClearDropTarget()
        {
            if (presenter != null)
            {
                presenter.ClearDropHighlight();
            }
        }

        private void OnCardPlayedAnimated(CardData data)
        {
            if (presenter != null)
            {
                presenter.ClearDropHighlight();
            }
            // The play already ran during the gate; this only catches any hand change made while the
            // resolve animation was playing (draw effects, opponent actions).
            SyncHand();
        }

        /// <summary>Kept for callers holding a <see cref="CardData"/> reference.</summary>
        public void OnPlayerPlayCard(CardData cardData)
        {
            TryPlayFromUI(cardData);
        }

        public MatchState GetMatchState() => matchState;
        public MatchEngine GetMatchEngine() => matchEngine;

        /// <summary>
        /// True while the opening lineup setup is holding the match open. The views ask this rather than reading
        /// <c>MatchState.SetupInProgress</c> directly so that "the board is not yours yet" has one meaning in
        /// the front end: the key legend, the button rail and the drag gate all stand down, and the setup picker
        /// is the only thing that is live.
        /// </summary>
        public bool IsSetupInProgress => matchState != null && matchState.SetupInProgress;
    }
}
