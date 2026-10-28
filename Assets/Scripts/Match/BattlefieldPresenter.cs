using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using Overlink.Cards;
using Overlink.Core;

namespace Overlink.Match
{
    /// <summary>
    /// Phase C battlefield presentation, built as real scene geometry rather than screen-space UI.
    /// <para>
    /// The board is a set of thin cube tiles (matching <c>CardView</c>'s 3D card bodies) laid out in a flat
    /// plane and viewed by a pitched, lightly perspective camera, so the match reads as an Underlords /
    /// Auto-Chess style tabletop.
    /// </para>
    /// <para>
    /// The board is <b>five distinct battlefields</b>: one shared <b>Core</b> battlefield in the middle
    /// (1 Active + 4 Bench per player) and four contested <b>Nodes</b> at the corners (1 Active + 2 Bench
    /// per player). Node 1 sits bottom-left and Node 2 bottom-right on the player's side, Node 3 top-right
    /// and Node 4 top-left on the opponent's side. A unit may attack, block and be blocked only within its
    /// own battlefield, so each region is drawn as its own plate with its own Active and Bench tiles.
    /// </para>
    /// <para>
    /// The tiles are authored in the local XY plane of this object; the scene builder rotates the
    /// <c>Match</c> root 90 degrees so they lie flat on the table. This is a <b>view only</b>: every frame it
    /// reads the authoritative <see cref="MatchState"/> that <see cref="GameCoordinator"/> owns and paints
    /// the tiles. It never mutates Core state and owns no rules. Interaction stays on the coordinator's
    /// keyboard bindings.
    /// </para>
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class BattlefieldPresenter : MonoBehaviour
    {
        [Header("Match source")]
        [Tooltip("The match owner this view observes. Found automatically when left empty.")]
        public GameCoordinator coordinator;

        [Header("Board layout (world units)")]
        [Tooltip("The screen-space panel the match log renders into. Found automatically; created on the " +
                 "first refresh when the scene has none, so the legacy standalone hand still gets a log.")]
        public BattlefieldHud hud;

        // ---- Board skin ------------------------------------------------------------
        // Five materials, one per family of surface on the table. Left empty, every body renders with the
        // generated per-colour material it always has (Assets/Materials/Battlefield, keyed by hex), which is
        // the frame the board ships in. Drop a material into a slot and every body of that family takes it -
        // both construction paths, because the family is resolved from the same name the empty colour comes
        // from, so a hand-regrouped prefab and a procedural board skin identically. See SurfaceFor.
        // <para>
        // <b>A skin multiplies; it does not replace.</b> The per-frame repaint overrides the surface colour
        // through a MaterialPropertyBlock (see SetBodyColor), so a tile's albedo below keeps tinting whatever
        // material it is handed: a stone diffuse comes out stone-shaped and board-blue. A skin meant to be
        // seen at full strength wants its family's albedo raised toward white. A material whose shader declares
        // neither _BaseColor nor _Color takes no tint at all and renders exactly as authored, which is also a
        // legitimate answer - it just means the albedos stop meaning anything for that one family.
        // </para>
        // <para>
        // The albedos are serialized for the same reason the slots are: they are the board's palette. The values
        // written here are the ones the shipped framing was measured and signed off against, and they are code
        // defaults rather than scene data - a scene or prefab saved before this field existed simply does not
        // carry the entry, so Unity leaves the initializer standing and the board looks exactly as it did.
        // Nothing else in the suite reads them, so BoardSkinTests pins them instead.
        // </para>
        // <para>
        // What is deliberately NOT here: the pointer marks and BlockMark. Those are not surfaces, they are
        // meanings - cool against warm is how a player tells "a thing of yours you may trade places with" from
        // "a thing you may hit" at a glance, across the whole table at once - and a tuning surface that lets the
        // two land on the same hue is a rules bug waiting for an artist. They stay code constants.
        // </para>

        [Header("Board skin (materials; empty = the shipped look)")]
        [Tooltip("The backdrop slab the whole board lies on.")]
        public Material tableSkin;

        [Tooltip("Every slot tile: Active, Bench and Protocol, both halves, all five battlefields.")]
        public Material socketSkin;

        [Tooltip("The plates behind the ranks - the two Core plates and the four Node plates.")]
        public Material plateSkin;

        [Tooltip("The deck and discard stacks, top card and buried layers alike.")]
        public Material pileSkin;

        [Tooltip("The rim cube behind every Active socket.")]
        public Material rimSkin;

        [Header("Board albedo (what the skins above are tinted with)")]
        [Tooltip("The backdrop slab.")]
        public Color tableTint = new Color(0.09f, 0.11f, 0.15f);

        [Tooltip("An empty Active socket - deliberately the brightest thing on the board, so a row of " +
                 "[Active][Bench][Bench] never reads as three identical slots.")]
        public Color activeEmpty = new Color(0.30f, 0.27f, 0.19f);

        [Tooltip("An empty Bench socket.")]
        public Color benchEmpty = new Color(0.13f, 0.15f, 0.19f);

        [Tooltip("An empty Protocol socket, on its own hue.")]
        public Color protocolEmpty = new Color(0.23f, 0.18f, 0.31f);

        [Tooltip("The border behind each Active tile; survives deployment, unlike the tile's own tint.")]
        public Color activeRim = new Color(0.80f, 0.64f, 0.28f);

        [Tooltip("A unit standing on a socket, in its owner's colour.")]
        public Color playerTint = new Color(0.20f, 0.42f, 0.72f);

        [Tooltip("The same, for the opponent.")]
        public Color opponentTint = new Color(0.62f, 0.24f, 0.24f);

        [Tooltip("The plate behind a Core rank. Darker than a Node plate on purpose: it sits under two full " +
                 "ranks, and at nodeFree it would compete with the socket fills stacked on top of it.")]
        public Color corePlateTint = new Color(0.16f, 0.18f, 0.25f);

        [Tooltip("A Node plate nobody controls.")]
        public Color nodeFree = new Color(0.26f, 0.28f, 0.32f);

        [Tooltip("A Node plate the player holds.")]
        public Color nodePlayer = new Color(0.14f, 0.48f, 0.28f);

        [Tooltip("A Node plate the opponent holds.")]
        public Color nodeOpponent = new Color(0.56f, 0.38f, 0.12f);

        [Tooltip("The deck stack: a card back, cooler and darker than any empty socket, so a face-down stack " +
                 "can never be mistaken for somewhere a card can be dropped.")]
        public Color deckTint = new Color(0.13f, 0.19f, 0.25f);

        [Tooltip("The spent pile, warmer and greyer still.")]
        public Color discardTint = new Color(0.21f, 0.17f, 0.14f);

        // ---- Board cards ------------------------------------------------------------
        // A unit standing on a socket used to be drawn as a name and three numbers painted onto a tinted cube,
        // while the very same card in the fan was drawn as a framed card with art, cost, ability text and its
        // rarity materials. One object, two appearances, and a player re-reading every unit from scratch each
        // time it leaves the hand. These four slots are the hand's own catalogs, wired to the same assets, so a
        // socket shows the card it is: the same frame, the same art window, the same live ATK/HP/Shield the
        // engine holds, refreshed by CardView.Refresh rather than by a second copy of the drawing rules here.
        // <para>
        // Left empty the board paints the caption cube exactly as it shipped. That is not a fallback kept for
        // form's sake: every test rig and every editor preview in the project builds a board without catalogs,
        // and the label path is what those depend on.
        // </para>
        // <para>
        // What the card deliberately does not replace: the tile's own colour. Owner tint, pointer marks, the
        // pending-block shading and the gold rim all still live on the cube, and the card is sized to cover
        // slightly less than the socket so they keep reading around it. Those colours say what a click would do;
        // the card says what the thing is. Neither is tunable into the other on purpose.
        // </para>

        [Header("Board cards (the hand's own catalogs; empty = the shipped caption tiles)")]
        [Tooltip("Frame art catalog. Drop the same asset the HandController carries and a socket draws the " +
                 "same frame the fan does.")]
        public CardFrameCatalog frameCatalog;

        [Tooltip("Card prefab catalog, keyed by card type and rarity.")]
        public CardTemplateCatalog templateCatalog;

        [Tooltip("Rarity material set: the shaders and textures a card's frame and art draw through.")]
        public CardTierMaterialSet tierMaterials;

        [Tooltip("Face used for a card with no bound definition. A socket always has a real card under it, so " +
                 "this only ever matters to an editor preview of a board with no match behind it.")]
        public CardPresentationFixture placeholderFixture;

        [Header("Board card fit")]
        [Tooltip("How much of the socket a card covers, 1 being edge to edge. Under 1 on purpose: the tile's own " +
                 "colour and its gold rim are what say whose unit this is and whether a click would land on it, " +
                 "and a card laid edge to edge swallows both.")]
        [Range(0.5f, 1f)]
        public float boardCardFill = 0.92f;

        [Tooltip("How far the card floats off the tile, in board units, along the axis that faces the camera " +
                 "(local -Z, the direction the caption already sits in front of the cube). Keep it under the " +
                 "caption's own 0.004, or the card stands in front of the one line that has to read over it.")]
        public float boardCardLift = 0.002f;

        [Tooltip("Rotation applied to every board card on top of the pose its own socket's caption uses. Zero " +
                 "means a card lies exactly as the tile label does, which is the one orientation proven readable " +
                 "on this table. Change it only if the board is re-posed under you and the cards land face-down " +
                 "or mirrored.")]
        public Vector3 boardCardExtraRotation;

        // ---- Tabletop -------------------------------------------------------------
        // The tabletop is sized to its content and nothing else. Since the status layer moved to
        // BattlefieldHud the widest content is still the pair of outer Node plates (x = +/-6.9) and the
        // tallest is the same plates plus the two piles, spanning y -4.85..+4.5, so 10.3 deep with the
        // centre at -0.2 hugs the board with a deliberate margin and nothing more.
        //
        // Public because the camera's overhead tier is defined by these numbers: it exists to frame exactly
        // this rectangle, and its test reads them rather than copying them so the two cannot drift apart.
        public const float TableWidth = 16.5f;
        public const float TableDepth = 10.3f;
        public const float TableCenterY = -0.2f;

        // Name of the child this component paints. The scene ships it as a prefab instance so the board
        // is a real, editable hierarchy object; BuildBoard() binds to it by name rather than rebuilding.
        const string BoardRootName = "Battlefield";

        // Every slot tile is a 5:7 portrait footprint, matching the hand cards, so the board reads as
        // empty card sockets rather than flat bars. Each battlefield fields TWO ranks per player and the
        // two halves are MIRRORED about the centre line: the Active is always the INNER rank, facing the
        // opponent across the middle of the table and drawn larger, with the Bench rank behind it toward
        // its owner's own edge. Screen order, bottom to top, is therefore always:
        //   player Bench -> player Active -> opponent Active -> opponent Bench.
        // ---- Core battlefield (centre): Active + Protocol in the front rank, four Bench behind. ----
        // The Core Active is the focal socket of the whole board, so it is deliberately the largest tile on
        // the table and sits on the centre line (x = 0) rather than being pushed aside for the Protocol,
        // which keeps its own Bench-sized footprint beside it. The Bench rank is pushed correspondingly
        // further back so the taller Active never crowds it and the caption in the dead band between the
        // two facing Actives keeps its own clear space.
        const float CorePlayerActiveY = -1.12f;

        // Public for the same reason TableWidth is: the resting camera framing is derived from where this row
        // lands on screen, and BattlefieldCameraControllerTests pins the seam between it and the hand fan.
        public const float CorePlayerBenchY = -2.5f;
        const float CoreOpponentActiveY = 1.12f;
        const float CoreOpponentBenchY = 2.5f;
        const float CoreSpacing = 0.9f;
        // Clears the wider Active: half its width (0.55) + half the Protocol's (0.36) + a 0.14 gutter.
        const float CoreProtocolX = 1.05f;
        static readonly Vector3 CoreActiveSize = new Vector3(1.10f, 1.54f, 0.06f);
        // The back rank is a touch smaller than the Protocol socket beside the Active. Being the furthest
        // thing from the centre line it is also the furthest from the camera, and a slightly tighter
        // footprint is what leaves the status strip a clear band between here and the hand fan.
        // Public because the near edge of this row is the number the camera's rest bias is derived against.
        public static readonly Vector3 CoreBenchSize = new Vector3(0.66f, 0.924f, 0.06f);
        static readonly Vector3 CoreProtocolSize = new Vector3(0.72f, 1.008f, 0.06f);

        // ---- The Core plate -------------------------------------------------------
        // Each half of the Core battlefield gets a plate behind its two ranks, mirroring what a Node plate
        // does for a Node. It is not decoration. With the status strips off the table it is the footprint of
        // an unblocked direct Core strike (GDD 2.1.6 Step 4), and unlike the Core Active socket it survives
        // the opponent putting a unit down, so a direct attack always has a visible place to be aimed at
        // rather than an invisible rectangle of empty tabletop or a screen-space hit into the UI.
        //
        // Centred on the two ranks it backs: the Active spans y -1.89..-0.35 and the Bench -2.96..-2.04 on
        // the player's side, so the block is 2.61 tall about -1.656, and 3.36 wide across the Bench row.
        // The plate is that plus a margin, and it is mirrored exactly about the centre line.
        //
        // Public because the targeting test probes the plate's own margin to prove it is still reachable
        // under the ranks, and copying this size into the test is how the two drift apart.
        const float CorePlayerPlateY = -1.656f;
        const float CoreOpponentPlateY = 1.656f;
        public static readonly Vector3 CorePlateSize = new Vector3(3.7f, 2.95f, 0.05f);

        // ---- Node battlefields (four corners): the same two-rank shape at a smaller scale. ----
        const float NodeCenterX = 5.5f;
        const float NodeTopY = 2.3f;
        const float NodeBottomY = -2.3f;
        const float NodePlayerActiveY = -0.72f;
        const float NodePlayerBenchY = -1.74f;
        const float NodeOpponentActiveY = 0.72f;
        const float NodeOpponentBenchY = 1.74f;
        const float NodeSpacing = 0.86f;
        static readonly Vector3 NodeActiveSize = new Vector3(0.74f, 1.036f, 0.06f);
        static readonly Vector3 NodeBenchSize = new Vector3(0.54f, 0.756f, 0.06f);
        static readonly Vector3 NodePlateSize = new Vector3(2.8f, 4.4f, 0.05f);

        /// <summary>
        /// Corner positions, indexed by the Core Node index: 0 = player bottom-left, 1 = player
        /// bottom-right, 2 = opponent top-right, 3 = opponent top-left.
        /// </summary>
        static readonly Vector2[] NodeCenter =
        {
            new Vector2(-NodeCenterX, NodeBottomY),
            new Vector2(NodeCenterX, NodeBottomY),
            new Vector2(NodeCenterX, NodeTopY),
            new Vector2(-NodeCenterX, NodeTopY),
        };

        // ---- Piles: the deck and the discard, printed onto the tabletop. ----
        // The hand's draw and discard anchors were invisible markers floating in the Match root's plane, so
        // cards appeared from nowhere and vanished into nowhere. These are those two spots as real geometry,
        // flanking the hand fan: the deck on the drawing side, the discard on the spending side. They sit
        // clear of the fan's outermost card (which reaches to 2.56 from the centre line) and of the inner
        // Node plate edges (4.1), and below the player status strip, which has no vertical room left
        // beside the Bench rank.
        // <para>
        // The scene builder seeds the anchors from these same coordinates and the presenter re-aligns them
        // once its board exists (see SyncPileAnchors), so dragging a pile in the editor takes the card
        // flight path with it instead of leaving it behind.
        public static readonly Vector2 DeckPilePosition = new Vector2(-3f, -4.35f);
        public static readonly Vector2 DiscardPilePosition = new Vector2(3f, -4.35f);
        static readonly Vector3 PileSize = new Vector3(0.72f, 1.008f, 0.04f);
        // The two pile albedos live in the Board albedo region with the rest of the palette: a pile is a
        // surface, and a surface is tunable.

        // Depth stacking. Local -Z faces the pitched camera, so a smaller Z draws on top of a larger one:
        // cards above their plates, caption text above the plates, tabletop furthest back.
        const float CardZ = 0f;
        const float PlateZ = 0.07f;
        const float LabelZ = 0.02f;
        const float TableZ = 0.14f;

        // The control legend is code-owned, not authored: a key binding added to GameCoordinator has to reach
        // the screen without anybody rebuilding a scene file or a prefab. It is written onto the HUD by
        // ResolveHud, which is the one place that knows the canvas exists.
        //
        // It does not list the rail's buttons, and it does not list the keys they replaced either: END TURN,
        // CONCEDE, MATCH STATE and the five swings say what they are on their own faces, so repeating them here
        // would spend two lines of the frame on a list the player can read off the screen faster than they can
        // read this one. What is left is what no button advertises - the drag, the pick, the read, and the two
        // camera gestures.
        // Public because the HUD's ink test measures the real strings rather than a copy that can drift: the
        // legend's own width is what its box has to hold.
        public const string ControlsHintText =
            "Drag onto a lit socket to deploy (MAIN)   \u2022   Click a Ready unit, then a red socket to attack " +
            "or a blue one to retreat\n" +
            "Click any card to read it   \u2022   ESC cancel   \u2022   \u2190/\u2192 browse   \u2022   Wheel zoom   \u2022   RMB pan";

        // The legend setup needs, which is a different job from the one above: nothing on the table is a
        // command during the opening, the only drag that means anything is a nomination, and the two buttons
        // that close setup are the only interactive things on the screen.
        public const string SetupHintText =
            "Drag a card onto a lit socket to field it   \u2022   Click a fielded card to send it back\n" +
            "REROLL replaces all five offers for NP   \u2022   CONFIRM opens the match";

        static readonly string[] NodeCaption =
        {
            "NODE 1  +1 ATK",
            "NODE 2  +1 HP",
            "NODE 3  FIRST STRIKE",
            "NODE 4  DRAW ON KILL",
        };

        // The board's palette - owner tints, empty-slot fills, plate and pile albedos - is serialized in the
        // Board albedo region near the top of the class, because every one of them is a surface a skin drop
        // should be able to recolour rather than a constant somebody has to recompile to change. What follows
        // is the other half of the colour inventory, and it stays code-owned on purpose: these are not
        // surfaces, they are statements.

        // A unit that is blocking this turn. Yellow because it is neither owner tint nor pointer mark and has
        // to be readable over both at once.
        static readonly Color BlockMark = new Color(1f, 0.84f, 0.20f);

        // Owner tints for the HUD's node list. Brightened from the plate albedos above: nodePlayer and
        // nodeOpponent are cube colours meant to sit under the board light, and as 21pt text on a dark slab
        // they read as mud.
        static readonly Color RowNeutral = new Color(0.58f, 0.65f, 0.77f);
        static readonly Color RowPlayer = new Color(0.45f, 0.95f, 0.62f);
        static readonly Color RowOpponent = new Color(1f, 0.72f, 0.35f);

        // Pointer marks. Chosen to be distinguishable from the owner tints they are drawn over - blue for the
        // player's own units, warm red/orange for the enemy's - so "I picked this" and "I could hit this"
        // never read as the same shade on opposite halves of the table.
        static readonly Color SelectMark = new Color(0.45f, 0.95f, 1.00f);
        static readonly Color AttackMark = new Color(1.00f, 0.42f, 0.32f);
        static readonly Color BlockerMark = new Color(0.42f, 0.95f, 0.55f);
        static readonly Color BlockTargetMark = new Color(1.00f, 0.72f, 0.30f);

        // The second thing a picked-up Active unit may do, on the player's own half, in the same cool family as
        // the selection it hangs off: red sockets are things to hit and blue sockets are things of yours you may
        // trade places with. Cool-versus-warm is the distinction the whole board is drawn on, and a retreat mark
        // in any of the enemy hues would put two legal clicks on the same table reading as the same verb.
        static readonly Color RetreatMark = new Color(0.42f, 0.62f, 1.00f);

        // A nominated Unit standing on a socket during the lineup setup. Washed toward white and away from the
        // owner tint it will become, because it is a promise rather than a body: the socket is still empty as
        // far as the rules are concerned, and a preview painted in full owner colour would read as deployed.
        static readonly Color PendingMark = new Color(0.80f, 0.86f, 0.98f);

        Transform boardRoot;

        // Core battlefield tiles.
        Tile[] playerCoreActive, playerCoreBench;
        Tile[] opponentCoreActive, opponentCoreBench;
        Tile playerProtocol, opponentProtocol;

        // The plate behind each Core rank. See the layout note at CorePlayerPlateY.
        Tile playerCorePlate, opponentCorePlate;

        // Node battlefield tiles, [node][slot].
        Tile[] nodePlate;
        Tile[][] nodePlayerActive, nodePlayerBench;
        Tile[][] nodeOpponentActive, nodeOpponentBench;

        // The deck and discard stacks. Not sockets: they carry no slot identity and are never probed as drop
        // targets, because nothing is played TO the deck or the discard from the hand.
        Tile deckPile, discardPile;

        Tile highlightedTile;

        // The player's opening nomination, published by the coordinator while the lineup setup is open: one
        // offered card id per Active socket in battlefield order (the Core rank, then each Node's Active), and
        // 0 for a socket the player has not filled. Front-end state rather than rules on purpose - the engine
        // holds no partial nomination and ConfirmLineupCommand is the only way to submit one - and serialized
        // like the coordinator's boot flag, because a hot reload mid-setup otherwise loses the board the
        // preview of what the player has already chosen.
        [SerializeField] int[] lineupPreview = new int[0];

        void Start()
        {
            if (coordinator == null)
            {
                coordinator = FindObjectOfType<GameCoordinator>();
            }

            ResolveHud();
            BuildBoard();
        }

        /// <summary>
        /// Binds the screen-space HUD. The gameplay scene ships one as an editable root object; when
        /// it is missing (the legacy standalone hand, a bare test scene) one is created &mdash; but only in play
        /// mode, because <see cref="BuildBoard"/> also runs from the editor prefab build, where creating a
        /// scene root would leave a stray object behind.
        /// </summary>
        void ResolveHud()
        {
            if (hud == null)
            {
                hud = BattlefieldHud.Find();
            }

            if (hud == null && Application.isPlaying)
            {
                hud = BattlefieldHud.Ensure();
            }

            // A HUD serialized before the status layer existed is a valid, complete log panel that simply has
            // no banner, chips or node list on it. Building it in place is idempotent and re-binds every child
            // by name, so the authored object survives and only the missing panels are added. Guarded to play
            // mode for the same reason as Ensure(): an editor build must not grow scene objects.
            if (hud != null && Application.isPlaying &&
                (!hud.IsBuilt || !hud.IsReaderBuilt || !hud.IsStatusBuilt))
            {
                hud.Build();
            }

            // The legend is code-owned, not authored: a key binding added to GameCoordinator has to reach the
            // screen without anybody rebuilding a scene file. Refresh swaps it for the setup legend while the
            // opening lineup is being chosen.
            SetHint(ControlsHintText);
        }

        void LateUpdate()
        {
            // A domain reload while playing (an editor script hot-reload) wipes every non-serialized
            // field, including these tile bindings, and Start() is not called again. BuildBoard()
            // re-binds by name without duplicating any geometry, so the presenter recovers instead of
            // throwing a NullReferenceException on every frame.
            if (playerCoreActive == null)
            {
                // A domain reload also drops the HUD reference on any board that was built procedurally
                // rather than shipped by the scene, so it is re-resolved here alongside the tile bindings.
                ResolveHud();
                BuildBoard();
                if (playerCoreActive == null)
                {
                    return;
                }
            }

            // A skin dropped in the Inspector, or moved by a script mid-match, lands on the next frame
            // rather than on the next board build. Cheap when nothing moved: five reference reads.
            ApplySkinsIfChanged();

            Refresh();
        }

        // ---- Construction -----------------------------------------------------------

        /// <summary>
        /// Builds the board geometry. If a <c>Battlefield</c> child already exists (the prefab instance
        /// the scene ships with) the tiles are re-<em>bound</em> by name instead of rebuilt, so the
        /// hierarchy you can edit in the editor is exactly what gets painted at runtime. Only when nothing
        /// is there does it construct from scratch.
        /// </summary>
        public void BuildBoard()
        {
            if (boardRoot != null)
            {
                return;
            }

            Transform existing = transform.Find(BoardRootName);
            if (existing != null && TryBindBoard(existing))
            {
                boardRoot = existing;
                pickablesValid = false;
                SyncPileAnchors();
                return;
            }

            // A stale or partial hierarchy is worse than none: clear it and rebuild deterministically.
            if (existing != null)
            {
                DestroyTarget(existing.gameObject);
            }

            boardRoot = new GameObject(BoardRootName).transform;
            boardRoot.SetParent(transform, false);
            pickablesValid = false;

            // Tabletop backdrop.
            MakeBody(boardRoot, "Table", new Vector3(0f, TableCenterY, TableZ),
                new Vector3(TableWidth, TableDepth, 0.05f), tableTint);

            // Everything that used to be painted here — the turn header, the key legend, the two status
            // strips, the victory banner and the notice line — now lives on BattlefieldHud's overlay canvas.
            // World text on a 52 degree pitch foreshortens, shrinks with zoom, and eats two bands of table
            // that the tiles themselves could use; see BattlefieldHud.EnsureStatus.

            BuildCoreBattlefield();
            BuildNodeBattlefields();
            BuildPiles();

            SyncPileAnchors();
        }

        /// <summary>
        /// Re-resolves every tile and label from an already-built hierarchy. Returns false if the
        /// hierarchy is missing or incomplete, in which case the caller rebuilds from scratch.
        /// </summary>
        bool TryBindBoard(Transform root)
        {
            boardRoot = root;
            bindFailed = false;

            // Binding mints a fresh set of Tile records, and a pooled card hangs off the record that created it.
            // Dropping the old ones here is what stops a re-bind from leaving last session's faces parented to
            // tiles nobody is going to paint again.
            ClearBoardCards();

            // The status layer moved onto BattlefieldHud's overlay canvas. A board authored before that change
            // still carries the old world labels, and binding resolves children by name — so unless they are
            // deleted here they keep painting stale strings straight over the new HUD. Pruned before the name
            // index is built, so nothing downstream can resolve them at all.
            PurgeRetiredTabletop(root);

            IndexHierarchy(root);

            int activeCount = Board.CoreActiveSlots;
            int benchCount = Board.CoreBenchSlots;

            playerCoreActive = new Tile[activeCount];
            playerCoreBench = new Tile[benchCount];
            opponentCoreActive = new Tile[activeCount];
            opponentCoreBench = new Tile[benchCount];

            for (int i = 0; i < activeCount; i++)
            {
                opponentCoreActive[i] = FindTile($"OppCoreActive{i}");
                playerCoreActive[i] = FindTile($"PlayCoreActive{i}");
            }

            opponentProtocol = FindTile("OppProtocol");
            playerProtocol = FindTile("PlayProtocol");

            opponentCorePlate = FindTile("OppCorePlate");
            playerCorePlate = FindTile("PlayCorePlate");

            for (int i = 0; i < benchCount; i++)
            {
                opponentCoreBench[i] = FindTile($"OppCoreBench{i}");
                playerCoreBench[i] = FindTile($"PlayCoreBench{i}");
            }

            nodePlate = new Tile[Board.NodeCount];
            nodePlayerActive = new Tile[Board.NodeCount][];
            nodePlayerBench = new Tile[Board.NodeCount][];
            nodeOpponentActive = new Tile[Board.NodeCount][];
            nodeOpponentBench = new Tile[Board.NodeCount][];

            for (int n = 0; n < Board.NodeCount; n++)
            {
                nodePlate[n] = FindTile($"NodePlate{n}");
                nodeOpponentActive[n] = new Tile[Board.NodeActiveSlots];
                nodeOpponentBench[n] = new Tile[Board.NodeBenchSlots];
                nodePlayerActive[n] = new Tile[Board.NodeActiveSlots];
                nodePlayerBench[n] = new Tile[Board.NodeBenchSlots];

                for (int i = 0; i < Board.NodeActiveSlots; i++)
                {
                    nodeOpponentActive[n][i] = FindTile($"NodeOppActive{n}_{i}");
                    nodePlayerActive[n][i] = FindTile($"NodePlayActive{n}_{i}");
                }

                for (int i = 0; i < Board.NodeBenchSlots; i++)
                {
                    nodeOpponentBench[n][i] = FindTile($"NodeOppBench{n}_{i}");
                    nodePlayerBench[n][i] = FindTile($"NodePlayBench{n}_{i}");
                }
            }

            deckPile = FindTile("DeckPile");
            discardPile = FindTile("DiscardPile");

            if (bindFailed || root.Find("Table/Body") == null)
            {
                return false;
            }

            // A board that already exists carries its own materials, and the skin slots are read at
            // construction - so without this pass a skin dropped on a bound scene would do nothing at all,
            // which is the one workflow the slots exist for.
            ApplySkins();
            return true;
        }

        /// <summary>
        /// Names that used to be painted onto the tabletop and now live on <see cref="BattlefieldHud"/>.
        /// <para>
        /// Binding resolves descendants by name, so an authored board that still carries these objects would
        /// keep rendering them straight through the new HUD — two turn counters, two legends, and a victory
        /// banner neither writer can clear. Deleting them on bind makes the migration self-healing: the same
        /// code path serves a freshly built board, the re-authored prefab, and any stale copy still sitting
        /// in a scene file.
        /// </para>
        /// </summary>
        static readonly string[] RetiredTabletopNames =
        {
            "Header", "ControlsHint", "Notice", "Victory", "OStatus", "PStatus", "Status",
            "NodeHeader0", "NodeHeader1", "NodeHeader2", "NodeHeader3",
        };

        void PurgeRetiredTabletop(Transform root)
        {
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == root || all[i] == null)
                {
                    continue;
                }

                for (int r = 0; r < RetiredTabletopNames.Length; r++)
                {
                    if (all[i].name != RetiredTabletopNames[r])
                    {
                        continue;
                    }

                    // "Status" is the group that held the two strips, and it is named the same as nothing the
                    // tiles use, so taking the group takes the strips with it. Guard against a component that
                    // was already destroyed by an earlier iteration of the outer loop.
                    DestroyTarget(all[i].gameObject);
                    break;
                }
            }
        }

        /// <summary>
        /// Set once any expected child fails to resolve, so a partially-matching hierarchy is rejected
        /// rather than silently painting only the tiles that happened to be found.
        /// </summary>
        bool bindFailed;

        /// <summary>
        /// Every uniquely named descendant of the board root, so binding reaches tiles inside the Core,
        /// Node and rank groups without any hard-coded path. Grouping is therefore a presentation choice
        /// that the runtime cannot break by accident: rename a group, reparent a rank, move one anywhere,
        /// and the tiles still bind by their own names.
        /// <para>
        /// A name held by more than one object is recorded as null rather than first-wins, because a board
        /// carrying two tiles of the same name is a board whose drop targets cannot be trusted. Ambiguity
        /// fails the bind, and <see cref="BuildBoard"/> then rebuilds it deterministically.
        /// </para>
        /// </summary>
        Dictionary<string, Transform> nameIndex;

        void IndexHierarchy(Transform root)
        {
            nameIndex = new Dictionary<string, Transform>(64);

            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i];
                if (t == root)
                {
                    continue;
                }

                if (nameIndex.ContainsKey(t.name))
                {
                    nameIndex[t.name] = null;
                }
                else
                {
                    nameIndex.Add(t.name, t);
                }
            }
        }

        /// <summary>
        /// Resolves a board object by name at any depth. Falls back to a direct child lookup when no index
        /// exists, which is the case for a board built procedurally in this session.
        /// </summary>
        Transform FindNamed(string name)
        {
            if (nameIndex != null
                && nameIndex.TryGetValue(name, out Transform indexed)
                && indexed != null)
            {
                return indexed;
            }

            return boardRoot != null ? boardRoot.Find(name) : null;
        }

        Tile FindTile(string name)
        {
            Transform slot = FindNamed(name);
            // Body and Label are children of the tile itself, so moving one socket in the editor carries its
            // cube, its gold rim and its caption along with it.
            Transform body = slot != null ? slot.Find("Body") : null;
            Transform labelNode = slot != null ? slot.Find("Label") : null;
            Renderer renderer = body != null ? body.GetComponent<Renderer>() : null;
            var label = labelNode != null ? labelNode.GetComponent<TextMeshPro>() : null;

            if (renderer == null || label == null)
            {
                bindFailed = true;
                return null;
            }

            var tile = new Tile
            {
                Body = renderer,
                Label = label,
                Name = name,
                // The cube's own local scale is the authored footprint on this path, and it is what a card
                // standing on the socket gets fitted inside. The holder carries no scale of its own (MakeTile
                // puts the non-uniform size on the cube child), so reading the cube is reading the tile.
                Size = body.localScale,
                // Derived from the slot's NAME, never read back off its material. The prefab cannot be
                // relied on to carry a per-tile colour (inline Materials are not assets, so a prefab save
                // collapses them onto one shared material), but the name always identifies the slot kind,
                // and that is exactly what the build path used to pick the colour in the first place.
                BaseColor = BaseColorForSlot(name),
                EmptyLabel = label.text,
            };
            ApplyCaptionSorting(label);
            ApplyTileIdentity(tile, name);
            return tile;
        }

        /// <summary>
        /// Pins a tile to the Core <see cref="BoardSlot"/> it renders, keyed off its name, so the build path
        /// and the prefab-bind path agree exactly (the same trick <see cref="BaseColorForSlot"/> uses for the
        /// colour). This is what makes a drag-and-drop target resolvable.
        /// <para>
        /// Only the player's own hosting sockets are tagged. Opponent tiles, Node plates and the Protocol
        /// tiles are never drop targets, so they keep the default identity and are simply not probed.
        /// </para>
        /// </summary>
        static void ApplyTileIdentity(Tile tile, string name)
        {
            if (tile == null)
            {
                return;
            }

            if (TryParseSlotIdentity(name, out SlotType type, out int index, out int nodeIndex))
            {
                tile.SlotType = type;
                tile.SlotIndex = index;
                tile.NodeIndex = nodeIndex;
            }

            if (TryParseTileOwnership(name, out PlayerIndex owner, out TileRole role, out int ownedNode))
            {
                tile.Owner = owner;
                tile.Role = role;
                // A plate is the only recognised object whose node index the drop contract never fills in,
                // so borrow it rather than parsing the name twice.
                if (role == TileRole.Plate)
                {
                    tile.NodeIndex = ownedNode;
                }
            }
        }

        /// <summary>
        /// The name-keyed identity of <b>every</b> recognised tile on the table, for clicking. Broader than
        /// <see cref="TryParseSlotIdentity"/>, which deliberately names only the player's own deployable
        /// sockets: a drag may target four objects, whereas a click is meaningful on the opponent's ranks,
        /// both Protocol sockets, the Node plates and the two piles too.
        /// <para>
        /// Keyed off names rather than off the hierarchy because both construction paths agree on them: the
        /// procedural build creates the tiles with these names and the prefab-bind path resolves them by name
        /// through the hierarchy index, so a board regrouped by hand still identifies itself correctly.
        /// </para>
        /// </summary>
        public static bool TryParseTileOwnership(string name, out PlayerIndex owner, out TileRole role,
            out int nodeIndex)
        {
            owner = PlayerIndex.Player;
            role = TileRole.Other;
            nodeIndex = -1;

            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            if (name.StartsWith("NodePlate"))
            {
                role = TileRole.Plate;
                nodeIndex = TrailingIndex(name, 0);
                return true;
            }

            if (name == "DeckPile" || name == "DiscardPile")
            {
                role = TileRole.Pile;
                return true;
            }

            // Node tiles are named Node{Play|Opp}{Active|Bench}{node}_{slot}; the plain Core ones are
            // {Play|Opp}{CoreActive|CoreBench}{slot}. Both put the owner's initial at the front.
            bool opponent = name.StartsWith("Opp") || name.StartsWith("NodeOpp");
            bool player = name.StartsWith("Play") || name.StartsWith("NodePlay");
            if (!opponent && !player)
            {
                return false;
            }

            owner = opponent ? PlayerIndex.Opponent : PlayerIndex.Player;

            // The Core's own plate, one per side. Deliberately below the owner split: this is the only
            // scenery-shaped tile on the table that carries a rule (hitting the opponent's is an unblocked
            // direct Core strike), so getting its owner backwards would aim the player's attacks at their own
            // Core. Above the rank rules because it is the one Core object that is not a socket, and it
            // contains neither "Active" nor "Bench" to be caught by them.
            if (name.EndsWith("CorePlate"))
            {
                role = TileRole.Core;
                return true;
            }

            if (name.EndsWith("Protocol"))
            {
                role = TileRole.Protocol;
                return true;
            }

            if (name.Contains("Active") || name.Contains("Bench"))
            {
                role = TileRole.Socket;
                if (name.StartsWith("Node"))
                {
                    nodeIndex = TrailingIndex(name, 1);
                }
                return true;
            }

            return false;
        }

        /// <summary>
        /// The tile-name to slot contract, exposed for tests because the whole drop-target feature rests on
        /// it: <c>PlayCoreActive0</c>, <c>PlayCoreBench3</c>, <c>NodePlayActive2_1</c> and
        /// <c>NodePlayBench0_0</c> name the player's hosting sockets, and nothing else does. Opponent tiles
        /// (<c>Opp*</c>, <c>NodeOpp*</c>), Node plates and Protocol tiles return false.
        /// </summary>
        public static bool TryParseSlotIdentity(string name, out SlotType type, out int index, out int nodeIndex)
        {
            type = default;
            index = 0;
            nodeIndex = -1;

            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            if (name.StartsWith("PlayCoreActive"))
            {
                type = SlotType.CoreActive;
                index = TrailingIndex(name, 0);
                return true;
            }

            if (name.StartsWith("PlayCoreBench"))
            {
                type = SlotType.CoreBench;
                index = TrailingIndex(name, 0);
                return true;
            }

            if (name.StartsWith("NodePlayActive"))
            {
                type = SlotType.NodeActive;
                nodeIndex = TrailingIndex(name, 1);
                index = TrailingIndex(name, 0);
                return true;
            }

            if (name.StartsWith("NodePlayBench"))
            {
                type = SlotType.NodeBench;
                nodeIndex = TrailingIndex(name, 1);
                index = TrailingIndex(name, 0);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Reads a number out of a tile name, counted in digit groups from the end:
        /// <c>"NodePlayActive2_1"</c> yields 1 at offset 0 (the slot) and 2 at offset 1 (the node).
        /// </summary>
        static int TrailingIndex(string name, int groupFromEnd)
        {
            int end = name.Length;
            for (int g = 0; g < groupFromEnd; g++)
            {
                while (end > 0 && char.IsDigit(name[end - 1])) end--;
                while (end > 0 && !char.IsDigit(name[end - 1])) end--;
            }

            int start = end;
            while (start > 0 && char.IsDigit(name[start - 1])) start--;

            return start < end && int.TryParse(name.Substring(start, end - start), out int value)
                ? value
                : 0;
        }

        /// <summary>
        /// The five families of surface a body on the table can belong to, and therefore the five materials a
        /// skin can replace.
        /// </summary>
        public enum Surface
        {
            Table,
            Socket,
            Plate,
            Pile,
            Rim,
        }

        /// <summary>
        /// The skin family a body claims, keyed off its name.
        /// <para>
        /// The same names, tested in the same order, that <see cref="BaseColorForSlot"/> reads for the empty
        /// colour - and for the same reason. The prefab cannot be relied on to carry a per-tile material any
        /// more than it can carry a per-tile colour, but the name always identifies what the body <em>is</em>,
        /// so a hand-regrouped board and a procedurally built one skin identically off the same vocabulary.
        /// That is also why the two resolvers are written next to each other: a name that means one thing to
        /// the colour and another to the slot is a tile that renders in the wrong material, silently.
        /// </para>
        /// <para>
        /// Order matters, and the rim test has to precede the fallback: the gold border cube is named
        /// <c>Socket</c> (see <see cref="MakeTile"/>) while no tile's own name ever contains the word. So a body
        /// called Socket is a rim, and anything that is not a table, a pile or a plate is a slot.
        /// </para>
        /// </summary>
        public static Surface SurfaceFor(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return Surface.Socket;
            }

            if (name.StartsWith("Table")) return Surface.Table;
            if (name.Contains("Socket")) return Surface.Rim;
            if (name == "DeckPile" || name == "DiscardPile" || name.StartsWith("Stack")) return Surface.Pile;
            if (name.StartsWith("NodePlate") || name.EndsWith("CorePlate")) return Surface.Plate;
            return Surface.Socket;
        }

        /// <summary>
        /// The empty-slot colour a tile is built with, keyed off its name. Shared by the build path and
        /// the bind path so a bound prefab repaints identically to a procedurally built board.
        /// </summary>
        Color BaseColorForSlot(string name)
        {
            if (name == "DeckPile") return deckTint;
            if (name == "DiscardPile") return discardTint;
            if (name.StartsWith("Table")) return tableTint;
            if (name.Contains("Socket")) return activeRim;
            if (name.StartsWith("NodePlate")) return nodeFree;
            if (name.EndsWith("CorePlate")) return corePlateTint;
            if (name.Contains("Active")) return activeEmpty;
            if (name.Contains("Bench")) return benchEmpty;
            if (name.Contains("Protocol")) return protocolEmpty;
            return Color.white;
        }

        void BuildCoreBattlefield()
        {
            int activeCount = Board.CoreActiveSlots;
            int benchCount = Board.CoreBenchSlots;

            playerCoreActive = new Tile[activeCount];
            playerCoreBench = new Tile[benchCount];
            opponentCoreActive = new Tile[activeCount];
            opponentCoreBench = new Tile[benchCount];

            Transform core = Group(boardRoot, "Core");
            Transform player = Group(core, "Player");
            Transform opponent = Group(core, "Opponent");

            // Each half splits into the three things you might want to reposition independently: the front
            // rank, the Protocol socket beside it, and the back rank.
            Transform playerActiveGroup = Group(player, "Active");
            Transform playerProtocolGroup = Group(player, "Protocol");
            Transform playerBenchGroup = Group(player, "Bench");
            Transform opponentActiveGroup = Group(opponent, "Active");
            Transform opponentProtocolGroup = Group(opponent, "Protocol");
            Transform opponentBenchGroup = Group(opponent, "Bench");

            // Plates first: they are at PlateZ, behind every socket on top of them, so build order is the
            // only thing that keeps the hierarchy readable when a designer opens the prefab.
            opponentCorePlate = MakeTile(opponent, "OppCorePlate",
                new Vector3(0f, CoreOpponentPlateY, PlateZ), CorePlateSize, corePlateTint, "", 14);
            playerCorePlate = MakeTile(player, "PlayCorePlate",
                new Vector3(0f, CorePlayerPlateY, PlateZ), CorePlateSize, corePlateTint, "", 14);

            // The dead band between the two facing Active ranks is left clear. It used to carry a
            // "CORE BATTLEFIELD" caption, which said nothing a player needed the table to tell them and sat
            // directly on the line the eye follows when aiming an attack.

            // Front rank: the Active socket on the board's centre line, with the player's single Protocol
            // socket beside it at Bench size. This rank sits nearer the middle of the table, so the Active
            // card faces the opponent and the card behind it (the Bench) never blocks it from the camera.
            for (int i = 0; i < activeCount; i++)
            {
                opponentCoreActive[i] = MakeTile(opponentActiveGroup, $"OppCoreActive{i}",
                    new Vector3(0f, CoreOpponentActiveY, CardZ), CoreActiveSize, activeEmpty,
                    "ACTIVE", 20, true);
                playerCoreActive[i] = MakeTile(playerActiveGroup, $"PlayCoreActive{i}",
                    new Vector3(0f, CorePlayerActiveY, CardZ), CoreActiveSize, activeEmpty,
                    "ACTIVE", 20, true);
            }

            opponentProtocol = MakeTile(opponentProtocolGroup, "OppProtocol",
                new Vector3(CoreProtocolX, CoreOpponentActiveY, CardZ), CoreProtocolSize, protocolEmpty, "PROT", 12);
            playerProtocol = MakeTile(playerProtocolGroup, "PlayProtocol",
                new Vector3(CoreProtocolX, CorePlayerActiveY, CardZ), CoreProtocolSize, protocolEmpty, "PROT", 12);

            // Back rank: the four Bench sockets, behind the Active rank, toward its owner's own edge.
            for (int i = 0; i < benchCount; i++)
            {
                float x = CoreSlotX(i, benchCount);
                opponentCoreBench[i] = MakeTile(opponentBenchGroup, $"OppCoreBench{i}",
                    new Vector3(x, CoreOpponentBenchY, CardZ), CoreBenchSize, benchEmpty, "BENCH", 14);
                playerCoreBench[i] = MakeTile(playerBenchGroup, $"PlayCoreBench{i}",
                    new Vector3(x, CorePlayerBenchY, CardZ), CoreBenchSize, benchEmpty, "BENCH", 14);
            }
        }

        static float CoreSlotX(int index, int total)
        {
            return (index - (total - 1) * 0.5f) * CoreSpacing;
        }

        void BuildNodeBattlefields()
        {
            nodePlate = new Tile[Board.NodeCount];
            nodePlayerActive = new Tile[Board.NodeCount][];
            nodePlayerBench = new Tile[Board.NodeCount][];
            nodeOpponentActive = new Tile[Board.NodeCount][];
            nodeOpponentBench = new Tile[Board.NodeCount][];

            Transform nodes = Group(boardRoot, "Nodes");

            for (int n = 0; n < Board.NodeCount; n++)
            {
                Vector2 c = NodeCenter[n % NodeCenter.Length];

                // The node group itself carries the centre offset, so everything under it is relative to
                // its own battlefield. Repositioning a Node in the editor is then one move of "Node 3"
                // rather than eight separate tile edits, and the drop hit-test follows automatically
                // because it resolves each tile through the world transform.
                Transform node = Group(nodes, $"Node {n + 1}");
                node.localPosition = new Vector3(c.x, c.y, 0f);

                // The plate keeps a bare NODE tag so the HUD's bonus list still reads against the board — you
                // can see which plate row 2 of the list is describing — but the bonus wording itself moved to
                // that list. It used to be a 13pt caption on the plate, which is the least legible place on
                // the table for it: dead centre, foreshortened by the pitch, and under a card as soon as
                // anyone contests the node. Riding the plate's own label rather than a sibling means the tag
                // travels with the plate when a node is moved in the editor.
                nodePlate[n] = MakeTile(node, $"NodePlate{n}",
                    new Vector3(0f, 0f, PlateZ), NodePlateSize, nodeFree, $"NODE {n + 1}", 14);

                nodeOpponentActive[n] = new Tile[Board.NodeActiveSlots];
                nodeOpponentBench[n] = new Tile[Board.NodeBenchSlots];
                nodePlayerActive[n] = new Tile[Board.NodeActiveSlots];
                nodePlayerBench[n] = new Tile[Board.NodeBenchSlots];

                Transform player = Group(node, "Player");
                Transform opponent = Group(node, "Opponent");
                Transform playerActiveGroup = Group(player, "Active");
                Transform playerBenchGroup = Group(player, "Bench");
                Transform opponentActiveGroup = Group(opponent, "Active");
                Transform opponentBenchGroup = Group(opponent, "Bench");

                // Front rank: the Active socket, nearer the middle of the table and larger than its
                // own Bench tiles, so it faces the opponent rather than hiding behind its Bench.
                for (int i = 0; i < Board.NodeActiveSlots; i++)
                {
                    float x = NodeSlotOffset(i, Board.NodeActiveSlots);
                    nodeOpponentActive[n][i] = MakeTile(opponentActiveGroup, $"NodeOppActive{n}_{i}",
                        new Vector3(x, NodeOpponentActiveY, CardZ), NodeActiveSize, activeEmpty,
                        "ACTIVE", 13, true);
                    nodePlayerActive[n][i] = MakeTile(playerActiveGroup, $"NodePlayActive{n}_{i}",
                        new Vector3(x, NodePlayerActiveY, CardZ), NodeActiveSize, activeEmpty,
                        "ACTIVE", 13, true);
                }

                // Back rank: the Bench sockets, behind the Active rank, toward its owner's own edge.
                for (int i = 0; i < Board.NodeBenchSlots; i++)
                {
                    float x = NodeSlotOffset(i, Board.NodeBenchSlots);
                    nodeOpponentBench[n][i] = MakeTile(opponentBenchGroup, $"NodeOppBench{n}_{i}",
                        new Vector3(x, NodeOpponentBenchY, CardZ), NodeBenchSize, benchEmpty, "BENCH", 11);
                    nodePlayerBench[n][i] = MakeTile(playerBenchGroup, $"NodePlayBench{n}_{i}",
                        new Vector3(x, NodePlayerBenchY, CardZ), NodeBenchSize, benchEmpty, "BENCH", 11);
                }
            }
        }

        /// <summary>
        /// Where the nth socket of a rank sits along that rank, measured from its own battlefield centre.
        /// </summary>
        static float NodeSlotOffset(int index, int perRow)
        {
            return (index - (perRow - 1) * 0.5f) * NodeSpacing;
        }

        /// <summary>
        /// The deck and discard stacks, as two card-footprint piles flanking the player's Bench row.
        /// <para>
        /// These are deliberately <em>not</em> sockets: they are given no slot identity, so a drag over them
        /// highlights nothing and the engine is never asked to host a card there. Their only job is to make
        /// the two places cards come from and go to visible, and to carry the live counts - a deck draining
        /// towards the Deck-Out victory condition is otherwise invisible information.
        /// </para>
        /// </summary>
        void BuildPiles()
        {
            Transform piles = Group(boardRoot, "Piles");

            deckPile = MakePile(piles, "DeckPile", DeckPilePosition, "DECK", deckTint);
            discardPile = MakePile(piles, "DiscardPile", DiscardPilePosition, "DISCARD", discardTint);
        }

        /// <summary>
        /// One pile: a top card plus two further cubes stacked behind it and nudged off the corner, so it
        /// reads as a stack of cards rather than as one more empty socket on the table.
        /// </summary>
        Tile MakePile(Transform parent, string name, Vector2 pos, string caption, Color tint)
        {
            Tile tile = MakeTile(parent, name, new Vector3(pos.x, pos.y, CardZ), PileSize, tint, "", 12);

            Transform holder = tile.Body.transform.parent;
            for (int i = 1; i <= 2; i++)
            {
                // Larger local Z is further from the pitched camera, so each layer tucks behind the one
                // above it; the sideways nudge is what makes the depth legible from a flat angle.
                MakeBody(holder, "Stack" + i,
                    new Vector3(0.024f * i, -0.024f * i, 0.05f * i), PileSize, Darken(tint, 0.78f - 0.08f * i));
            }

            tile.Label.text = caption;
            return tile;
        }

        /// <summary>
        /// Shades a colour towards black without touching its alpha. Multiplying a <c>Color</c> by a scalar
        /// scales alpha too, which turns an opaque URP Lit surface translucent.
        /// </summary>
        static Color Darken(Color c, float k)
        {
            return new Color(c.r * k, c.g * k, c.b * k, c.a);
        }

        // ---- Drop targeting ---------------------------------------------------------

        /// <summary>
        /// Hit-tests the player's own slot tiles against a world position and highlights the closest match.
        /// <para>
        /// Only the player's half is probed: you cannot deploy onto the opponent's ranks. The Protocol socket
        /// is excluded because nothing is targeted there (Abilities take no slot and a Protocol has exactly
        /// one home, which the engine already enforces). Depth is ignored on purpose, since a card dragged
        /// along the table floats only a little above it on the board's local Z.
        /// </para>
        /// That last clause is the limit of this method: it is the right test for a point that lies near the
        /// table, and the wrong one for anything held up at the lens, where "under" stops meaning anything a
        /// player can see. Use <see cref="FindDropTargetAtScreenByWorld"/> for a card.
        /// <para>
        /// Returns the slot under the point, or null when there is none. Legality (card type, occupancy) is
        /// <b>not</b> judged here: this view has no rules, so <see cref="GameCoordinator"/> validates the
        /// candidate and clears the highlight when the drop would be illegal.
        /// </summary>
        public BoardSlot FindDropTarget(Vector3 worldPos)
        {
            if (boardRoot == null)
            {
                return null;
            }

            return FindDropTargetInBoardSpace(boardRoot.InverseTransformPoint(worldPos));
        }

        /// <summary>
        /// The same test against a point on screen, which is the one a card held in front of the lens needs.
        /// <para>
        /// A card lying a hand's width above the table can be asked "what is under you" by dropping straight
        /// down along the table normal, and <see cref="FindDropTarget"/> does exactly that. A card parented to
        /// the camera cannot: it hangs several units above the board and back past its near edge, so the point
        /// beneath it is behind the table rather than on it, and every drop resolves to nothing. The only
        /// meaning a floating card has is the one the player sees, so the question has to be asked along the
        /// view ray: through the pixel the card covers, onto the table plane, and probe there.
        /// </para>
        /// </summary>
        public BoardSlot FindDropTargetAtScreen(Camera cam, Vector2 screenPos)
        {
            if (boardRoot == null)
            {
                return null;
            }

            if (cam == null)
            {
                cam = Camera.main;
            }

            if (cam == null)
            {
                return ClearDropTarget();
            }

            Ray ray = cam.ScreenPointToRay(screenPos);
            // The board's local Z is the table normal, taken from the hierarchy for the same reason
            // FindHitAt takes it: the Match root is tilted, so the plane cannot be assumed.
            var table = new Plane(boardRoot.forward, boardRoot.position);
            if (!table.Raycast(ray, out float enter))
            {
                // Parallel to the table, or aimed away from it: nothing is under the card, and the
                // highlight has to go out rather than linger on whatever it last named.
                return ClearDropTarget();
            }

            return FindDropTargetInBoardSpace(boardRoot.InverseTransformPoint(ray.GetPoint(enter)));
        }

        /// <summary>
        /// Extinguishes the drop highlight and reports no target. The miss paths have to do this rather
        /// than return a bare null, or a socket lit by the previous frame stays lit after the card has
        /// been moved off it - and a lit socket is a promise the release relies on.
        /// </summary>
        BoardSlot ClearDropTarget()
        {
            highlightedTile = null;
            return null;
        }

        /// <summary>
        /// The view-ray drop test, expressed the way the hand reports a drag: as the card's world position.
        /// <para>
        /// This is the one the drop path should call. Asking "what socket is under this card" only has an
        /// answer once the card may not lie on the table at all, and the answer is whatever the card
        /// <em>covers</em> - so the world point is folded back to the pixel it projects to and handed to
        /// <see cref="FindDropTargetAtScreen"/>. For a card resting just above the board the two tests agree
        /// to within the card's hover height, which is well inside a socket, so nothing that worked before
        /// changes; for a card held in front of the lens the straight-down test lands behind the board and
        /// finds nothing, which is why it cannot be the only route.
        /// </para>
        /// </summary>
        public BoardSlot FindDropTargetAtScreenByWorld(Vector3 worldPos)
        {
            if (boardRoot == null)
            {
                return null;
            }

            Camera cam = Camera.main;
            if (cam == null)
            {
                // No lens means no view ray, and the straight-down footprint is all there is.
                return FindDropTarget(worldPos);
            }

            Vector3 screen = cam.WorldToScreenPoint(worldPos);
            if (screen.z <= 0f)
            {
                // Behind the camera: WorldToScreenPoint mirrors the point through the lens and the
                // resulting ray is nonsense, so this is a miss rather than a hit on whatever happens to
                // sit opposite.
                return ClearDropTarget();
            }

            return FindDropTargetAtScreen(cam, screen);
        }

        /// <summary>
        /// The footprint probe both entry points share, in board-root coordinates.
        /// </summary>
        BoardSlot FindDropTargetInBoardSpace(Vector3 point)
        {
            if (playerCoreActive == null)
            {
                // The board has not been built yet, so there is nothing to probe and nothing to light.
                return null;
            }

            Tile best = null;
            float bestDistance = float.MaxValue;

            // A tile is a holder GameObject with a scaled cube child, so its centre comes from the world
            // transform (valid for the prefab-bound hierarchy too) and its half-extent from the cube's
            // authored localScale, which is expressed in board-root units.
            Probe(playerCoreActive, ref best, ref bestDistance, point);
            Probe(playerCoreBench, ref best, ref bestDistance, point);
            for (int n = 0; n < Board.NodeCount; n++)
            {
                Probe(nodePlayerActive[n], ref best, ref bestDistance, point);
                Probe(nodePlayerBench[n], ref best, ref bestDistance, point);
            }

            highlightedTile = best;
            return best == null ? null : new BoardSlot(best.SlotType, best.SlotIndex, best.NodeIndex);
        }

        /// <summary>
        /// Keeps the closest tile in <paramref name="best"/> whose footprint contains <paramref name="point"/>,
        /// given in board-root space. Depth is ignored: a dragged card floats above the table on the board's
        /// local Z, so only the X/Y footprint decides whether a socket is under it.
        /// </summary>
        void Probe(Tile[] tiles, ref Tile best, ref float bestDistance, Vector3 point)
        {
            if (tiles == null)
            {
                return;
            }

            for (int i = 0; i < tiles.Length; i++)
            {
                if (!TryHit(tiles[i], point, out float distance))
                {
                    continue;
                }

                if (best == null || distance < bestDistance)
                {
                    best = tiles[i];
                    bestDistance = distance;
                }
            }
        }

        /// <summary>
        /// True when <paramref name="point"/> (in board-root units) lies inside this tile's footprint, with
        /// the squared distance to its centre on output. Depth is ignored for the same reason
        /// <see cref="FindDropTarget"/> ignores it: the tiles are laid out flat and the thing being tested
        /// floats above them.
        /// </summary>
        bool TryHit(Tile tile, Vector3 point, out float distanceSquared)
        {
            distanceSquared = float.MaxValue;
            if (tile == null || tile.Body == null)
            {
                return false;
            }

            // Centre from the world transform and extent from the effective scale, both expressed in
            // board-root units, so a tile buried inside the Core / Node / rank groups probes exactly
            // like a flat one - and a group you scaled or moved in the editor is honoured as authored.
            Vector3 center = boardRoot.InverseTransformPoint(tile.Body.transform.position);
            Vector3 half = HalfExtentInBoardUnits(tile.Body.transform);
            float dx = point.x - center.x;
            float dy = point.y - center.y;
            if (Mathf.Abs(dx) > half.x || Mathf.Abs(dy) > half.y)
            {
                return false;
            }

            distanceSquared = dx * dx + dy * dy;
            return true;
        }

        /// <summary>
        /// A tile cube's half-extent measured in board-root units. Dividing the body's lossy scale by the
        /// board root's removes every scale authored between the two, so resizing a <c>Bench</c> or a whole
        /// <c>Node 3</c> group in the editor resizes its drop footprint with it instead of leaving the
        /// hit-test silently stuck on the prefab's original size.
        /// </summary>
        Vector3 HalfExtentInBoardUnits(Transform body)
        {
            Vector3 lossy = body.lossyScale;
            Vector3 root = boardRoot.lossyScale;

            float x = Mathf.Abs(root.x) > 1e-6f ? lossy.x / root.x : lossy.x;
            float y = Mathf.Abs(root.y) > 1e-6f ? lossy.y / root.y : lossy.y;
            float z = Mathf.Abs(root.z) > 1e-6f ? lossy.z / root.z : lossy.z;
            return new Vector3(Mathf.Abs(x), Mathf.Abs(y), Mathf.Abs(z)) * 0.5f;
        }

        /// <summary>Drops the current drop-target highlight. The next <see cref="Refresh"/> repaints the tile.</summary>
        public void ClearDropHighlight()
        {
            highlightedTile = null;
        }

        // ---- Pointer targeting ------------------------------------------------------
        // Everything above answers "where would a dragged card land", which only ever concerns the player's
        // own deployable sockets. Clicking is a broader question - the opponent's ranks, both Protocol
        // sockets, the Node plates, the two piles and the two Core counters are all worth knowing about - so
        // it gets its own hit-test over every object on the table, sharing the footprint maths above.
        //
        // Like the drop test, this is geometry and nothing else. It reports what is under the pointer and
        // whose it is; GameCoordinator decides whether the player may act on it.
        //
        // The two Core plates are part of that geometry like any other tile, so an unblocked direct Core
        // strike (GDD 2.1.6 Step 4) is aimed at a real object on the table. It used to be aimed at an
        // invisible rectangle where the status strips had stood, which was defensible only while the strips
        // were drawn there and the rectangle was their footprint.

        readonly List<Tile> pickables = new List<Tile>();
        readonly Dictionary<Tile, CardInstance> cardByTile = new Dictionary<Tile, CardInstance>();
        readonly Dictionary<int, Tile> tileByCard = new Dictionary<int, Tile>();
        bool pickablesValid;
        bool coreMarked;

        /// <summary>The tile a unit is standing on, or null when the board has not painted it yet.</summary>
        public Tile TileOf(CardInstance card)
        {
            return card != null && tileByCard.TryGetValue(card.InstanceId, out Tile tile) ? tile : null;
        }

        /// <summary>
        /// Resolves a screen point to the tabletop object under it, by casting through the board's own plane.
        /// <para>
        /// Deliberately not a <c>Physics</c> raycast: the tile cubes have no colliders, because the hand's
        /// drag raycast must never hit the board, and a collider added for clicking would change what a drag
        /// can pass over. Projecting onto the board plane and reusing the drop test's footprint maths keeps
        /// one geometry for both gestures.
        /// </para>
        /// </summary>
        /// <returns>What is under the pointer, or null when the ray misses the table entirely.</returns>
        public BoardHit FindHitAt(Camera cam, Vector2 screenPos)
        {
            if (boardRoot == null || playerCoreActive == null)
            {
                return null;
            }

            if (cam == null)
            {
                cam = Camera.main;
            }

            if (cam == null)
            {
                return null;
            }

            Ray ray = cam.ScreenPointToRay(screenPos);
            // The board's local Z is the table normal: the Match root is tilted 90 degrees, so the plane has
            // to come from the hierarchy rather than be assumed to be Vector3.forward.
            var table = new Plane(boardRoot.forward, boardRoot.position);
            if (!table.Raycast(ray, out float enter))
            {
                return null;
            }

            return FindHitInBoardSpace(boardRoot.InverseTransformPoint(ray.GetPoint(enter)));
        }

        /// <summary>
        /// The same test in board-root coordinates, exposed for tests: the screen-space version needs a
        /// camera and a viewport, and neither should be a precondition for asserting what is clickable.
        /// </summary>
        public BoardHit FindHitInBoardSpace(Vector3 point)
        {
            CollectPickables();

            Tile best = null;
            float bestDistance = float.MaxValue;
            int bestRank = -1;

            for (int i = 0; i < pickables.Count; i++)
            {
                if (!TryHit(pickables[i], point, out float distance))
                {
                    continue;
                }

                int rank = SpecificityRank(pickables[i]);
                if (best == null || rank > bestRank || (rank == bestRank && distance < bestDistance))
                {
                    best = pickables[i];
                    bestDistance = distance;
                    bestRank = rank;
                }
            }

            return best == null ? null : FromTile(best);
        }

        /// <summary>
        /// How specific a tile is as a click target, used to break the overlap between a plate and the ranks
        /// drawn on top of it.
        /// <para>
        /// A plate is scenery. It exists so a Node's bonus has something to hang on and so a direct Core strike
        /// has somewhere to be aimed from; it must never intercept a click that was meant for a unit. And the
        /// overlap is not hypothetical: the Core plate is 2.95 units tall and centred in the dead band between
        /// its two ranks, which puts a third of the Active tile's own footprint closer to the plate's centre
        /// than to the Active's. Decided on distance alone, clicking the bottom of an enemy unit would strike
        /// their Core. Decided on role, the plate keeps only the margin of tabletop nothing more specific covers
        /// &mdash; which is what the drop layer has always assumed, in words if not in code.
        /// </para>
        /// </summary>
        static int SpecificityRank(Tile tile)
        {
            switch (tile.Role)
            {
                case TileRole.Plate:
                case TileRole.Core:
                    return 0;
                default:
                    return 1;
            }
        }

        BoardHit FromTile(Tile tile)
        {
            cardByTile.TryGetValue(tile, out CardInstance card);
            return new BoardHit
            {
                Kind = KindFor(tile),
                Owner = tile.Owner,
                SlotType = tile.SlotType,
                SlotIndex = tile.SlotIndex,
                NodeIndex = tile.NodeIndex,
                Card = card,
                Tile = tile,
            };
        }

        /// <summary>
        /// Boards only carry the four recognised roles, since <see cref="CollectPickables"/> drops anything
        /// else - the last arm exists for the compiler, not for a case that can happen.
        /// </summary>
        static BoardHitKind KindFor(Tile tile)
        {
            switch (tile.Role)
            {
                case TileRole.Protocol: return BoardHitKind.Protocol;
                case TileRole.Plate: return BoardHitKind.Plate;
                case TileRole.Core:
                    return tile.Owner == PlayerIndex.Opponent
                        ? BoardHitKind.OpponentCore
                        : BoardHitKind.PlayerCore;
                case TileRole.Pile: return tile.Name == "DeckPile" ? BoardHitKind.DeckPile : BoardHitKind.DiscardPile;
                default: return BoardHitKind.Socket;
            }
        }

        void CollectPickables()
        {
            if (pickablesValid)
            {
                return;
            }

            pickables.Clear();
            Add(playerCoreActive);
            Add(playerCoreBench);
            Add(opponentCoreActive);
            Add(opponentCoreBench);
            AddTile(playerProtocol);
            AddTile(opponentProtocol);

            // The Core plates are pickable even though they underlie both Core ranks: TryHit resolves by
            // nearest centre, and a socket's centre is always closer to a click inside that socket than the
            // plate's is. So the plates only ever win the margin of tabletop around the ranks, which is
            // precisely the generous, visible area a direct Core strike should be aimable at.
            AddTile(playerCorePlate);
            AddTile(opponentCorePlate);

            if (nodePlate != null)
            {
                for (int n = 0; n < nodePlate.Length; n++)
                {
                    AddTile(nodePlate[n]);
                }
            }

            if (nodePlayerActive != null)
            {
                for (int n = 0; n < nodePlayerActive.Length; n++)
                {
                    Add(nodePlayerActive[n]);
                    Add(nodePlayerBench[n]);
                    Add(nodeOpponentActive[n]);
                    Add(nodeOpponentBench[n]);
                }
            }

            AddTile(deckPile);
            AddTile(discardPile);

            pickablesValid = true;

            void Add(Tile[] tiles)
            {
                if (tiles == null)
                {
                    return;
                }

                for (int i = 0; i < tiles.Length; i++)
                {
                    AddTile(tiles[i]);
                }
            }

            void AddTile(Tile tile)
            {
                if (tile != null && tile.Body != null && tile.Role != TileRole.Other)
                {
                    pickables.Add(tile);
                }
            }
        }

        // ---- Pointer marks ------------------------------------------------------------

        /// <summary>
        /// Clears every attention mark, including the Core glow. Cheap and total on purpose: the pointer
        /// layer rebuilds the marks from its own state whenever that state changes, so the way to stay
        /// consistent is to start from nothing rather than to diff against what was lit last time.
        /// </summary>
        public void ClearMarks()
        {
            CollectPickables();
            for (int i = 0; i < pickables.Count; i++)
            {
                pickables[i].Mark = PickMark.None;
            }

            coreMarked = false;
        }

        /// <summary>Marks one tile. Safe to call with a tile the board no longer has.</summary>
        public void Mark(BattlefieldPresenter.Tile tile, PickMark mark)
        {
            if (tile != null)
            {
                tile.Mark = mark;
            }
        }

        /// <summary>Marks the tiles every unit in the list is standing on, and reports how many were lit.</summary>
        public int MarkCards(IReadOnlyList<CardInstance> cards, PickMark mark)
        {
            if (cards == null)
            {
                return 0;
            }

            int lit = 0;
            for (int i = 0; i < cards.Count; i++)
            {
                Tile tile = TileOf(cards[i]);
                if (tile == null)
                {
                    continue;
                }

                tile.Mark = mark;
                lit++;
            }

            return lit;
        }

        /// <summary>
        /// Lights the opponent's Core plate, which is the click target for an unblocked direct attack.
        /// <para>
        /// This is the one mark that cannot go through <see cref="MarkCards"/>: a plate holds no card, so
        /// there is nothing in <c>cardByTile</c> to resolve it from. Marking the tile directly is also what
        /// keeps the footprint stable — the plate is there whether or not the opponent has a unit on their
        /// Core Active rank, whereas aiming at the socket would vanish the moment they deployed to it.
        /// </summary>
        public void MarkCoreTarget(bool on)
        {
            coreMarked = on;
            Mark(opponentCorePlate, on ? PickMark.AttackTarget : PickMark.None);
        }

        /// <summary>
        /// Whether the opponent's Core is currently lit as an attack target. A test or a cursor state asks
        /// this rather than reading the plate's colour, because the colour is a blend of the mark over
        /// whatever the plate's base tint happens to be.
        /// </summary>
        public bool CoreTargetMarked => coreMarked;

        /// <summary>
        /// The tile the pointer is currently over, when it is something the player could act on. Kept
        /// separate from <see cref="Tile.Mark"/> because hover is a transient sheen that must never
        /// outsurvive a frame, whereas a mark is a commitment the click layer made and expects to still be
        /// there when it resolves the pointer's release.
        /// </summary>
        public void SetHover(Tile tile)
        {
            if (hoveredTile == tile)
            {
                return;
            }

            hoveredTile = tile;
        }

        Tile hoveredTile;

        /// <summary>
        /// Rebuilds the marks for the current selection. Called by <see cref="BattlefieldInput"/> whenever the
        /// player picks or un-picks a unit, and given the legal targets rather than computing them: the view
        /// has no rules, so it cannot decide what a unit may hit.
        /// <para>
        /// The retreat list is the second of the two things a picked-up Active may do in Combat, and it arrives
        /// here rather than behind its own call because both halves of the choice are lit at the same instant:
        /// the player reads "attack or retreat" off one unit, not off two gestures. Null is the block mode's
        /// answer, and means no blue sockets rather than an error.
        /// </para>
        /// </summary>
        public void ShowAttack(CardInstance attacker, IReadOnlyList<CardInstance> targets, bool coreTargetable,
            IReadOnlyList<CardInstance> retreats = null)
        {
            ClearMarks();
            MarkCards(attacker == null ? null : new[] { attacker }, PickMark.Selected);
            MarkCards(targets, PickMark.AttackTarget);
            MarkCards(retreats, PickMark.RetreatTarget);
            MarkCoreTarget(coreTargetable);
        }

        /// <summary>The blocking equivalent of <see cref="ShowAttack"/>.</summary>
        public void ShowBlocks(IReadOnlyList<CardInstance> readyBlockers,
            IReadOnlyList<CardInstance> blockableAttackers)
        {
            ClearMarks();
            MarkCards(readyBlockers, PickMark.Blocker);
            MarkCards(blockableAttackers, PickMark.BlockTarget);
        }

        /// <summary>
        /// Lights the single tile a blocker is being aimed at, without disturbing the rest of the marks.
        /// Used while the player has picked a blocker and is choosing which enemy attack to answer.
        /// </summary>
        public void ShowBlockPick(CardInstance blocker, IReadOnlyList<CardInstance> attackers)
        {
            ClearMarks();
            MarkCards(new[] { blocker }, PickMark.Selected);
            MarkCards(attackers, PickMark.BlockTarget);
        }

        // ---- Details ------------------------------------------------------------------

        /// <summary>
        /// Writes the details panel. Every reader write in the game funnels through here, because the HUD is a
        /// reference the presenter resolves lazily and may not have yet: a caller reaching for
        /// <c>presenter.hud</c> directly would be the one place a null HUD turned into an exception.
        /// </summary>
        /// <param name="title">What the panel is about. Null with a null <paramref name="body"/> dismisses it.</param>
        /// <param name="body">The panel's text, TMP markup allowed.</param>
        /// <param name="foot">The footer - a position in a pile, or a hint. Null clears the line.</param>
        public void ShowInfo(string title, string body, string foot)
        {
            if (hud == null)
            {
                ResolveHud();
            }

            if (hud != null)
            {
                hud.ShowReader(title, body, foot);
            }
        }

        /// <summary>
        /// Puts a card that is (or was) on the table in the reader panel. Formatting lives here because the
        /// panel is a view of Core state: it reads the same <see cref="CardInstance"/> the tiles are painted
        /// from, so the details cannot disagree with the socket it came out of.
        /// </summary>
        public void ShowCard(CardInstance card, MatchState state, string where)
        {
            if (card == null)
            {
                return;
            }

            if (hud == null)
            {
                ResolveHud();
            }

            if (hud != null)
            {
                hud.ShowReader(card.Definition.Name, CardDetails(card, state), where, CardAccent(card.Definition.Type));
            }
        }

        /// <summary>
        /// The full text of a card: everything the tile's two-line caption has to leave out. Stat lines are
        /// skipped for the card types that have no stats rather than printed as <c>0/0</c>, which reads as a
        /// bug on a Relay or a Protocol (GDD 4.15.1).
        /// </summary>
        static string CardDetails(CardInstance card, MatchState state)
        {
            var sb = new StringBuilder();
            CardDefinition def = card.Definition;

            sb.Append("<color=#8fa0b8>").Append(TypeLabel(def.Type)).Append("</color>");
            if (def.Faction != 0 || def.Rarity != 0)
            {
                sb.Append("   \u2022   ").Append(def.Faction).Append(" / ").Append(def.Rarity);
            }
            sb.Append('\n');

            sb.Append("Cost ").Append(def.Cost).Append(" NP");

            // Gated on the card's <em>type</em>, not on whether its numbers happen to be non-zero. Only Units
            // fight (GDD 4.15.1), so a Protocol carrying ATK 2 is a data bug, and printing "ATK 2" here would
            // launder it into something that looks like a rule. The alternative - printing 0/0 for every Relay
            // and Protocol - reads as a bug too, which is the one this line exists to avoid.
            if (def.Type == CardType.Unit)
            {
                sb.Append('\n');
                sb.Append("ATK ").Append(card.CurrentAtk);
                if (card.NodeAtkBonus != 0)
                {
                    sb.Append("  (").Append(card.NodeAtkBonus > 0 ? "+" : "").Append(card.NodeAtkBonus)
                      .Append(" node)");
                }

                sb.Append("   HP ").Append(card.CurrentHp).Append('/').Append(def.Hp);
                if (card.NodeHpBonus != 0)
                {
                    sb.Append("  (+").Append(card.NodeHpBonus).Append(" node)");
                }

                if (card.CurrentShield > 0)
                {
                    sb.Append("   Shield ").Append(card.CurrentShield);
                }
            }

            string status = StatusLine(card, state);
            if (!string.IsNullOrEmpty(status))
            {
                sb.Append('\n').Append(status);
            }

            if (!string.IsNullOrEmpty(def.AbilityText))
            {
                sb.Append("\n\n").Append(def.AbilityText);
            }

            if (def.Keywords != null && def.Keywords.Count > 0)
            {
                sb.Append("\n\n<color=#8fa0b8>Keywords</color>   ");
                for (int i = 0; i < def.Keywords.Count; i++)
                {
                    if (i > 0)
                    {
                        sb.Append(", ");
                    }
                    sb.Append('[').Append(def.Keywords[i]).Append(']');
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Where the card stands right now, in words the player can act on: its slot, whether it could take
        /// an action this step, and whether somebody is already ready to block it.
        /// </summary>
        static string StatusLine(CardInstance card, MatchState state)
        {
            if (state == null)
            {
                return null;
            }

            BoardSlot slot = state.GetPlayer(card.Owner).Board.FindSlot(card);
            var parts = new List<string>();

            if (slot != null)
            {
                parts.Add(slot.Type.ToString().ToUpper());
            }

            if (card.HasSummoningDelay)
            {
                parts.Add("SUMMONING DELAY");
            }
            else if (card.State == UnitState.Exhausted)
            {
                parts.Add("EXHAUSTED");
            }
            else if (card.State == UnitState.Summoned)
            {
                parts.Add("SUMMONED");
            }

            return parts.Count == 0 ? null : string.Join("   •   ", parts);
        }

        static string TypeLabel(CardType type)
        {
            switch (type)
            {
                case CardType.Unit: return "UNIT";
                case CardType.Ability: return "ABILITY";
                case CardType.Relay: return "RELAY";
                case CardType.Protocol: return "PROTOCOL";
                default: return type.ToString().ToUpper();
            }
        }

        /// <summary>
        /// Shows one card of the discard, with its position in the pile. <paramref name="index"/> is the
        /// browser's business, not the view's: the caller owns how far into the pile the player has stepped.
        /// <para>
        /// The discard is public knowledge, so it is read in full. The deck deliberately gets the opposite
        /// treatment in <see cref="ShowDeck"/> - its order is secret, and a browser that stepped through it
        /// would be a cheat with a nice interface.
        /// </para>
        /// </summary>
        public void ShowPileEntry(string pileName, CardInstance card, int index, int count)
        {
            if (count == 0)
            {
                // No position in a pile that does not exist: "DISCARD 1 / 0" reads as an off-by-one bug.
                ShowInfo(pileName, "Nothing has been spent yet.", pileName);
                return;
            }

            string where = $"{pileName}  {index + 1} / {count}";
            if (card == null)
            {
                ShowInfo(pileName, string.Empty, where);
                return;
            }

            if (hud == null)
            {
                ResolveHud();
            }

            if (hud != null)
            {
                hud.ShowReader(card.Definition.Name, CardDetails(card, null), where, CardAccent(card.Definition.Type));
            }
        }

        static Color CardAccent(CardType type)
        {
            switch (type)
            {
                case CardType.Ability: return new Color(0.92f, 0.48f, 0.25f, 1f);
                case CardType.Relay: return new Color(0.25f, 0.78f, 0.58f, 1f);
                case CardType.Protocol: return new Color(0.60f, 0.43f, 0.95f, 1f);
                default: return new Color(0.35f, 0.63f, 1f, 1f);
            }
        }

        /// <summary>
        /// Reports the draw stack without revealing it. The count is information the table should be showing
        /// anyway - Deck-Out is one of the four victory conditions - but which card is next is not the
        /// player's to read, so the deck is the one clickable object on the table that refuses to open.
        /// </summary>
        public void ShowDeck(int count)
        {
            ShowInfo("DECK",
                count == 0
                    ? "Empty. Drawing from an empty deck is a loss (GDD 2.1.8 Deck-Out)."
                    : $"{count} card{(count == 1 ? "" : "s")} left.\n\n" +
                      "The top card is face down and the order is secret, so there is nothing here to leaf " +
                      "through - unlike the discard, which is public knowledge.",
                $"DECK  {count}");
        }

        /// <summary>Takes the details panel off screen.</summary>
        public void ClearDetails()
        {
            if (hud != null)
            {
                hud.HideReader();
            }
        }

        // ---- Notice ---------------------------------------------------------------------

        /// <summary>How long a rejection reason stays painted on the notice line before it clears itself.</summary>
        const float NoticeSeconds = 4f;

        float noticeUntil;
        bool hintShowsSetup;
        int deployPulseCardId = -1;
        float deployPulseUntil;

        /// <summary>
        /// Shows a short-lived explanation on the notice line. The play gate routes every rejected drag
        /// through here so the reason reaches the player, not just the Editor console: a card that bounces
        /// back with no message at all reads as a broken game rather than as an illegal play.
        /// </summary>
        public void ShowNotice(string message)
        {
            SetNotice(message);
            noticeUntil = Time.unscaledTime + NoticeSeconds;
        }

        /// <summary>
        /// Publishes the player's opening-lineup nomination so the board can preview it: an offered card id per
        /// Active socket in battlefield order, 0 for a socket the player has left empty. Passing null or an empty
        /// array clears the preview, which is what the coordinator does the moment setup closes - the offered
        /// cards are in the deck, in a hand or on the board by then, and a preview of them would be a duplicate
        /// of whatever the real paint puts there.
        /// <para>
        /// The board never asks the engine what the player nominated, because no such state exists in the engine:
        /// a nomination is a front-end promise until <c>ConfirmLineupCommand</c> submits it.
        /// </para>
        /// </summary>
        public void SetLineupPreview(int[] instanceIdsByActiveSocket)
        {
            lineupPreview = instanceIdsByActiveSocket ?? new int[0];
        }

        /// <summary>
        /// Repaints from an explicit match state, without a coordinator.
        /// <para>
        /// The runtime path is <see cref="LateUpdate"/> reading the state off the coordinator, and that is the
        /// right coupling for a game. It is the wrong one for a test: proving that a unit on a Core Active
        /// resolves to that socket's tile needs a painted board and nothing else, and manufacturing a
        /// <see cref="GameCoordinator"/> - which boots its match from a coroutine half a second into play mode
        /// - would couple the view's tests to the match's startup. Editor tooling that previews a board outside
        /// play mode gets the same seam for the same reason.
        /// </para>
        /// </summary>
        public void Repaint(MatchState state)
        {
            Repaint(state, null);
        }

        /// <summary>
        /// Repaints from a state the caller owns, consulting <paramref name="engine"/> for the pending-block
        /// shading instead of the coordinator's.
        /// <para>
        /// Pass null for the engine and this is exactly the runtime view of the match. Passing one is for the
        /// block step, which is the only thing on the board that reads the engine rather than the state, and the
        /// only state a player reaches while they are not the one taking their turn: a committed blocker is the
        /// clearest single thing the table can be showing, and leaving it unreachable from EditMode would ship it
        /// on faith. Declaring a real block costs three lines in a test and needs no match startup, so there is
        /// no reason to widen anything else for it.
        /// </para>
        /// </summary>
        public void Repaint(MatchState state, MatchEngine engine)
        {
            // The card-to-tile map is rebuilt as tiles are painted, and a stale map survives a rebuild only
            // because the tiles themselves are the same objects. Dropping validity here means a repaint after
            // a manual hierarchy edit re-collects the pickables rather than clicking on ghosts.
            pickablesValid = false;
            Refresh(state, engine);
        }

        /// <summary>Starts a short board-side pulse after a hand card is accepted as a deployment.</summary>
        public void PlayDeployAnimation(CardInstance card)
        {
            if (card == null)
            {
                return;
            }

            deployPulseCardId = card.InstanceId;
            deployPulseUntil = Time.unscaledTime + 0.55f;
            ShowNotice($"Deploying {card.Definition.Name}...");
        }

        void Refresh()
        {
            Refresh(coordinator != null ? coordinator.GetMatchState() : null);
        }

        void Refresh(MatchState state, MatchEngine injected = null)
        {
            if (Time.unscaledTime > noticeUntil)
            {
                SetNotice(string.Empty);
            }

            // Rebuilt from the tiles this frame paints, so a unit that moved slots cannot keep an old tile.
            tileByCard.Clear();
            if (state == null)
            {
                SetBanner("Waiting for the match to initialize...");
                highlightedTile = null;
                return;
            }

            // A board with no coordinator (a test fixture, or an editor preview) still paints: the engine is
            // only ever consulted for the pending block, which PaintTile, ShadeFor and TryStateCaption all
            // null-guard. That is now three readers of one fact, and all three say the same thing about a unit
            // the defender has committed, so an absent engine has to mean "nothing is blocked" rather than
            // "cannot tell" - which is what a null check on a lookup that returns 0 already gives.
            MatchEngine engine = injected != null
                ? injected
                : coordinator != null ? coordinator.GetMatchEngine() : null;
            bool over = state.IsMatchOver();

            // The legend is rewritten on the transition only, not every frame: the board repaints sixty times a
            // second for a line that changes once per match, and a text setter dirties its mesh on a write
            // whether or not the string changed.
            if (state.SetupInProgress != hintShowsSetup)
            {
                hintShowsSetup = state.SetupInProgress;
                SetHint(hintShowsSetup ? SetupHintText : ControlsHintText);
            }

            // The banner is 32pt rather than the 38 it shipped with, and the reason is a number rather than a
            // taste: its longest line - TURN 99, CLEANUP, ACTIVE: OPPONENT, bullet separators and all - lays out
            // 819 reference pixels at 32pt inside a 900 pixel box, which is about 973 at the 38 it used to be,
            // and TMP's Truncate overflow eats the surplus without complaint. The shipped setup banner rendered
            // as "FIELD YOUR OPENING F". Tightening the bullet spacing was the other candidate fix and it is the
            // worse one: the airy separator is how this HUD reads as one family, and 32pt still stands 1.9x over
            // the legend it sits above.
            if (hintShowsSetup)
            {
                // There is no turn worth numbering and no active player worth naming while setup is open, and
                // the toss is still under the cup, so the header says what the player is actually doing instead.
                SetBanner("LINEUP SETUP   \u2022   FIELD YOUR OPENING FIVE");
            }
            else
            {
                string active = state.ActivePlayer == PlayerIndex.Player ? "YOU" : "OPPONENT";
                SetBanner($"TURN {state.TurnNumber}   \u2022   {state.CurrentPhase.ToString().ToUpper()}   \u2022   ACTIVE: {active}");
            }

            PaintCore(state, engine, PlayerIndex.Opponent, opponentCoreActive, opponentCoreBench,
                opponentProtocol, opponentCorePlate);
            PaintCore(state, engine, PlayerIndex.Player, playerCoreActive, playerCoreBench,
                playerProtocol, playerCorePlate);
            PaintNodes(state, engine);
            PaintPiles(state);

            // The Core's pointer mark now lives on the plate, so PaintCore's TintForMark is the only writer.
            // Nothing here has to do with it any more.

            RefreshLog(state);

            if (over)
            {
                string word = state.Result == MatchResult.PlayerWin ? "VICTORY"
                    : state.Result == MatchResult.OpponentWin ? "DEFEAT"
                    : state.Result == MatchResult.Draw ? "DRAW" : "";

                // The reason rides on the same line at a third of the size: at the overlay's full 84pt a
                // phrase like "BOTH CORES DESTROYED" would run off a 1500px panel at the reference resolution.
                string reason = string.IsNullOrEmpty(state.ResultReason)
                    ? ""
                    : $"<size=36%>   \u2022   {state.ResultReason}</size>";

                SetResult(word + reason, state.Result == MatchResult.PlayerWin
                    ? new Color(0.45f, 1f, 0.55f)
                    : state.Result == MatchResult.Draw ? new Color(0.95f, 0.9f, 0.6f)
                    : new Color(1f, 0.45f, 0.45f));
            }
            else
            {
                SetResult(string.Empty, Color.white);
            }
        }

        // ---- HUD writes ---------------------------------------------------------------
        // Every one of these null-guards the HUD rather than the caller, because the HUD is a reference the
        // presenter resolves lazily: a board built by an editor menu or a bare test fixture legitimately has
        // no canvas, and the board still has to paint.

        void SetBanner(string text)
        {
            if (hud != null) hud.SetBanner(text);
        }

        void SetHint(string text)
        {
            if (hud != null) hud.SetHint(text);
        }

        void SetNotice(string text)
        {
            if (hud != null) hud.SetNotice(text);
        }

        void SetResult(string text, Color tint)
        {
            if (hud != null) hud.SetResult(text, tint);
        }

        /// <summary>
        /// Writes the live counts onto the two piles and lets an emptied deck read as drained. Deck-Out is a
        /// victory condition, so "how many are left" is information the table should be showing anyway -
        /// previously it lived only in the status strip.
        /// </summary>
        void PaintPiles(MatchState state)
        {
            PlayerState p = state.GetPlayer(PlayerIndex.Player);
            PaintPile(deckPile, p.Deck.Count, "DECK");
            PaintPile(discardPile, p.Discard.Count, "DISCARD");
        }

        void PaintPile(Tile tile, int count, string caption)
        {
            if (tile == null)
            {
                return;
            }

            SetBodyColor(tile.Body, TintForHover(tile, TintForMark(tile,
                count > 0 ? tile.BaseColor : Darken(tile.BaseColor, 0.45f))));
            tile.Label.text = caption + "\n" + count;
            tile.Label.color = count > 0 ? new Color(0.80f, 0.85f, 0.92f) : new Color(0.46f, 0.48f, 0.54f);
        }

        /// <summary>
        /// Moves the hand's draw and discard anchors onto the piles this board just built, so a card is dealt
        /// from the stack you can see and discarded onto the stack you can see.
        /// <para>
        /// <c>HandController</c> consumes those anchors as local positions in the Match root's plane, and the
        /// board root shares that plane, so copying world positions is exact. Doing it once here rather than
        /// every frame is what lets you drag the <c>Piles</c> group in the editor: the flight path follows.
        /// </para>
        /// </summary>
        void SyncPileAnchors()
        {
            HandController hand = coordinator != null ? coordinator.handController : null;
            if (hand == null)
            {
                return;
            }

            if (hand.deckPile != null && deckPile != null)
            {
                hand.deckPile.position = deckPile.Body.transform.position;
            }

            if (hand.discardPile != null && discardPile != null)
            {
                hand.discardPile.position = discardPile.Body.transform.position;
            }
        }

        void PaintCore(MatchState state, MatchEngine engine, PlayerIndex who,
            Tile[] active, Tile[] bench, Tile protocol, Tile plate)
        {
            PlayerState p = state.GetPlayer(who);
            for (int i = 0; i < active.Length; i++)
            {
                CardInstance occupant = Occupied(p.Board.CoreActive[i]);
                CardInstance shown = PreviewFor(state, who, i, occupant);
                PaintTile(active[i], shown, engine, !ReferenceEquals(shown, occupant));
            }
            for (int i = 0; i < bench.Length; i++)
            {
                PaintTile(bench[i], Occupied(p.Board.CoreBench[i]), engine);
            }
            PaintTile(protocol, p.Board.Protocol, engine);

            // The plate is pure geometry: a slab to aim at and a surface for the pointer mark. It carries no
            // caption on purpose. Its centre is behind the Active rank, so any text written there is covered
            // by a card the moment the opponent fields one, and the durability it would show is already on
            // this side's HUD chip, permanently — which is the whole reason it left the table.
            if (plate != null)
            {
                SetBodyColor(plate.Body, TintForHover(plate, TintForMark(plate, plate.BaseColor)));
            }

            // The readout moved off the table onto this side's chip. Formatting stays here because the HUD is
            // a view of strings, not of match state.
            int nodes = state.CountControlledNodes(who);
            if (hud != null)
            {
                hud.SetStatus(who,
                    // Five counters on one line need 732 reference pixels at 26pt and the chip's text rect is 446
                    // wide, so the readout either breaks or loses HAND and DECK to Truncate - which is exactly
                    // what it was doing, silently, in the shipped scene. It breaks after NP, because that is the
                    // seam that leaves both halves comfortable: the two numbers that tick down through a turn on
                    // the first line, the three stock counts on the second. Breaking after NODES - the other
                    // obvious seam, and the first one tried here - lays its top line out at 442 of 446 pixels,
                    // four pixels of slack on a figure the player grows by holding ground. Widening the slab is
                    // not on the table: the chips live in the fan's corners and 470 already touches the fan's
                    // maximum-span envelope.
                    $"CORE {p.CoreDurability}/{PlayerState.StartingCoreDurability}   " +
                    $"NP {p.CurrentNP}/{PlayerState.NpCap(nodes)}\n" +
                    $"NODES {nodes}/{Board.NodeCount}   HAND {p.Hand.Count}   DECK {p.Deck.Count}");
            }
        }

        void PaintNodes(MatchState state, MatchEngine engine)
        {
            PlayerState player = state.GetPlayer(PlayerIndex.Player);
            PlayerState opp = state.GetPlayer(PlayerIndex.Opponent);

            for (int n = 0; n < Board.NodeCount; n++)
            {
                // Occupation on each board, not MatchState.ControlsNode: this has to draw all four states
                // (both hold, one holds, neither), and the pair of holds is the primitive the four states are
                // defined from. ControlsNode is the same expression reduced to the one half that pays:
                // pHolds && !oHolds. A Node both sides hold is contested and pays neither side, which is why
                // it is coloured nodeFree rather than split down the middle - the colour says "nobody's".
                bool pHolds = player.Board.HoldsNode(n, PlayerIndex.Player);
                bool oHolds = opp.Board.HoldsNode(n, PlayerIndex.Opponent);
                bool pControls = pHolds && !oHolds;
                bool oControls = oHolds && !pHolds;

                SetBodyColor(nodePlate[n].Body, TintForHover(nodePlate[n], TintForMark(nodePlate[n],
                    pControls && oControls ? nodeFree
                    : pControls ? nodePlayer
                    : oControls ? nodeOpponent
                    : nodeFree)));

                // The bonus text and the ownership readout, which used to be the plate caption. The plate keeps
                // only its bare NODE tag now, so this is the one place a player can read what holding a node
                // is actually worth.
                if (hud != null)
                {
                    bool contested = pControls && oControls;
                    hud.SetNodeRow(n,
                        $"{NodeCaption[n % NodeCaption.Length]}   \u2022   " +
                        (contested ? "CONTESTED" : pControls ? "YOURS" : oControls ? "THEIRS" : "OPEN"),
                        contested || (!pControls && !oControls) ? RowNeutral
                        : pControls ? RowPlayer : RowOpponent);
                }

                for (int i = 0; i < Board.NodeActiveSlots; i++)
                {
                    CardInstance occupant = Occupied(player.Board.NodeActive[n][i]);
                    CardInstance shown = PreviewFor(state, PlayerIndex.Player,
                        NodeActiveSocketIndex(n, i), occupant);
                    PaintTile(nodePlayerActive[n][i], shown, engine, !ReferenceEquals(shown, occupant));
                    PaintTile(nodeOpponentActive[n][i], Occupied(opp.Board.NodeActive[n][i]), engine);
                }

                for (int i = 0; i < Board.NodeBenchSlots; i++)
                {
                    PaintTile(nodePlayerBench[n][i], Occupied(player.Board.NodeBench[n][i]), engine);
                    PaintTile(nodeOpponentBench[n][i], Occupied(opp.Board.NodeBench[n][i]), engine);
                }
            }
        }

        void RefreshLog(MatchState state)
        {
            if (hud == null)
            {
                return;
            }

            // The panel owns the windowing and the wrapping now, so the log arrives whole instead of being
            // clipped to whatever fitted a 1.9-unit-wide slab of tabletop.
            hud.SetLog(state.EventLog);
        }

        // ---- Tile painting ----------------------------------------------------------

        /// <summary>
        /// The two spellings a lit surface's colour goes by: URP Lit says <c>_BaseColor</c>, the built-in
        /// fallback says <c>_Color</c>.
        /// <para>
        /// Both ids exist for every board, and the choice is made per material rather than cached once per
        /// repaint, because a skin is whatever the scene hands it. Before the skin slots the whole board was
        /// built from two known shaders, so one static id resolved on the first tile was harmless; with five
        /// droppable materials the same cache would tint a URP tile through <c>_Color</c>, or an artist's unlit
        /// diffuse through <c>_BaseColor</c>, and the tile would simply never change colour - silently, on the
        /// half of the board nobody assigned a skin to. Asking the material whether it has the property is both
        /// cheaper and more honest than asking its shader what it is called.
        /// </para>
        /// </summary>
        static readonly int baseColorId = Shader.PropertyToID("_BaseColor");

        static readonly int colorId = Shader.PropertyToID("_Color");

        /// <summary>
        /// Reused across every tile in a repaint, which is the point: allocating one of these per renderer per
        /// frame is exactly the garbage the per-frame repaint exists to avoid. Created lazily rather than in an
        /// instance field initializer, because Unity forbids native-object construction (<c>CreateImpl</c>)
        /// there - the board is built straight out of <c>BuildBoard()</c> in tests and editor menus, with no
        /// <c>Awake</c> guaranteed to have run first.
        /// </summary>
        MaterialPropertyBlock tintBlock;

        /// <summary>
        /// Colours one tile body, without touching its material.
        /// <para>
        /// The obvious <c>renderer.material.color = c</c> is wrong twice over here. In edit mode it raises
        /// <em>"Instantiating material due to calling renderer.material during edit mode"</em>, which the test
        /// runner promotes to a failure and which leaks a material into the scene every time a board is built
        /// from a menu. In play mode it silently clones one material per renderer, so 35 tiles become 35
        /// material instances and the batch the board was built around is gone. A property block overrides the
        /// colour at draw time instead: no clone, no leak, and the shared asset keeps rendering every tile in
        /// one pass.
        /// </para>
        /// </summary>
        void SetBodyColor(Renderer renderer, Color color)
        {
            if (renderer == null)
            {
                return;
            }

            if (tintBlock == null)
            {
                tintBlock = new MaterialPropertyBlock();
            }

            Material shared = renderer.sharedMaterial;
            tintBlock.Clear();
            tintBlock.SetColor(shared != null && shared.HasProperty(baseColorId) ? baseColorId : colorId, color);
            renderer.SetPropertyBlock(tintBlock);
        }

        /// <param name="preview">
        /// True when <paramref name="card"/> is a lineup nomination rather than a body the rules put on this
        /// socket. A preview is washed toward <see cref="PendingMark"/> and stripped of every combat marker:
        /// it cannot be exhausted and cannot be a pending blocker, and it is not summoning either, because
        /// <c>CompleteSetup</c> readies everything it fields and waives the delay - so printing any of those
        /// would advertise a state the match explicitly refuses to give it.
        /// </param>
        void PaintTile(Tile tile, CardInstance card, MatchEngine engine, bool preview = false)
        {
            if (tile == null)
            {
                return;
            }

            // The pointer layer resolves a unit to the tile under it on every click, so the pairing is
            // refreshed with the rest of the view rather than cached across frames: a card promoted from
            // Bench to Active moves, and a stale tile would light the wrong socket. A nomination is paired the
            // same way on purpose, because that is what lets a click on a preview find the card it previews.
            if (card != null)
            {
                tileByCard[card.InstanceId] = tile;
                cardByTile[tile] = card;
            }
            else
            {
                cardByTile.Remove(tile);
            }

            if (card == null)
            {
                // A socket that has just emptied takes its card with it. Dead units are removed from the board by
                // the engine and appear in the discard, so this is the frame a death lands on: without the clear
                // the face would hang over an empty socket describing a card that is no longer there, which is
                // precisely the "I never saw it die" the caption used to be blamed for.
                ClearBoardCard(tile);

                Color empty = highlightedTile == tile
                    ? Color.Lerp(tile.BaseColor, new Color(1f, 0.9f, 0.5f), 0.4f)
                    : tile.BaseColor;
                SetBodyColor(tile.Body, TintForHover(tile, TintForMark(tile, empty)));
                tile.Label.text = tile.EmptyLabel;
                tile.Label.color = new Color(0.62f, 0.66f, 0.74f);
                return;
            }

            Color baseTint = card.Owner == PlayerIndex.Player ? playerTint : opponentTint;
            if (preview)
            {
                baseTint = Color.Lerp(baseTint, PendingMark, 0.45f);
            }
            else if (engine != null && engine.IsBlocking(card.InstanceId))
            {
                // The defender's own commitment, and the one thing on this board that has to be readable while it
                // is not your turn. IsBlocking rather than GetPendingBlocker, deliberately: the pending block is
                // keyed by attacker, so the near-identical call answers "is this attack answered" and would light
                // the swing instead of the wall in front of it - the exact inversion this used to ship with. The
                // same fact drives the card's shade and its caption two methods down, so all three say BLOCKING
                // about the unit that said it.
                baseTint = Color.Lerp(baseTint, BlockMark, 0.55f);
            }
            else if (card.State == UnitState.Exhausted)
            {
                baseTint *= 0.65f;
            }

            if (highlightedTile == tile)
            {
                baseTint = Color.Lerp(baseTint, new Color(1f, 0.85f, 0.35f), 0.5f);
            }

            if (card.InstanceId == deployPulseCardId && Time.unscaledTime < deployPulseUntil)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin((deployPulseUntil - Time.unscaledTime) * 18f);
                baseTint = Color.Lerp(baseTint, new Color(1f, 0.85f, 0.35f), pulse * 0.62f);
            }

            SetBodyColor(tile.Body, TintForHover(tile, TintForMark(tile, baseTint)));

            // The tile's own colour is painted first and the card laid over it, which is the order that keeps
            // both jobs done: the cube says what a click here would do (owner, mark, pending block, the gold rim
            // behind it), and the card says what the thing is. The card covers all but boardCardFill of the
            // socket, so the cube keeps reading as a ring around it rather than being hidden by it.
            if (SyncBoardCard(tile, card, engine, preview))
            {
                // The card already carries the name, the cost, the art, the ability text and the live attack,
                // health and shield, in the same face the fan uses, so the caption's job has narrowed to the four
                // states a frame cannot express about itself. With no catalogs wired the label keeps carrying the
                // whole description instead, which is what the board shipped with and what a bare test board
                // still depends on.
                if (TryStateCaption(card, engine, preview, out string caption, out Color captionColor))
                {
                    // A state word is one word, and the label is authored with wrapping on so a socket's full
                    // description can flow. Left on, TMP breaks SUMMONING across two lines mid-word on a Node
                    // Active, which is narrow enough that auto-size cannot save it first. Wrapping off hands the
                    // fitting to auto-size alone, so the word stays whole and shrinks instead.
                    tile.Label.enableWordWrapping = false;
                    tile.Label.text = caption;
                    tile.Label.color = captionColor;
                }
                else
                {
                    tile.Label.enableWordWrapping = false;
                    tile.Label.text = string.Empty;
                }
            }
            else
            {
                tile.Label.enableWordWrapping = true;
                tile.Label.color = Color.white;
                tile.Label.text = CardText(card, preview);
            }
        }

        /// <summary>
        /// Pushes a tile's colour towards whatever its pointer mark means. Applied last, so a mark always
        /// wins over the state shading: the whole point of lighting a socket is that the player can see it
        /// through an exhausted unit's dimming.
        /// </summary>
        static Color TintForMark(Tile tile, Color baseTint)
        {
            switch (tile.Mark)
            {
                case PickMark.Selected: return Color.Lerp(baseTint, SelectMark, 0.62f);
                case PickMark.AttackTarget: return Color.Lerp(baseTint, AttackMark, 0.52f);
                case PickMark.Blocker: return Color.Lerp(baseTint, BlockerMark, 0.52f);
                case PickMark.BlockTarget: return Color.Lerp(baseTint, BlockTargetMark, 0.52f);
                case PickMark.RetreatTarget: return Color.Lerp(baseTint, RetreatMark, 0.52f);
                default: return baseTint;
            }
        }

        /// <summary>
        /// The hover sheen, applied after every other colour. A quarter of the way to white is enough to
        /// read as "the pointer is here" without competing with the marks, which say something the player
        /// has to act on.
        /// PR-01: Only apply hover brightening to the actual hovered tile, not every tile.
        /// </summary>
        Color TintForHover(Tile tile, Color tint)
        {
            return tile != null && tile == hoveredTile ? Color.Lerp(tint, Color.white, 0.22f) : tint;
        }

        // ---- Board cards ------------------------------------------------------------

        /// <summary>
        /// Every card face currently standing on a socket, so the board can clear them all in one pass when it
        /// is rebuilt or re-bound. Mirrors <see cref="Tile.Card"/>, which is where a single socket's card is found.
        /// </summary>
        readonly List<CardView> boardCards = new List<CardView>();

        /// <summary>
        /// True when this view can draw a card at all. A face built without the catalogs is the legacy
        /// placeholder cube rather than the card the fan shows, so having neither is the same as having no board
        /// cards, and the caption tiles stay where they were.
        /// </summary>
        bool BoardCardsAvailable => frameCatalog != null || templateCatalog != null;

        /// <summary>
        /// Puts the real card face on a socket that has a card standing on it, and keeps it there across every
        /// repaint that follows.
        /// <para>
        /// Three things can change and they are handled apart, because they cost very differently. The socket's
        /// occupant can become a <em>different</em> card - a promotion off the Bench, a retreat, a replacement -
        /// and then the pooled face describes the wrong object entirely and is rebuilt. The same card's numbers
        /// can move, which is one re-projection through <see cref="CardView.Refresh"/> and nothing else. Or
        /// neither can change, which is ninety-nine frames in a hundred and costs a reference comparison.
        /// </para>
        /// </summary>
        /// <returns>True when a card is painted, which is what lets the caller narrow the socket's caption to the
        /// states a card cannot describe about itself.</returns>
        /// <summary>
        /// Where a socket's card gets its presentation record from.
        /// <para>
        /// Left empty the board asks the coordinator, which caches one <see cref="CardData"/> per live card
        /// instance — and that cache is what makes a card standing on the tabletop literally the same record the
        /// fan was holding, the identity both <c>HandController.SetHand</c> and <see cref="SyncBoardCard"/> key
        /// their view reuse on. Two records for one instance would not merely waste memory, it would make the same
        /// card draw from two sources and be free to disagree.
        /// </para>
        /// <para>
        /// Set it and the board draws cards with no match running, which is what makes this path testable at all:
        /// a <see cref="GameCoordinator"/> boots its match from a coroutine half a second into play mode, and
        /// tying a view's tests to that is the same mistake <see cref="Repaint"/> exists to avoid. Deliberately
        /// not serialized, so the Inspector never shows it and no scene can ship wired to a delegate.
        /// </para>
        /// </summary>
        public System.Func<CardInstance, CardData> cardDataResolver;

        CardData ResolveCardData(CardInstance card)
        {
            if (card == null)
            {
                return null;
            }

            if (cardDataResolver != null)
            {
                return cardDataResolver(card);
            }

            return coordinator != null ? coordinator.DataFor(card) : null;
        }

        bool SyncBoardCard(Tile tile, CardInstance card, MatchEngine engine, bool preview)
        {
            // The card hangs off the tile's holder, not off its cube, for the reason written in MakeTile: the
            // cube carries the non-uniform footprint, and a face parented to it would arrive flattened to
            // 0.06 of its height. A tile authored without a holder therefore gets no card rather than a wrong one.
            Transform holder = tile.Body != null ? tile.Body.transform.parent : null;
            if (holder == null || !BoardCardsAvailable)
            {
                ClearBoardCard(tile);
                return false;
            }

            CardData data = ResolveCardData(card);
            if (data == null)
            {
                // Nothing behind this socket to project a face from, which is the editor-preview case. The
                // caption tile is the honest rendering of a board with no match on it, rather than a card built
                // out of the placeholder fixture - a preview board is exactly where a fake card would be
                // mistaken for a real one.
                ClearBoardCard(tile);
                return false;
            }

            CardView view = tile.Card;
            if (view != null && !ReferenceEquals(view.Data, data))
            {
                ClearBoardCard(tile);
                view = null;
            }

            if (view == null)
            {
                view = CardView.CreateBoard(data, holder, FitSize(tile), frameCatalog, templateCatalog,
                    placeholderFixture, tierMaterials, BoardCardSortBaseline);
                tile.Card = view;
                boardCards.Add(view);
            }
            else
            {
                view.Refresh();
            }

            PoseBoardCard(tile, view);
            view.SetShade(ShadeFor(card, engine, preview));
            return true;
        }

        /// <summary>
        /// The largest card that fits this socket, keeping the frame's own aspect and covering only
        /// <see cref="boardCardFill"/> of the tile.
        /// <para>
        /// Fitted on the tighter axis, and the two axes disagree across the board: a Core Active socket is
        /// 1.10 x 1.54 against a card's 1.0 x 1.4615, so width would allow a scale of 1.01 while height allows
        /// 0.95, and taking the wider answer would spill the card past the top and bottom of the tile it stands
        /// on. A Node Active at 0.74 x 1.036 is the opposite way round. <c>min</c> is the only answer that is
        /// right on both, and it is what the fill factor is then applied to.
        /// </para>
        /// </summary>
        Vector3 FitSize(Tile tile)
        {
            Vector3 card = CardView.CardSize;
            float scale = Mathf.Min(tile.Size.x * boardCardFill / card.x,
                tile.Size.y * boardCardFill / card.y);
            return new Vector3(card.x * scale, card.y * scale, card.z * scale);
        }

        /// <summary>
        /// Lays the card onto its socket, facing the way the socket's own caption faces.
        /// <para>
        /// The caption's rotation is the source and not a convenience: it is the one orientation on this table
        /// that is known to read, because it is the one that is being read every frame. Copying it means a board
        /// re-posed in the scene takes its cards with it instead of stranding them face-down, and
        /// <see cref="boardCardExtraRotation"/> is the escape hatch if a future pose ever needs one.
        /// </para>
        /// </summary>
        void PoseBoardCard(Tile tile, CardView view)
        {
            Transform holder = tile.Body.transform.parent;
            if (view.transform.parent != holder)
            {
                view.transform.SetParent(holder, false);
            }

            // In front of the cube on the axis that faces the pitched camera - local -Z, the same rule the
            // caption follows at -half thickness less a hair - and a hair behind the caption, which is the one
            // thing on a socket that has to read over a card rather than under it.
            view.transform.localPosition = new Vector3(0f, 0f, -tile.Size.z * 0.5f - boardCardLift);
            view.transform.localRotation = (tile.Label != null
                ? tile.Label.transform.localRotation
                : Quaternion.identity) * Quaternion.Euler(boardCardExtraRotation);
            view.transform.localScale = Vector3.one;
        }

        /// <summary>
        /// The colour multiplied over a board card's whole face. <see cref="Color.white"/> is the card as
        /// authored, and that is what a ready unit gets: nothing is holding it back, so nothing is drawn on it.
        /// <para>
        /// These are the states a frame cannot carry. Dimming the entire face rather than one label is
        /// deliberate - a spent unit has to read as spent from across the table, and the attack number is the one
        /// detail a player is least likely to be looking at when they decide whether they may swing.
        /// </para>
        /// <para>
        /// Code constants, not serialized knobs, on the same argument as the pointer marks: these four colours
        /// are the difference between "this unit is available" and "this unit is not", and a tuning surface that
        /// lets a summoned unit land on the same shade as a ready one is a rules bug waiting for an artist.
        /// </para>
        /// </summary>
        static Color ShadeFor(CardInstance card, MatchEngine engine, bool preview)
        {
            if (preview)
            {
                // A nomination is a promise rather than a body. The engine holds no unit here yet, and the state
                // an offered card carries is an artefact of how it was constructed, so cooling the whole face is
                // what stops a preview reading as something already fielded.
                return new Color(0.70f, 0.76f, 0.88f);
            }

            if (engine != null && engine.IsBlocking(card.InstanceId))
            {
                return new Color(1f, 0.87f, 0.64f);
            }

            switch (card.State)
            {
                case UnitState.Summoned: return new Color(0.66f, 0.68f, 0.74f);
                case UnitState.Exhausted: return new Color(0.50f, 0.51f, 0.55f);
                default: return Color.white;
            }
        }

        /// <summary>
        /// The one line a card cannot say about itself, and the colour it draws in, for a socket that has a real
        /// card on it. False when the unit is simply ready, because an empty caption is the correct signal for
        /// "nothing is holding this one back" and a word there would be noise the player learns to ignore.
        /// <para>
        /// One switch for both the shade and the words, deliberately: the two are the same four states seen two
        /// ways, and the version of this with a separate colour function is one edit away from disagreeing with
        /// the one next to it.
        /// </para>
        /// </summary>
        static bool TryStateCaption(CardInstance card, MatchEngine engine, bool preview,
            out string caption, out Color color)
        {
            if (preview)
            {
                caption = "NOMINATION";
                color = new Color(0.74f, 0.80f, 0.94f);
                return true;
            }

            if (engine != null && engine.IsBlocking(card.InstanceId))
            {
                caption = "BLOCKING";
                color = new Color(1f, 0.88f, 0.68f);
                return true;
            }

            switch (card.State)
            {
                case UnitState.Summoned:
                    caption = "SUMMONING";
                    color = new Color(0.82f, 0.84f, 0.88f);
                    return true;
                case UnitState.Exhausted:
                    caption = "SPENT";
                    color = new Color(0.70f, 0.71f, 0.76f);
                    return true;
                default:
                    caption = string.Empty;
                    color = Color.white;
                    return false;
            }
        }

        /// <summary>Takes the card off one socket, if it carries one.</summary>
        void ClearBoardCard(Tile tile)
        {
            if (tile == null || tile.Card == null)
            {
                return;
            }

            boardCards.Remove(tile.Card);
            DestroyTarget(tile.Card.gameObject);
            tile.Card = null;
        }

        /// <summary>
        /// Takes every card off the board. Called when the board itself goes away, at which point the tile
        /// records are being rebuilt too, so there is no per-tile reference left to null.
        /// </summary>
        void ClearBoardCards()
        {
            for (int i = 0; i < boardCards.Count; i++)
            {
                if (boardCards[i] != null)
                {
                    DestroyTarget(boardCards[i].gameObject);
                }
            }

            boardCards.Clear();
        }

        static string CardText(CardInstance card, bool preview = false)
        {
            var sb = new StringBuilder();
            string name = card.Definition.Name;
            if (name.Length > 14)
            {
                name = name.Substring(0, 13) + "\u2026";
            }
            sb.Append(name).Append('\n');
            
            // PR-02: Only show combat stats for Units, not Relays/Protocols.
            if (card.Definition.Type == CardType.Unit)
            {
                sb.Append(card.CurrentAtk).Append('/').Append(card.CurrentHp);
                if (card.CurrentShield > 0)
                {
                    sb.Append(" +").Append(card.CurrentShield).Append('S');
                }
            }
            else
            {
                sb.Append(card.Definition.Type);
            }
            
            // A nomination is not a body on the board yet, and the state an offered card carries is an
            // artefact of its construction (Summoned, until CompleteSetup readies everything it fields), so
            // printing it here would advertise a summoning delay the lineup explicitly waives.
            if (!preview)
            {
                if (card.State == UnitState.Summoned)
                {
                    sb.Append(" (S)");
                }
                else if (card.State == UnitState.Exhausted)
                {
                    sb.Append(" (E)");
                }
            }
            return sb.ToString();
        }

        static CardInstance Occupied(BoardSlot slot)
        {
            return slot != null && slot.IsOccupied() ? slot.Card : null;
        }

        /// <summary>
        /// The card that stands on one of the player's Active sockets for the duration of the lineup setup: the
        /// offered Unit nominated for it, painted as a preview because no rule has been asked to place it, or the
        /// socket's real occupant when nothing is nominated there. Everything outside setup, and everything on the
        /// opponent's half, passes straight through to the occupant untouched.
        /// <para>
        /// Callers detect the substitution by the returned reference differing from the occupant they passed in,
        /// which is why a nomination cannot accidentally recolour a real body.
        /// </para>
        /// </summary>
        CardInstance PreviewFor(MatchState state, PlayerIndex who, int activeSocketIndex, CardInstance occupant)
        {
            if (who != PlayerIndex.Player || !state.SetupInProgress
                || activeSocketIndex < 0 || activeSocketIndex >= lineupPreview.Length)
            {
                return occupant;
            }

            int instanceId = lineupPreview[activeSocketIndex];
            if (instanceId == 0)
            {
                return occupant;
            }

            foreach (CardInstance offer in state.GetPlayer(PlayerIndex.Player).LineupOffer)
            {
                if (offer != null && offer.InstanceId == instanceId)
                {
                    return offer;
                }
            }

            return occupant;
        }

        /// <summary>
        /// A Node's Active socket counted in battlefield order, which is exactly how a lineup nomination is
        /// indexed and how <c>MatchEngine.ActiveSlotsInBattlefieldOrder</c> walks them: the Core rank first,
        /// then each Node's Active rank in turn.
        /// </summary>
        static int NodeActiveSocketIndex(int node, int slot)
        {
            return Board.CoreActiveSlots + node * Board.NodeActiveSlots + slot;
        }

        // ---- Primitive helpers ------------------------------------------------------

        // ---- Board card plumbing ----------------------------------------------------
        // A card face writes no depth, so where it lands in the pile is decided by sorting order alone - and the
        // tabletop renders through URP's 2D Renderer, which orders meshes and sprites by that one key. So a board
        // card's lane is a gap rather than a free choice: it has to clear the board's own geometry (table,
        // plates, gold rims and socket cubes all draw at the default 0) and still stay under the hand's lowest
        // slice, a discard flight at 50, so a card lifted out of the fan covers the unit it is played onto
        // instead of hiding behind it. Below zero does not mean "under the hand", it means under the TABLE: the
        // first version of this lane was negative, and a socket holding a unit kept showing nothing but its
        // caption, which is the failure no EditMode assertion about relative lanes could have seen.
        //
        // One baseline serves every socket. A face spends ten orders below its baseline on art and twenty above
        // it on text, so per-socket slices would cost 31 each and 36 sockets do not fit between 0 and 50. None
        // are needed: two board cards contend only where they overlap, and each is fitted inside its own socket
        // (see BoardCard_FitsInsideItsSocketOnTheTighterAxis) while the authored sockets are disjoint. That
        // disjointness is also what keeps one socket's art mask off another's art - every mask on the board
        // shares one range, so only the pixels decide whether it bites. Widening boardCardFill past 1.0 in the
        // scene is what would break that, and the fit test is what says not to.
        const int BoardCardSortBaseline = 20;

        /// <summary>
        /// Where a socket's caption draws: over the card standing on it, under the hand.
        /// <para>
        /// The caption is the only place the states a frame cannot speak for itself get named - SUMMONING,
        /// BLOCKING, SPENT, NOMINATION - so letting a card bury the caption that annotates it would take the
        /// message straight back off the board.
        /// </para>
        /// </summary>
        const int BoardCaptionSortOrder = 45;

        /// <summary>
        /// Raises one socket's caption into the lane above the cards. Both construction paths call it, because a
        /// caption bound from the prefab arrives at the default 0 exactly as a built one does.
        /// </summary>
        static void ApplyCaptionSorting(TextMeshPro label)
        {
            Renderer renderer = label != null ? label.GetComponent<Renderer>() : null;
            if (renderer != null)
            {
                renderer.sortingOrder = BoardCaptionSortOrder;
            }
        }

        /// <summary>
        /// Find-or-create an empty grouping node. Groups carry no geometry and are created at the identity,
        /// so every child's local position is still expressed in board-root units and the layout constants
        /// above mean exactly what they always meant.
        /// <para>
        /// They exist purely so the board is navigable in the Scene view: the Core, each Node, and each
        /// half's Active and Bench ranks are individually selectable, which makes nudging a whole rank into
        /// place one drag instead of eleven. Binding resolves tiles through a name index rather than a
        /// child path, so regrouping never breaks the runtime view - and a group offset you author in the
        /// editor is respected, because binding never rewrites a transform it did not create.
        /// </para>
        /// </summary>

        static Transform Group(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                return existing;
            }

            var group = new GameObject(name);
            group.transform.SetParent(parent, false);
            group.transform.localPosition = Vector3.zero;
            group.transform.localRotation = Quaternion.identity;
            group.transform.localScale = Vector3.one;
            return group.transform;
        }

        Renderer MakeBody(Transform parent, string name, Vector3 pos, Vector3 size, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;

            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Body";
            cube.transform.SetParent(go.transform, false);
            cube.transform.localScale = size;
            Collider collider = cube.GetComponent<Collider>();
            if (collider != null)
            {
                DestroyTarget(collider);
            }

            Renderer renderer = cube.GetComponent<Renderer>();
            renderer.sharedMaterial = MaterialFor(color, name);
            return renderer;
        }

        /// <summary>
        /// The material one body renders with: its family's skin when the scene has assigned one, and the
        /// material this colour generates when it has not.
        /// <para>
        /// A skin short-circuits the generator, which is the whole point of it. Every body in the family then
        /// shares one material, so they draw in one batch, and in play mode one fewer <c>new Material</c> is
        /// allocated per board. The colour still arrives - through the per-frame property block in
        /// <see cref="SetBodyColor"/> - which is why a skin multiplies rather than replaces.
        /// </para>
        /// </summary>
        Material MaterialFor(Color color, string name)
        {
            Material skin = SkinFor(SurfaceFor(name));
            return skin != null ? skin : MaterialForColor(color);
        }

        /// <summary>
        /// The slot a surface family reads from. Five named fields rather than a keyed table, so the Inspector
        /// shows exactly what a skin can replace and nothing else.
        /// </summary>
        Material SkinFor(Surface surface)
        {
            switch (surface)
            {
                case Surface.Table: return tableSkin;
                case Surface.Socket: return socketSkin;
                case Surface.Plate: return plateSkin;
                case Surface.Pile: return pileSkin;
                case Surface.Rim: return rimSkin;
                default: return null;
            }
        }

        /// <summary>
        /// Re-reads the five skin slots against a board that already exists, and changes nothing else.
        /// <para>
        /// The bound path needs it for the reason written at its call site. The Inspector needs it because a
        /// gear-menu item is the only way a serialized field can act on a live scene without a custom editor:
        /// drop five materials on the component, click Apply, and the tabletop changes under the cursor instead
        /// of waiting for somebody to rebuild a board or re-run a scene builder.
        /// </para>
        /// <para>
        /// One-directional on purpose. An empty slot leaves whatever the body is already wearing, because a
        /// cleared slot cannot recover the colour that generated the material in the first place - and guessing
        /// it from the current material would trade a hand-authored per-tile material for a generated one of
        /// the same shade, which is the opposite of what an artist just did. Back to the shipped look means
        /// rebuilding the board, which consults the palette from scratch.
        /// </para>
        /// </summary>
        [ContextMenu("Apply Board Skins")]
        public void ApplySkins()
        {
            if (boardRoot == null)
            {
                return;
            }

            foreach (Renderer renderer in boardRoot.GetComponentsInChildren<Renderer>(true))
            {
                // Every cube MakeBody builds is named Body and hangs off a holder that carries the tile's
                // name, which is what the family is keyed off - the same shape FindTile binds against.
                if (renderer.name != "Body" || renderer.transform.parent == null)
                {
                    continue;
                }

                Material skin = SkinFor(SurfaceFor(renderer.transform.parent.name));
                if (skin != null)
                {
                    renderer.sharedMaterial = skin;
                }
            }

            // Snapshot taken here rather than at the call sites, so every route into this method - the gear
            // menu, the bind path, the per-frame check below - leaves the same truth behind it.
            appliedTableSkin = tableSkin;
            appliedSocketSkin = socketSkin;
            appliedPlateSkin = plateSkin;
            appliedPileSkin = pileSkin;
            appliedRimSkin = rimSkin;
        }

        // The five slots as they stood the last time the board was walked. A skin dropped mid-match has to
        // show up on the next frame or the slots are a build-time curiosity rather than a tuning surface, but
        // walking every renderer on the table to find out whether one of five references moved is not a
        // per-frame cost worth paying. So the comparison is five field reads and the walk only happens when
        // one of them disagrees - the same derived-not-remembered shape the control rail publishes with.
        Material appliedTableSkin;
        Material appliedSocketSkin;
        Material appliedPlateSkin;
        Material appliedPileSkin;
        Material appliedRimSkin;

        /// <summary>
        /// Walks the board only if one of the five slots has moved since the last walk.
        /// <para>
        /// Public because <c>LateUpdate</c> is a one-line caller and EditMode never ticks a MonoBehaviour, which
        /// is the same reason <see cref="HandIdleSinker"/> hosts its arithmetic in statics and
        /// <see cref="BuildBoard"/> is public at all: the behaviour has to be reachable from a test to be
        /// behaviour rather than hope.
        /// </para>
        /// </summary>
        public void ApplySkinsIfChanged()
        {
            if (tableSkin == appliedTableSkin && socketSkin == appliedSocketSkin &&
                plateSkin == appliedPlateSkin && pileSkin == appliedPileSkin &&
                rimSkin == appliedRimSkin)
            {
                return;
            }

            ApplySkins();
        }

        /// <summary>
        /// The material a tile body renders with when no skin claims its family.
        /// <para>
        /// In <b>edit mode</b> this must resolve to a real <c>.mat</c> asset on disk. A Material built in
        /// memory is not an asset, so saving the board as a prefab silently collapses every one of them
        /// onto a single shared material - and every tile then reads back the same colour at bind time.
        /// In <b>play mode</b> an in-memory Material is enough: nothing is being serialized. The per-frame
        /// repaint never writes to it either - <see cref="SetBodyColor"/> overrides the colour through a
        /// <c>MaterialPropertyBlock</c> - so one instance serves every tile on the board and they all still
        /// draw in the same pass.
        /// </para>
        /// </summary>
        static Material MaterialForColor(Color color)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                return EditorMaterial(color);
            }
#endif
            var material = new Material(FindLitShader());
            material.color = color;
            return material;
        }

        /// <summary>
        /// URP Lit when the pipeline is present, falling back to the built-in lit shader so a missing or
        /// renamed pipeline reference degrades to "slightly wrong shading" instead of a null shader
        /// throwing halfway through a board build.
        /// </summary>
        static Shader FindLitShader()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }
            return shader;
        }

#if UNITY_EDITOR
        const string MaterialFolder = "Assets/Materials/Battlefield";

        /// <summary>
        /// Get-or-create the persistent material for one empty-slot colour, keyed by hex so every
        /// distinct tint owns exactly one asset and the prefab stores real asset references.
        /// </summary>
        static Material EditorMaterial(Color color)
        {
            string path = MaterialFolder + "/Slot_" + ColorUtility.ToHtmlStringRGB(color) + ".mat";

            Material existing = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                return existing;
            }

            var material = new Material(FindLitShader());
            material.color = color;

            if (!System.IO.Directory.Exists(MaterialFolder))
            {
                System.IO.Directory.CreateDirectory(MaterialFolder);
                UnityEditor.AssetDatabase.Refresh();
            }

            UnityEditor.AssetDatabase.CreateAsset(material, path);
            UnityEditor.AssetDatabase.SaveAssets();
            return material;
        }
#endif

        /// <summary>
        /// DestroyImmediate outside play mode, Destroy inside it. The board is now buildable from an
        /// editor menu so it can be saved as a prefab, and Destroy() refuses to run outside play mode.
        /// </summary>
        static void DestroyTarget(Object target)
        {
            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        TextMeshPro MakeLabel(Transform parent, string name, Vector3 pos, Vector2 size, float fontSize,
            FontStyles style)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = Vector3.one * 0.02f;

            var tmp = go.AddComponent<TextMeshPro>();
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Overflow;
            // World-space TMP must be flagged orthographic regardless of the render camera. With this left
            // false (the naive "perspective camera" guess) TMP lays the glyphs out at a tiny fraction of the
            // font size, so the labels are effectively invisible. CardView sets this the same way.
            tmp.isOrthographic = true;
            tmp.color = Color.white;
            // At localScale 0.02 one TMP unit is 0.02 world units.
            tmp.rectTransform.sizeDelta = size / 0.02f;
            return tmp;
        }

        Tile MakeTile(Transform parent, string name, Vector3 pos, Vector3 size, Color emptyColor,
            string emptyLabel, float fontSize, bool socketBorder = false)
        {
            Renderer body = MakeBody(parent, name, pos, size, emptyColor);
            // MakeBody hands back the cube's renderer; the socket object is its parent, at the identity.
            // Children must hang off that, not off the cube, or they inherit the cube's non-uniform scale
            // (a tile cube is 0.06 thick in Z, which would flatten a rim authored behind it to nothing).
            Transform holder = body.transform.parent;

            // The gold rim and the caption are children of the tile rather than siblings of it, so picking
            // one socket in the editor and dragging it moves all three pieces together instead of stranding
            // the label and the border behind at the old position.
            if (socketBorder)
            {
                // Slightly larger cube just behind the tile (larger Z is further from the pitched
                // camera), so a gold rim peeks out around the Active slot and keeps identifying it even
                // once a unit is deployed there and the fill turns into an owner tint.
                MakeBody(holder, "Socket", new Vector3(0f, 0f, 0.05f),
                    new Vector3(size.x + 0.06f, size.y + 0.06f, 0.04f), activeRim);
            }

            TextMeshPro label = MakeLabel(holder, "Label", new Vector3(0f, 0f, -size.z * 0.5f - 0.004f),
                new Vector2(size.x * 0.94f, size.y * 0.94f), fontSize, FontStyles.Normal);
            label.enableAutoSizing = true;
            label.fontSizeMin = 6f;
            label.fontSizeMax = fontSize;
            label.enableWordWrapping = true;
            label.text = emptyLabel;

            var tile = new Tile
            {
                Body = body,
                Label = label,
                Name = name,
                Size = size,
                BaseColor = emptyColor,
                EmptyLabel = emptyLabel,
            };
            // Same name-keyed identity as the prefab-bind path, so a board built from scratch and a board
            // bound from the prefab resolve drop targets identically.
            ApplyCaptionSorting(label);
            ApplyTileIdentity(tile, name);
            return tile;
        }

        /// <summary>
        /// One socket on the tabletop. The last three fields pin the tile to the Core <see cref="BoardSlot"/>
        /// it renders, which is what makes a drag-and-drop target resolvable. Only the player's own hosting
        /// sockets carry meaningful values: the Node plates, the Protocol tiles and the whole opponent half
        /// are never probed as drop targets, so they leave the identity at its default.
        /// </summary>
        public sealed class Tile
        {
            public Renderer Body;
            public TextMeshPro Label;
            public Color BaseColor;
            public string EmptyLabel;
            public SlotType SlotType;
            public int SlotIndex;
            public int NodeIndex;

            /// <summary>
            /// The authored footprint of this tile's cube: x wide, y tall, z thick, in board units.
            /// <para>
            /// Carried on the record rather than measured off the renderer, because the thing that has to fit
            /// inside it is a card, and <c>Renderer.bounds</c> is the pitched board's answer to a question the
            /// tile was built knowing. Both construction paths fill it: the build path from the size it was
            /// handed, the bind path from the cube's own local scale.
            /// </para>
            /// </summary>
            public Vector3 Size;

            /// <summary>
            /// The real card face standing on this socket, or null when it carries none.
            /// <para>
            /// Pooled per tile across repaints. The board repaints every frame for a socket whose occupant
            /// changes a few times a match, so a card is created when its socket gains an occupant, re-projected
            /// when that occupant's numbers move, and destroyed when the socket empties or a <em>different</em>
            /// card takes it. See <see cref="SyncBoardCard"/>.
            /// </para>
            /// </summary>
            public CardView Card;

            /// <summary>
            /// The tile holder's name, which is the one thing both construction paths agree on and therefore
            /// what the click layer reads to tell the deck pile from the discard pile.
            /// </summary>
            public string Name;

            /// <summary>Whose side of the table this tile is on. Meaningless for plates, piles and furniture.</summary>
            public PlayerIndex Owner;

            /// <summary>What the tile is, which is broader than the drop contract above.</summary>
            public TileRole Role = TileRole.Other;

            /// <summary>How the pointer layer should paint this tile. None leaves the ordinary state colours alone.</summary>
            public PickMark Mark = PickMark.None;
        }

        /// <summary>
        /// What a tile <em>is</em>, read off its name. The drop contract
        /// (<see cref="TryParseSlotIdentity"/>) only recognises the player's own hosting sockets, because
        /// nothing else may be deployed onto; clicking is much broader, so the view needs a coarser label for
        /// every object on the table.
        /// </summary>
        public enum TileRole
        {
            /// <summary>Board furniture: the tabletop slab, and anything not yet given a role.</summary>
            Other,

            /// <summary>An Active or Bench socket, either player's.</summary>
            Socket,

            /// <summary>The Protocol socket, one per player.</summary>
            Protocol,

            /// <summary>A Node's plate, behind its two ranks.</summary>
            Plate,

            /// <summary>
            /// A player's Core plate, behind their two Core ranks. Unlike a Node plate this one carries a
            /// rule: hitting the opponent's is an unblocked direct Core strike.
            /// </summary>
            Core,

            /// <summary>The deck or the discard stack.</summary>
            Pile,
        }

        /// <summary>
        /// Why the pointer layer is drawing attention to a tile. The colours are chosen so a tile can only
        /// ever carry one of these at a time, which is what makes <see cref="Tile.Mark"/> a single field
        /// rather than a set of overlapping flags an editor could author into a contradiction.
        /// </summary>
        public enum PickMark
        {
            None,

            /// <summary>The unit the player has picked to attack or block with.</summary>
            Selected,

            /// <summary>A unit the selected attacker may hit.</summary>
            AttackTarget,

            /// <summary>A unit that may be committed as a blocker.</summary>
            Blocker,

            /// <summary>An enemy attacker that may be blocked.</summary>
            BlockTarget,

            /// <summary>The opponent's Core, when the selected attacker may hit it.</summary>
            CoreTarget,

            /// <summary>
            /// One of the player's own Bench units the selected Active may be traded for. Still disjoint from
            /// <see cref="AttackTarget"/> by construction rather than by care: a retreat target is a card this
            /// player owns and an attack target is one they do not, so no card can be both and the single
            /// <c>Tile.Mark</c> field stays a field.
            /// </summary>
            RetreatTarget,
        }
    }
}
