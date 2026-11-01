using System.Collections.Generic;
using Overlink.Cards;
using Overlink.Core;
using UnityEngine;

namespace Overlink.Match
{
    /// <summary>
    /// Turns pointer clicks on the tabletop into match actions.
    /// <para>
    /// Three jobs, in this order of precedence: <b>attack</b> (click one of your Ready units, then click what
    /// it should hit), <b>block</b> (during the opponent's Combat step, click one of your units and then the
    /// attack it answers), and <b>inspect</b> (click anything on the table and read what is behind it). The
    /// keyboard bindings on <see cref="GameCoordinator"/> still work, so this is an addition rather than a
    /// replacement - but the legend on the board advertises the mouse first, because a player who has never
    /// seen the key list has no way to guess it.
    /// </para>
    /// <para>
    /// The split of responsibility is the one the drop targeting already uses. This component owns
    /// <em>dialogue state</em> - which unit is picked up, which pile is being read, and nothing else. It asks
    /// <see cref="BattlefieldPresenter"/> what is under the pointer (geometry, no rules) and
    /// <see cref="GameCoordinator"/> whether acting on it is legal (rules, no geometry). It never mutates
    /// <see cref="MatchState"/> itself, and it never treats a lit socket as proof a move is legal: the marks
    /// are rebuilt from these same queries on every change, and a query re-run against a stale state is what
    /// a mark is checked against before a command leaves.
    /// </para>
    /// </summary>
    [DefaultExecutionOrder(200)]
    public class BattlefieldInput : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The match owner. Found automatically when left empty.")]
        public GameCoordinator coordinator;

        [Tooltip("The board view, which supplies the hit-test. Found automatically when left empty.")]
        public BattlefieldPresenter presenter;

        [Tooltip("The hand. Its drag state gates every click here: the fan sits inside the tabletop's " +
                 "footprint, so a click that picks up a card also lands on a socket behind it.")]
        public HandController hand;

        // Not serialized and not [SerializeField]: this is the rail's own four keys, and the HUD is the
        // coordinator's object to create. Found after it exists and re-found after a domain reload wipes it.
        BattlefieldHud hud;

        [Header("Keys")]
        [Tooltip("Puts the picked-up unit down and takes the details panel off screen. Does NOT end the " +
                 "opponent's block window - a reflexive Escape should not cost a player their only defensive " +
                 "move. Use the done-blocking key for that.")]
        public KeyCode cancelKey = KeyCode.Escape;

        [Tooltip("Browses backwards through the pile the details panel is showing.")]
        public KeyCode pilePrevKey = KeyCode.LeftArrow;

        [Tooltip("Browses forwards through the pile the details panel is showing.")]
        public KeyCode pileNextKey = KeyCode.RightArrow;

        [Tooltip("Lets the opponent's attacks land without waiting for the window's timeout.")]
        public KeyCode doneBlockingKey = KeyCode.Return;

        /// <summary>
        /// Why a unit is currently picked up. The two are not interchangeable: an attacker was nominated by a
        /// player who is swinging, a blocker by a player who is defending, and each is only valid while the
        /// half-turn that offered it is still running. Keeping them apart is what stops a blocker picked up
        /// during the opponent's Combat step from being re-read as an attacker the moment the window closes -
        /// which would offer the player an attack on the opponent's turn.
        /// </summary>
        enum PickMode
        {
            None,
            Attack,
            Block,
        }

        PickMode mode;
        CardInstance selected;
        readonly List<CardInstance> targets = new List<CardInstance>();

        // The Bench units the picked-up Active may be traded for: the second half of what an Attack pick
        // offers. Deliberately not a third PickMode - the player does not announce whether they mean to attack
        // or to retreat, they pick the unit up and both lists light at once, and the click on a red or a blue
        // socket is what decides it. A mode switch here would be a gesture performed twice for one choice.
        readonly List<CardInstance> retreats = new List<CardInstance>();

        // The discard being read, and how far into it. Kept here rather than in the presenter because "which
        // card am I looking at" is dialogue state, not board state, and it has to survive a repaint.
        CardInstance[] browsed;
        string browsedName;
        int browseIndex;

        void Awake()
        {
            if (coordinator == null)
            {
                coordinator = GetComponent<GameCoordinator>();
                if (coordinator == null)
                {
                    coordinator = FindObjectOfType<GameCoordinator>();
                }
            }

            if (presenter == null)
            {
                presenter = GetComponent<BattlefieldPresenter>();
                if (presenter == null)
                {
                    presenter = FindObjectOfType<BattlefieldPresenter>();
                }
            }

            // Found rather than required, because a scene with no hand still wants a clickable board - and
            // because the guard this feeds is only ever as good as the wiring. A null hand means "nothing is
            // blocking the pointer", which is the safe default for a board with no fan on it.
            if (hand == null)
            {
                hand = coordinator != null && coordinator.handController != null
                    ? coordinator.handController
                    : FindObjectOfType<HandController>();
            }

            WireHud();
        }

        /// <summary>
        /// Subscribes to the rail's press event. The coordinator runs at execution order -100 against this
        /// component's 200, so by the time this Awake fires the HUD it binds to has already been Ensured and its
        /// rail built - a null here therefore means no HUD in the scene at all rather than a race, and the retry
        /// in <see cref="Update"/> exists for the case that actually produces one: a domain reload.
        /// <para>
        /// Only four of the rail's keys land here. The rest fire commands on the board and belong to the
        /// coordinator, which subscribes to the same event: a button presses at whoever is listening, and the
        /// split follows who owns the state the press acts on, not which object drew the button.
        /// </para>
        /// </summary>
        void WireHud()
        {
            hud = BattlefieldHud.Find();
            if (hud == null)
            {
                return;
            }

            hud.RailPressed -= OnRailPressed;
            hud.RailPressed += OnRailPressed;
        }

        void OnDestroy()
        {
            if (hud != null)
            {
                hud.RailPressed -= OnRailPressed;
            }
        }

        void OnRailPressed(BattlefieldHud.RailKey key)
        {
            switch (key)
            {
                case BattlefieldHud.RailKey.DoneBlocking:
                    DoneBlocking();
                    break;

                // The same gesture the card itself offers: pick a unit up, then ask for its skill. Clearing the
                // selection first is not cosmetic - the activation exhausts the unit and repaints its socket, and
                // a mark left pointing at a card that no longer can act is the thing DropStaleSelection exists
                // to sweep up one frame later.
                case BattlefieldHud.RailKey.Activate:
                    if (selected != null && coordinator.CanPlayerActivateWith(selected))
                    {
                        CardInstance activator = selected;
                        ClearSelection();
                        coordinator.TryPlayerActivateWith(activator);
                    }
                    break;

                case BattlefieldHud.RailKey.PilePrev:
                    StepBrowse(-1);
                    break;

                case BattlefieldHud.RailKey.PileNext:
                    StepBrowse(1);
                    break;
            }
        }

        void Update()
        {
            if (presenter == null || coordinator == null)
            {
                return;
            }

            // A match that has not booted yet, or one that has ended, has no moves to offer. The details panel
            // is left alone so a player who conceded can still read what was on the table.
            if (coordinator.GetMatchState() == null)
            {
                return;
            }

            // A script recompile while the game is running wipes this non-serialized reference and every event
            // subscription with it, and Awake is not called again. Without the retry the rail's dialogue keys
            // would go on being drawn and stop answering, so recovery costs one FindObjectsOfType on the frame
            // after the reload rather than one per frame.
            if (hud == null)
            {
                WireHud();
            }

            DropStaleSelection();

            Hover();

            if (Input.GetKeyDown(cancelKey))
            {
                // Cancel means "put the unit down", not "give up my blocks". The window is closed by Enter, or
                // by its timeout: a player who hits Escape reflexively still has their answer to give, which is
                // the same reasoning that keeps an off-table misclick from closing it in Click().
                ClearSelection();
                ClearBrowse();
                presenter.ClearDetails();
            }
            else if (Input.GetKeyDown(doneBlockingKey))
            {
                DoneBlocking();
            }

            BrowsePile();
            PublishDialogueRail();

            if (Input.GetMouseButtonDown(0) && !IsHandBusy()
                && !presenter.IsPointerOverInspect(Input.mousePosition))
            {
                Click();
            }
        }

        /// <summary>
        /// Lets the opponent's attacks land instead of waiting out the window. The body of the Enter key and of
        /// the rail's DONE BLOCKING button, and both share its refusal: outside the window there is nothing to
        /// be done early, and saying so would be a notice about a thing the player did not do.
        /// </summary>
        void DoneBlocking()
        {
            if (coordinator == null || !coordinator.PlayerBlockWindowOpen)
            {
                return;
            }

            ClearSelection();
            coordinator.CloseBlockWindow();
        }

        /// <summary>
        /// Publishes the rail keys that act on dialogue state rather than on the board: the picked-up unit's
        /// skill, and the two steps through a pile the reader is open on. Derived every frame from the same
        /// queries the click path asks, so a button and the gesture it duplicates disagree only if this file
        /// stops asking the coordinator.
        /// </summary>
        void PublishDialogueRail()
        {
            if (hud == null)
            {
                return;
            }

            hud.SetRail(BattlefieldHud.RailKey.Activate,
                selected != null && coordinator.CanPlayerActivateWith(selected));

            // One card in a pile has nowhere to step to, so the pair leaves rather than sitting dimmed.
            bool browsing = browsed != null && browsed.Length > 1;
            hud.SetRail(BattlefieldHud.RailKey.PilePrev, browsing);
            hud.SetRail(BattlefieldHud.RailKey.PileNext, browsing);
        }

        bool IsHandBusy()
        {
            // The field first: Find() is a FindObjectsOfType and this runs on every frame a card is held.
            BattlefieldHud controls = hud != null ? hud : BattlefieldHud.Find();
            return (controls != null && controls.IsPointerOverControls(Input.mousePosition))
                || (hand != null && hand.IsPointerOverHand());
        }

        /// <summary>
        /// Releases a picked-up unit the moment it stops being able to do what it was picked up for. A unit
        /// that exhausted itself, died, or was promoted off its Active socket all end up here, and the
        /// selection drops rather than lingering on a socket that no longer means anything.
        /// </summary>
        void DropStaleSelection()
        {
            if (selected == null)
            {
                return;
            }

            bool stillValid = mode == PickMode.Attack
                ? coordinator.CanPlayerAttackWith(selected)
                : coordinator.PlayerBlockWindowOpen && coordinator.CanPlayerBlockWith(selected);

            if (!stillValid)
            {
                ClearSelection();
            }
        }

        // ---- Hover ---------------------------------------------------------------------

        /// <summary>
        /// Sheens the tile under the pointer when it is something the player could act on. Deliberately subtle,
        /// and deliberately not a rules verdict: it answers "is there a clickable thing here", which is why it
        /// consults the same queries the click does instead of keeping its own notion of affordance.
        /// </summary>
        void Hover()
        {
            if (IsHandBusy())
            {
                presenter.SetHover(null);
                return;
            }

            BoardHit hit = presenter.FindHitAt(Camera.main, Input.mousePosition);
            presenter.SetHover(hit != null && IsActionable(hit) ? hit.Tile : null);
        }

        bool IsActionable(BoardHit hit)
        {
            switch (hit.Kind)
            {
                case BoardHitKind.DeckPile:
                case BoardHitKind.DiscardPile:
                case BoardHitKind.Plate:
                case BoardHitKind.PlayerCore:
                    return true;

                case BoardHitKind.OpponentCore:
                    // Only while an attacker is picked up: otherwise it is a counter to read, not a target,
                    // and Inspect handles the read.
                    return mode == PickMode.Attack;

                case BoardHitKind.Socket:
                case BoardHitKind.Protocol:
                    // A card is worth reading, and an empty socket of ours is worth dropping into; the
                    // opponent's empties are neither, so they stay dull under the pointer.
                    return hit.Card != null || hit.IsOwnSocket;

                default:
                    return false;
            }
        }

        // ---- Click ---------------------------------------------------------------------

        void Click()
        {
            HandleBoardClick(presenter.FindHitAt(Camera.main, Input.mousePosition));
        }

        // Also used by runtime tests so they exercise the same action precedence as a pointer click.
        void HandleBoardClick(BoardHit hit)
        {
            if (hit == null)
            {
                // Off the table. Cancel the selection and the reader, but leave the opponent's block window
                // alone: a player who missed the board by a pixel has not decided to give up their blocks.
                ClearSelection();
                ClearBrowse();
                presenter.ClearDetails();
                return;
            }

            // 1. Setup owns the click while it is open. The only thing on the table a player can act on is a
            //    Unit they have nominated and changed their mind about; picking a fighter up, committing a
            //    block and reading a socket all wait until the lineup is confirmed, because none of them have
            //    anything to act on yet - no body is on the board and no turn has been taken.
            if (coordinator.IsSetupInProgress)
            {
                if (hit.Card != null && hit.Owner == PlayerIndex.Player)
                {
                    coordinator.ReturnLineupNomination(hit.Card);
                }
                else if (hit.Card != null)
                {
                    Inspect(hit);
                }
                return;
            }

            // 2. An in-flight selection gets first refusal, so a lit socket is always actionable from the
            //    keyboard-free path and never silently falls through to "show me this card instead".
            if (selected != null && mode == PickMode.Block && TryCommitBlock(hit))
            {
                return;
            }

            if (selected != null && mode == PickMode.Attack && TryCommitAttack(hit))
            {
                return;
            }

            // 3. Picking a unit up.
            if (TrySelect(hit))
            {
                presenter.ShowCard(hit.Card, coordinator.GetMatchState(), hit.Describe());
                return;
            }

            // 4. Nothing to do, so read the table instead.
            Inspect(hit);
        }

        bool TrySelect(BoardHit hit)
        {
            if (hit.Card == null)
            {
                return false;
            }

            string name = hit.Card.Definition.Name;

            if (coordinator.PlayerBlockWindowOpen && coordinator.CanPlayerBlockWith(hit.Card))
            {
                List<CardInstance> blockable = coordinator.AttackersBlockableBy(hit.Card);
                if (blockable.Count == 0)
                {
                    // Legal to commit, useless to commit: nothing in its battlefield is swinging. Rather than
                    // lighting a target list that is empty, fall through to the card's details and say why.
                    coordinator.ShowNotice($"{name} is ready, but no enemy in its battlefield is attacking.");
                    Inspect(hit);
                    return true;
                }

                Pick(hit.Card, blockable, PickMode.Block);
                coordinator.ShowNotice($"{name} is ready to block. Click the attacker it should meet, " +
                    "or Enter to let them swing.");
                return true;
            }

            if (coordinator.CanPlayerAttackWith(hit.Card))
            {
                Pick(hit.Card, coordinator.TargetsForPlayerAttack(hit.Card), PickMode.Attack,
                    coordinator.RetreatChoicesForPlayer(hit.Card));
                coordinator.ShowNotice(AttackPickNotice(name, hit.Card));
                return true;
            }

            return false;
        }

        /// <summary>
        /// The single place a unit is picked up: state first, marks second, so the lit sockets are always
        /// derived from the same lists the next click will be judged against.
        /// </summary>
        void Pick(CardInstance card, List<CardInstance> list, PickMode forMode,
            List<CardInstance> retreatChoices = null)
        {
            selected = card;
            mode = forMode;
            targets.Clear();
            if (list != null)
            {
                targets.AddRange(list);
            }

            retreats.Clear();
            if (retreatChoices != null)
            {
                retreats.AddRange(retreatChoices);
            }

            ClearBrowse();

            if (forMode == PickMode.Attack)
            {
                presenter.ShowAttack(card, targets, true, retreats);
            }
            else
            {
                presenter.ShowBlockPick(card, targets);
            }
        }

        /// <summary>
        /// One line saying what the picked-up unit can still be made to do. Built from clauses rather than
        /// written out per case, because the three are independent - a unit can have attack targets, a printed
        /// skill, somewhere to retreat, and any subset of that list, and four hardcoded sentences get one of
        /// them wrong the first time a card has a skill and nowhere to retreat.
        /// <para>
        /// The retreat clause carries its price, because the price is the reason anybody declines it, and it
        /// appears at all only when the list is non-empty - which <see cref="CombatLegality.RetreatChoices"/>
        /// already filtered on affordability. A blue socket is therefore never a promise the NP balance cannot
        /// keep, which is the same reason the rail dims an unaffordable REROLL instead of lying about it.
        /// </para>
        /// </summary>
        string AttackPickNotice(string name, CardInstance card)
        {
            string moves = targets.Count > 0
                ? $"{name} is selected. Click a red socket, or the opponent's Core, to attack."
                : $"{name} has nothing to attack. Click the opponent's Core instead.";

            if (retreats.Count > 0)
            {
                moves += $" Click a blue socket to trade places for {MatchState.RetreatNP} NP.";
            }

            if (coordinator.CanPlayerActivateWith(card))
            {
                moves += " Click the same card to use its SKILL.";
            }

            return moves;
        }

        bool TryCommitAttack(BoardHit hit)
        {
            CardInstance attacker = selected;

            // A second click on the selected card chooses its printed Activate skill directly from the card.
            // A different target keeps the normal attack flow, so the interaction remains point-and-click:
            // click the unit, then either click the same unit for Skill or an enemy for Attack.
            if (hit.Card == attacker && coordinator.CanPlayerActivateWith(attacker))
            {
                ClearSelection();
                coordinator.TryPlayerActivateWith(attacker);
                return true;
            }

            if (hit.Kind == BoardHitKind.OpponentCore)
            {
                ClearSelection();
                coordinator.TryPlayerAttackWith(attacker, null);
                return true;
            }

            // A blue socket is a trade rather than a strike: the picked-up unit goes back to the Bench and the
            // bench unit takes its place, both of them exhausted. It is the same gesture as an attack and a
            // different command, which is why it is judged against its own list rather than folded into
            // targets - the two lists point at opposite halves of the table and only one of them is legal to
            // hit, so merging them would need a rule to unmerge them again on the way out.
            if (hit.Card != null && retreats.Contains(hit.Card))
            {
                CardInstance outgoing = attacker;
                ClearSelection();
                coordinator.TryPlayerRetreatWith(outgoing, hit.Card);
                return true;
            }

            if (hit.Card == null || !targets.Contains(hit.Card))
            {
                return false;
            }

            CardInstance target = hit.Card;
            ClearSelection();
            coordinator.TryPlayerAttackWith(attacker, target);
            return true;
        }

        bool TryCommitBlock(BoardHit hit)
        {
            if (hit.Card == null || !targets.Contains(hit.Card))
            {
                return false;
            }

            CardInstance blocker = selected;
            CardInstance attacker = hit.Card;
            ClearSelection();
            coordinator.TryPlayerBlockWith(blocker, attacker);

            // Committing one block does not close the window. A player with two units in two contested
            // battlefields has two answers to give, and ending the window on the first would silently take
            // the second away. The remaining blockers are re-offered from the rules rather than from what was
            // lit a moment ago, so a blocker spent on this commit cannot light again.
            if (coordinator.PlayerBlockWindowOpen)
            {
                presenter.ShowBlocks(coordinator.ReadyBlockers(), null);
                coordinator.ShowNotice($"{blocker.Definition.Name} stands in front of " +
                    $"{attacker.Definition.Name}. Enter when you are done blocking.");
            }

            return true;
        }

        // ---- Inspection -----------------------------------------------------------------

        /// <summary>
        /// Reads the table out to the player: a card's full text, a pile's contents, a Node's bonus, a Core's
        /// durability. This is the fallback for every click that is not a move, which is what makes the board
        /// explorable without a tutorial.
        /// </summary>
        void Inspect(BoardHit hit)
        {
            switch (hit.Kind)
            {
                case BoardHitKind.DeckPile:
                    presenter.ShowDeck(coordinator.GetMatchState().Players[0].Deck.Count);
                    ClearBrowse();
                    return;

                case BoardHitKind.DiscardPile:
                    OpenBrowse(coordinator.GetMatchState().Players[0].Discard, "DISCARD");
                    return;

                case BoardHitKind.Plate:
                    ShowNode(hit.NodeIndex);
                    return;

                case BoardHitKind.PlayerCore:
                    ShowCore(PlayerIndex.Player);
                    return;

                case BoardHitKind.OpponentCore:
                    ShowCore(PlayerIndex.Opponent);
                    return;

                case BoardHitKind.Socket:
                case BoardHitKind.Protocol:
                    if (hit.Card != null)
                    {
                        presenter.ShowCard(hit.Card, coordinator.GetMatchState(), hit.Describe());
                        ClearBrowse();
                        return;
                    }

                    ShowEmptySocket(hit);
                    return;
            }
        }

        /// <summary>
        /// Opens the discard browser on the most recently spent card. The list is copied rather than held:
        /// <see cref="PlayerState.Discard"/> is live state, and a browser reading a mutating list shows the
        /// same card twice or walks off the end.
        /// </summary>
        void OpenBrowse(List<CardInstance> pile, string name)
        {
            if (pile == null || pile.Count == 0)
            {
                presenter.ShowPileEntry(name, null, 0, 0);
                ClearBrowse();
                return;
            }

            browsed = pile.ToArray();
            browsedName = name;
            // The newest discard is the end of the list, so browsing starts there and walks back in time.
            browseIndex = browsed.Length - 1;
            ShowBrowseEntry();
        }

        void ShowBrowseEntry()
        {
            if (browsed == null || browsed.Length == 0)
            {
                return;
            }

            presenter.ShowPileEntry(browsedName, browsed[browseIndex], browseIndex, browsed.Length);
        }

        void BrowsePile()
        {
            int step = 0;
            if (Input.GetKeyDown(pilePrevKey)) step = -1;
            if (Input.GetKeyDown(pileNextKey)) step = 1;
            StepBrowse(step);
        }

        /// <summary>
        /// Steps the reader through the pile it is open on. The body of the arrow keys and of the rail's two
        /// steps alike. It clamps rather than wrapping, because the ends of a pile are real ends: the oldest
        /// card spent is as far back in this match as anybody knows anything.
        /// </summary>
        void StepBrowse(int step)
        {
            if (browsed == null || browsed.Length <= 1 || step == 0)
            {
                return;
            }

            browseIndex = Mathf.Clamp(browseIndex + step, 0, browsed.Length - 1);
            ShowBrowseEntry();
        }

        void ClearBrowse()
        {
            browsed = null;
            browsedName = null;
            browseIndex = 0;
        }

        void ShowNode(int nodeIndex)
        {
            MatchState state = coordinator.GetMatchState();
            bool mine = state.Players[0].Board.HoldsNode(nodeIndex, PlayerIndex.Player);
            bool theirs = state.Players[1].Board.HoldsNode(nodeIndex, PlayerIndex.Opponent);

            // A Node is held by whoever has a unit in its Active socket, and both players have one per Node, so
            // "contested" is a real state rather than a contradiction.
            string owner = mine && theirs ? "contested" : mine ? "yours" : theirs ? "theirs" : "unheld";
            string label = $"NODE {nodeIndex + 1}";

            presenter.ShowInfo(label,
                $"{NodeBonus(nodeIndex)}\n\nHeld by: {owner}\n" +
                "A Node is held by a unit in its Active socket. Its bonus - and its count toward the three of " +
                "four that wins outright (GDD 2.1.8 Node Domination) - belongs only to the player who holds it " +
                "while the opponent does not, so a Node both sides hold pays neither.",
                label);
            ClearBrowse();
        }

        /// <summary>
        /// The four Node bonuses, in the order <see cref="Board.NodeCount"/> lays them out. Hard-coded here
        /// because the multipliers themselves are applied inside <see cref="MatchState"/> against the same
        /// fixed indices - there is no data-driven Node table to read yet, and inventing one to answer a
        /// tooltip would be a second source of truth for the same fact.
        /// </summary>
        static string NodeBonus(int nodeIndex)
        {
            switch (nodeIndex)
            {
                case 0: return "+1 ATK to your Core Active";
                case 1: return "+1 HP to your Core Active";
                case 2: return "First Strike in the Core";
                case 3: return "Draw a card on a kill";
                default: return "No bonus";
            }
        }

        void ShowCore(PlayerIndex who)
        {
            MatchState state = coordinator.GetMatchState();
            PlayerState p = state.GetPlayer(who);
            string label = who == PlayerIndex.Player ? "YOUR CORE" : "OPPONENT CORE";

            presenter.ShowInfo(label,
                $"Durability {p.CoreDurability} / {PlayerState.StartingCoreDurability}\n" +
                $"NP {p.CurrentNP} / {PlayerState.NpCap(state.CountControlledNodes(who))}\n" +
                $"Hand {p.Hand.Count}   Deck {p.Deck.Count}   Discard {p.Discard.Count}\n\n" +
                "An attack that reaches this Core unblocked costs exactly one durability, whatever the " +
                "attacker's ATK (GDD 2.1.6 Step 4). At zero, its owner loses.",
                label);
            ClearBrowse();
        }

        void ShowEmptySocket(BoardHit hit)
        {
            string hint = hit.IsOwnSocket
                ? "Drag a card here to deploy it."
                : "The opponent's socket. Nothing you can play there.";

            presenter.ShowInfo(hit.Describe(), "Empty.\n\n" + hint, hit.Describe());
            ClearBrowse();
        }

        // ---- Teardown --------------------------------------------------------------------

        /// <summary>
        /// Drops the picked-up unit and every mark with it. Called on a cancelled click, a resolved move, and a
        /// stale selection, so a lit socket never outlives the moment it was offered.
        /// </summary>
        public void ClearSelection()
        {
            selected = null;
            mode = PickMode.None;
            targets.Clear();
            retreats.Clear();
            if (presenter != null)
            {
                presenter.ClearMarks();
            }
        }

        /// <summary>The unit currently picked up to attack or block, if any.</summary>
        public CardInstance Selected => selected;

        /// <summary>What the picked-up unit may currently hit (or answer, while blocking).</summary>
        public IReadOnlyList<CardInstance> CurrentTargets => targets;

        /// <summary>Whether the current pick is an attack or a block. Diagnostic and test surface.</summary>
        public bool PickedToBlock => mode == PickMode.Block;
    }
}
