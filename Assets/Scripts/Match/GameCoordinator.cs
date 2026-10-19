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
            handController.dropTargetCleared = ClearDropTarget;
            handController.CardPlayed -= OnCardPlayedAnimated;
            handController.CardPlayed += OnCardPlayedAnimated;
        }

        void OnDestroy()
        {
            if (handController != null)
            {
                handController.CardPlayed -= OnCardPlayedAnimated;
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

            matchState = new MatchState();
            matchState.InitializeMatch(playerDeck, opponentDeck);

            // Pass the pool so EffectType.Summon can resolve a ReferenceId to a real definition.
            matchEngine = new MatchEngine(matchState, pool);

            if (enableAI)
            {
                aiOpponent = new SimpleAI(matchEngine, matchState, PlayerIndex.Opponent, aiSeed);
                Debug.Log("[GameCoordinator] AI opponent created");
            }

            SyncHand();
            matchStarted = true;
            matchBooted = true;
            initializing = false;
            Debug.Log("[GameCoordinator] Match started - Player's turn");
            LogMatchState();
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
                StartCoroutine(InitializeMatch());
                return;
            }

            if (!matchStarted || matchState == null || matchState.IsMatchOver())
            {
                return;
            }

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

            // Handle phase advancement (for testing).
            //
            // Gated on the player's own turn, which the old unconditional form was not: P and E fired
            // whatever ActivePlayer happened to be, so during the opponent's turn - now four seconds long
            // while the block window is open - they drove the opponent's phases for it. Concede stays
            // ungated, because conceding is legal on either player's turn (GDD 2.1.8).
            if (Input.GetKeyDown(passPhaseKey) && matchState.ActivePlayer == PlayerIndex.Player)
            {
                matchEngine.ExecuteCommand(new PassPhaseCommand(PlayerIndex.Player));
                SyncHand();
                LogMatchState();
            }

            if (Input.GetKeyDown(endTurnKey) && matchState.ActivePlayer == PlayerIndex.Player)
            {
                matchEngine.ExecuteCommand(new EndTurnCommand(PlayerIndex.Player));
                SyncHand();
                LogMatchState();
            }

            if (Input.GetKeyDown(attackCoreKey)) TryPlayerAttack(-1);
            if (Input.GetKeyDown(attackNode1Key)) TryPlayerAttack(0);
            if (Input.GetKeyDown(attackNode2Key)) TryPlayerAttack(1);
            if (Input.GetKeyDown(attackNode3Key)) TryPlayerAttack(2);
            if (Input.GetKeyDown(attackNode4Key)) TryPlayerAttack(3);

            // Log match state
            if (Input.GetKeyDown(logStateKey))
            {
                LogMatchState();
            }

            // Concession (GDD 2.1.8) is legal on either player's turn, so it is not gated on the active
            // player above. The match-over guard already lives in Update's early return.
            if (Input.GetKeyDown(concedeKey))
            {
                CommandResult conceded = matchEngine.ExecuteCommand(new ConcedeCommand(PlayerIndex.Player));
                if (conceded == CommandResult.Success)
                {
                    SyncHand();
                    LogMatchState();
                }
            }
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
            int playerCap = PlayerState.NpCap(player.Board.CountControlledNodes(PlayerIndex.Player));
            int opponentCap = PlayerState.NpCap(opponent.Board.CountControlledNodes(PlayerIndex.Opponent));

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
                data.Add(GetOrMakeData(card));
            }

            handController.SetHand(data);
        }

        private CardData GetOrMakeData(CardInstance card)
        {
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
                if (exact != null)
                {
                    return exact;
                }
            }

            // Fallback keeps the coordinator usable if the view was built outside SyncHand.
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

            BoardSlot hit = presenter.FindDropTarget(worldPos);
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
    }
}
