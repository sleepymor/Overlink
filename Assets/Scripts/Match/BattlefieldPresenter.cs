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
        public float headerY = 5.35f;

        [Header("Event log")]
        [Tooltip("The screen-space panel the match log renders into. Found automatically; created on the " +
                 "first refresh when the scene has none, so the legacy standalone hand still gets a log.")]
        public BattlefieldHud hud;

        // ---- Tabletop -------------------------------------------------------------
        // The tabletop is sized to its content, not to the log column it used to reserve on the right. The
        // widest content is the pair of outer Node plates, which reach x = +/-6.9, so 16.5 units leaves a
        // deliberate margin at each edge and nothing more.
        //
        // Public because the camera's overhead tier is defined by these numbers: it exists to frame exactly
        // this rectangle, and its test reads them rather than copying them so the two cannot drift apart.
        public const float TableWidth = 16.5f;
        public const float TableDepth = 11.4f;
        const float TableCenterY = 0.3f;

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
        const float CorePlayerBenchY = -2.5f;
        const float CoreOpponentActiveY = 1.12f;
        const float CoreOpponentBenchY = 2.5f;
        const float CoreSpacing = 0.9f;
        // Clears the wider Active: half its width (0.55) + half the Protocol's (0.36) + a 0.14 gutter.
        const float CoreProtocolX = 1.05f;
        static readonly Vector3 CoreActiveSize = new Vector3(1.10f, 1.54f, 0.06f);
        // The back rank is a touch smaller than the Protocol socket beside the Active. Being the furthest
        // thing from the centre line it is also the furthest from the camera, and a slightly tighter
        // footprint is what leaves the status strip a clear band between here and the hand fan.
        static readonly Vector3 CoreBenchSize = new Vector3(0.66f, 0.924f, 0.06f);
        static readonly Vector3 CoreProtocolSize = new Vector3(0.72f, 1.008f, 0.06f);

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
        // A card back, deliberately cooler and darker than any empty socket so a face-down stack can never
        // be mistaken for somewhere a card can be dropped, and the spent pile warmer and greyer still.
        static readonly Color DeckTint = new Color(0.13f, 0.19f, 0.25f);
        static readonly Color DiscardTint = new Color(0.21f, 0.17f, 0.14f);

        // Status strips live in the clear centre column above/below the Core rows, so they never overlap
        // the corner Node plates.
        const float PlayerStatusY = -3.1f;
        const float OpponentStatusY = 3.1f;

        // Depth stacking. Local -Z faces the pitched camera, so a smaller Z draws on top of a larger one:
        // cards above their plates, caption text above the plates, tabletop furthest back.
        const float CardZ = 0f;
        const float PlateZ = 0.07f;
        const float LabelZ = 0.02f;
        const float TableZ = 0.14f;

        // The control legend is code-owned, not authored: the prefab binds by name and would otherwise
        // keep whatever stale string was baked into it the last time the prefab was rebuilt. Binding
        // re-applies both the size and the text so a key added to GameCoordinator shows up without a
        // prefab rebuild.
        //
        // Two lines, because the mouse layer doubled what the legend has to say: at 11pt a single line of
        // this length is ~13 board units wide against a 16.5-unit table, and the third bullet point was
        // already running off the edge.
        const float ControlsHintFontSize = 11f;
        static readonly Vector2 ControlsHintSize = new Vector2(17f, 0.78f);
        const string ControlsHintText =
            "Click a card to read it   \u2022   Click a Ready unit, then a red socket, to attack   \u2022   " +
            "Drag onto a lit socket to deploy\n" +
            "\u2190/\u2192 browse   \u2022   ESC cancel   \u2022   P phase (plays need MAIN)   \u2022   E end turn   \u2022   " +
            "Q concede   \u2022   1-5 attack   \u2022   Wheel zoom   \u2022   RMB pan";

        static readonly string[] NodeCaption =
        {
            "NODE 1  +1 ATK",
            "NODE 2  +1 HP",
            "NODE 3  FIRST STRIKE",
            "NODE 4  DRAW ON KILL",
        };

        static readonly Color PlayerTint = new Color(0.20f, 0.42f, 0.72f);
        static readonly Color OpponentTint = new Color(0.62f, 0.24f, 0.24f);
        // Empty-slot colours: the Active socket is deliberately the brightest thing on the board so a
        // row of [Active][Bench][Bench] never reads as three identical slots. Protocol gets its own hue.
        static readonly Color ActiveEmpty = new Color(0.30f, 0.27f, 0.19f);
        static readonly Color BenchEmpty = new Color(0.13f, 0.15f, 0.19f);
        static readonly Color ProtocolEmpty = new Color(0.23f, 0.18f, 0.31f);
        // Gold border drawn behind each Active tile; survives deployment, unlike the tile's own tint.
        static readonly Color ActiveRim = new Color(0.80f, 0.64f, 0.28f);
        static readonly Color TableTint = new Color(0.09f, 0.11f, 0.15f);
        static readonly Color NodeFree = new Color(0.26f, 0.28f, 0.32f);
        static readonly Color NodePlayer = new Color(0.14f, 0.48f, 0.28f);
        static readonly Color NodeOpponent = new Color(0.56f, 0.38f, 0.12f);
        static readonly Color BlockMark = new Color(1f, 0.84f, 0.20f);

        // Pointer marks. Chosen to be distinguishable from the owner tints they are drawn over - blue for the
        // player's own units, warm red/orange for the enemy's - so "I picked this" and "I could hit this"
        // never read as the same shade on opposite halves of the table.
        static readonly Color SelectMark = new Color(0.45f, 0.95f, 1.00f);
        static readonly Color AttackMark = new Color(1.00f, 0.42f, 0.32f);
        static readonly Color BlockerMark = new Color(0.42f, 0.95f, 0.55f);
        static readonly Color BlockTargetMark = new Color(1.00f, 0.72f, 0.30f);
        static readonly Color CoreTargetMark = new Color(1.00f, 0.55f, 0.35f);
        static readonly Color StatusIdle = new Color(0.92f, 0.95f, 1f);

        Transform boardRoot;

        // Core battlefield tiles.
        Tile[] playerCoreActive, playerCoreBench;
        Tile[] opponentCoreActive, opponentCoreBench;
        Tile playerProtocol, opponentProtocol;

        // Node battlefield tiles, [node][slot].
        Tile[] nodePlate;
        Tile[][] nodePlayerActive, nodePlayerBench;
        Tile[][] nodeOpponentActive, nodeOpponentBench;
        TextMeshPro[] nodeHeader;

        // The deck and discard stacks. Not sockets: they carry no slot identity and are never probed as drop
        // targets, because nothing is played TO the deck or the discard from the hand.
        Tile deckPile, discardPile;

        TextMeshPro headerText, playerStatus, opponentStatus, victoryText, controlsHint;
        Tile highlightedTile;

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
        /// Binds the screen-space log panel. The gameplay scene ships one as an editable root object; when
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
                new Vector3(TableWidth, TableDepth, 0.05f), TableTint);

            headerText = MakeLabel(boardRoot, "Header", new Vector3(0f, headerY, LabelZ),
                new Vector2(17f, 0.7f), 32, FontStyles.Bold);
            headerText.alignment = TextAlignmentOptions.Center;

            controlsHint = MakeLabel(boardRoot, "ControlsHint", new Vector3(0f, headerY - 0.62f, LabelZ),
                new Vector2(17f, 0.42f), ControlsHintFontSize, FontStyles.Normal);
            controlsHint.alignment = TextAlignmentOptions.Center;
            controlsHint.color = new Color(0.60f, 0.68f, 0.80f);
            ApplyControlsHint(controlsHint);

            BuildCoreBattlefield();
            BuildNodeBattlefields();
            BuildPiles();

            Transform status = Group(boardRoot, "Status");
            opponentStatus = MakeStatusStrip(status, "OStatus", OpponentStatusY);
            playerStatus = MakeStatusStrip(status, "PStatus", PlayerStatusY);

            victoryText = MakeLabel(boardRoot, "Victory", new Vector3(0f, 0.1f, -0.25f),
                new Vector2(14f, 2f), 72, FontStyles.Bold);
            victoryText.text = "";

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
            IndexHierarchy(root);

            headerText = FindLabel("Header");
            controlsHint = FindLabel("ControlsHint");
            // Code-owned, so binding writes it rather than trusting whatever the prefab last baked in.
            ApplyControlsHint(controlsHint);
            opponentStatus = FindLabel("OStatus");
            playerStatus = FindLabel("PStatus");
            victoryText = FindLabel("Victory");

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

            for (int i = 0; i < benchCount; i++)
            {
                opponentCoreBench[i] = FindTile($"OppCoreBench{i}");
                playerCoreBench[i] = FindTile($"PlayCoreBench{i}");
            }

            nodePlate = new Tile[Board.NodeCount];
            nodeHeader = new TextMeshPro[Board.NodeCount];
            nodePlayerActive = new Tile[Board.NodeCount][];
            nodePlayerBench = new Tile[Board.NodeCount][];
            nodeOpponentActive = new Tile[Board.NodeCount][];
            nodeOpponentBench = new Tile[Board.NodeCount][];

            for (int n = 0; n < Board.NodeCount; n++)
            {
                nodePlate[n] = FindTile($"NodePlate{n}");
                nodeHeader[n] = FindLabel($"NodeHeader{n}");
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

            return !bindFailed && root.Find("Table/Body") != null;
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

        TextMeshPro FindLabel(string name)
        {
            Transform found = FindNamed(name);
            var label = found != null ? found.GetComponent<TextMeshPro>() : null;
            if (label == null)
            {
                bindFailed = true;
            }
            return label;
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
                // Derived from the slot's NAME, never read back off its material. The prefab cannot be
                // relied on to carry a per-tile colour (inline Materials are not assets, so a prefab save
                // collapses them onto one shared material), but the name always identifies the slot kind,
                // and that is exactly what the build path used to pick the colour in the first place.
                BaseColor = BaseColorForSlot(name),
                EmptyLabel = label.text,
            };
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
        /// The empty-slot colour a tile is built with, keyed off its name. Shared by the build path and
        /// the bind path so a bound prefab repaints identically to a procedurally built board.
        /// </summary>
        static Color BaseColorForSlot(string name)
        {
            if (name == "DeckPile") return DeckTint;
            if (name == "DiscardPile") return DiscardTint;
            if (name.StartsWith("Table")) return TableTint;
            if (name.Contains("Socket")) return ActiveRim;
            if (name.StartsWith("NodePlate")) return NodeFree;
            if (name.Contains("Active")) return ActiveEmpty;
            if (name.Contains("Bench")) return BenchEmpty;
            if (name.Contains("Protocol")) return ProtocolEmpty;
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

            // Caption sits in the dead band between the two facing Active ranks, at the table centre.
            MakeLabel(core, "CoreCaption", new Vector3(0f, 0f, LabelZ),
                new Vector2(6f, 0.36f), 15, FontStyles.Bold).text = "CORE BATTLEFIELD";

            // Each half splits into the three things you might want to reposition independently: the front
            // rank, the Protocol socket beside it, and the back rank.
            Transform playerActiveGroup = Group(player, "Active");
            Transform playerProtocolGroup = Group(player, "Protocol");
            Transform playerBenchGroup = Group(player, "Bench");
            Transform opponentActiveGroup = Group(opponent, "Active");
            Transform opponentProtocolGroup = Group(opponent, "Protocol");
            Transform opponentBenchGroup = Group(opponent, "Bench");

            // Front rank: the Active socket on the board's centre line, with the player's single Protocol
            // socket beside it at Bench size. This rank sits nearer the middle of the table, so the Active
            // card faces the opponent and the card behind it (the Bench) never blocks it from the camera.
            for (int i = 0; i < activeCount; i++)
            {
                opponentCoreActive[i] = MakeTile(opponentActiveGroup, $"OppCoreActive{i}",
                    new Vector3(0f, CoreOpponentActiveY, CardZ), CoreActiveSize, ActiveEmpty,
                    "ACTIVE", 20, true);
                playerCoreActive[i] = MakeTile(playerActiveGroup, $"PlayCoreActive{i}",
                    new Vector3(0f, CorePlayerActiveY, CardZ), CoreActiveSize, ActiveEmpty,
                    "ACTIVE", 20, true);
            }

            opponentProtocol = MakeTile(opponentProtocolGroup, "OppProtocol",
                new Vector3(CoreProtocolX, CoreOpponentActiveY, CardZ), CoreProtocolSize, ProtocolEmpty, "PROT", 12);
            playerProtocol = MakeTile(playerProtocolGroup, "PlayProtocol",
                new Vector3(CoreProtocolX, CorePlayerActiveY, CardZ), CoreProtocolSize, ProtocolEmpty, "PROT", 12);

            // Back rank: the four Bench sockets, behind the Active rank, toward its owner's own edge.
            for (int i = 0; i < benchCount; i++)
            {
                float x = CoreSlotX(i, benchCount);
                opponentCoreBench[i] = MakeTile(opponentBenchGroup, $"OppCoreBench{i}",
                    new Vector3(x, CoreOpponentBenchY, CardZ), CoreBenchSize, BenchEmpty, "BENCH", 14);
                playerCoreBench[i] = MakeTile(playerBenchGroup, $"PlayCoreBench{i}",
                    new Vector3(x, CorePlayerBenchY, CardZ), CoreBenchSize, BenchEmpty, "BENCH", 14);
            }
        }

        static float CoreSlotX(int index, int total)
        {
            return (index - (total - 1) * 0.5f) * CoreSpacing;
        }

        void BuildNodeBattlefields()
        {
            nodePlate = new Tile[Board.NodeCount];
            nodeHeader = new TextMeshPro[Board.NodeCount];
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

                nodePlate[n] = MakeTile(node, $"NodePlate{n}",
                    new Vector3(0f, 0f, PlateZ), NodePlateSize, NodeFree, "", 14);

                // Caption sits in the dead band between the two facing Active ranks, at the node centre.
                nodeHeader[n] = MakeLabel(node, $"NodeHeader{n}", new Vector3(0f, 0f, LabelZ),
                    new Vector2(NodePlateSize.x, 0.26f), 13, FontStyles.Bold);
                nodeHeader[n].text = NodeCaption[n % NodeCaption.Length];

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
                        new Vector3(x, NodeOpponentActiveY, CardZ), NodeActiveSize, ActiveEmpty,
                        "ACTIVE", 13, true);
                    nodePlayerActive[n][i] = MakeTile(playerActiveGroup, $"NodePlayActive{n}_{i}",
                        new Vector3(x, NodePlayerActiveY, CardZ), NodeActiveSize, ActiveEmpty,
                        "ACTIVE", 13, true);
                }

                // Back rank: the Bench sockets, behind the Active rank, toward its owner's own edge.
                for (int i = 0; i < Board.NodeBenchSlots; i++)
                {
                    float x = NodeSlotOffset(i, Board.NodeBenchSlots);
                    nodeOpponentBench[n][i] = MakeTile(opponentBenchGroup, $"NodeOppBench{n}_{i}",
                        new Vector3(x, NodeOpponentBenchY, CardZ), NodeBenchSize, BenchEmpty, "BENCH", 11);
                    nodePlayerBench[n][i] = MakeTile(playerBenchGroup, $"NodePlayBench{n}_{i}",
                        new Vector3(x, NodePlayerBenchY, CardZ), NodeBenchSize, BenchEmpty, "BENCH", 11);
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

            deckPile = MakePile(piles, "DeckPile", DeckPilePosition, "DECK", DeckTint);
            discardPile = MakePile(piles, "DiscardPile", DiscardPilePosition, "DISCARD", DiscardTint);
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

        TextMeshPro MakeStatusStrip(Transform parent, string name, float y)
        {
            TextMeshPro tmp = MakeLabel(parent, name, new Vector3(0f, y, LabelZ),
                new Vector2(7f, 0.4f), 16, FontStyles.Normal);
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(0.92f, 0.95f, 1f);
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 8f;
            tmp.fontSizeMax = 16f;
            return tmp;
        }

        // ---- Drop targeting ---------------------------------------------------------

        /// <summary>
        /// Hit-tests the player's own slot tiles against a world position and highlights the closest match.
        /// <para>
        /// Only the player's half is probed: you cannot deploy onto the opponent's ranks. The Protocol socket
        /// is excluded because nothing is targeted there (Abilities take no slot and a Protocol has exactly
        /// one home, which the engine already enforces). Depth is ignored on purpose, since a dragged card
        /// floats above the table on the board's local Z.
        /// </para>
        /// Returns the slot under the point, or null when there is none. Legality (card type, occupancy) is
        /// <b>not</b> judged here: this view has no rules, so <see cref="GameCoordinator"/> validates the
        /// candidate and clears the highlight when the drop would be illegal.
        /// </summary>
        public BoardSlot FindDropTarget(Vector3 worldPos)
        {
            if (boardRoot == null || playerCoreActive == null)
            {
                return null;
            }

            Vector3 point = boardRoot.InverseTransformPoint(worldPos);
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

        // The two status strips double as the Core counters, and therefore as the target of an unblocked
        // direct attack (GDD 2.1.6 Step 4). Their footprint is MakeStatusStrip's rect, in board-root units.
        static readonly Vector2 PlayerCoreStripCenter = new Vector2(0f, PlayerStatusY);
        static readonly Vector2 OpponentCoreStripCenter = new Vector2(0f, OpponentStatusY);
        internal const float CoreStripHalfWidth = 3.5f;
        internal const float CoreStripHalfHeight = 0.2f;

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

            for (int i = 0; i < pickables.Count; i++)
            {
                if (!TryHit(pickables[i], point, out float distance))
                {
                    continue;
                }

                if (best == null || distance < bestDistance)
                {
                    best = pickables[i];
                    bestDistance = distance;
                }
            }

            BoardHit hit = best == null ? null : FromTile(best);

            // The Core counters compete on the same nearest-centre rule, so a click that lands on both a
            // strip and a socket gets the socket: it is the smaller, more specific target.
            if (WithinStrip(OpponentCoreStripCenter, point, out float opponentDistance)
                && (hit == null || opponentDistance < bestDistance))
            {
                hit = new BoardHit { Kind = BoardHitKind.OpponentCore, Owner = PlayerIndex.Opponent };
                bestDistance = opponentDistance;
            }

            if (WithinStrip(PlayerCoreStripCenter, point, out float playerDistance)
                && (hit == null || playerDistance < bestDistance))
            {
                hit = new BoardHit { Kind = BoardHitKind.PlayerCore, Owner = PlayerIndex.Player };
            }

            return hit;
        }

        static bool WithinStrip(Vector2 center, Vector3 point, out float distanceSquared)
        {
            float dx = point.x - center.x;
            float dy = point.y - center.y;
            distanceSquared = dx * dx + dy * dy;
            return Mathf.Abs(dx) <= CoreStripHalfWidth && Mathf.Abs(dy) <= CoreStripHalfHeight;
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

        /// <summary>Glows the opponent's Core counter, which is the click target for an unblocked direct attack.</summary>
        public void MarkCoreTarget(bool on)
        {
            coreMarked = on;
        }

        /// <summary>
        /// Whether the opponent's Core is currently lit as an attack target. It has no cube to tint - the
        /// counter is a text strip - so this is how anything outside the view (a test, or a future cursor
        /// state) can tell that the glow is on.
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
        /// </summary>
        public void ShowAttack(CardInstance attacker, IReadOnlyList<CardInstance> targets, bool coreTargetable)
        {
            ClearMarks();
            MarkCards(attacker == null ? null : new[] { attacker }, PickMark.Selected);
            MarkCards(targets, PickMark.AttackTarget);
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

        /// <summary>How long a rejection reason stays painted under the header.</summary>
        const float NoticeSeconds = 4f;

        TextMeshPro noticeText;
        float noticeUntil;
        int deployPulseCardId = -1;
        float deployPulseUntil;

        /// <summary>
        /// Shows a short-lived explanation under the header. The play gate routes every rejected drag
        /// through here so the reason reaches the player, not just the Editor console: a card that bounces
        /// back with no message at all reads as a broken game rather than as an illegal play.
        /// </summary>
        public void ShowNotice(string message)
        {
            EnsureNoticeLabel();
            if (noticeText == null)
            {
                return;
            }

            noticeText.text = message;
            noticeUntil = Time.unscaledTime + NoticeSeconds;
        }

        /// <summary>
        /// The notice label is created on demand rather than authored: the shipped <c>Battlefield</c> prefab
        /// predates it, and binding must never fail over one optional decoration. A Notice child written by
        /// an earlier run is re-bound instead of duplicated.
        /// </summary>
        void EnsureNoticeLabel()
        {
            if (noticeText != null || boardRoot == null)
            {
                return;
            }

            // Resolved through the index directly rather than FindLabel: the Notice is optional decoration,
            // so its absence must not flag the whole bind as failed.
            var noticeNode = FindNamed("Notice");
            noticeText = noticeNode != null ? noticeNode.GetComponent<TextMeshPro>() : null;
            if (noticeText == null)
            {
                // Sits in the clear band between the controls legend and the opponent's status strip.
                noticeText = MakeLabel(boardRoot, "Notice", new Vector3(0f, headerY - 1.24f, LabelZ),
                    new Vector2(17f, 0.44f), 15, FontStyles.Bold);
                noticeText.alignment = TextAlignmentOptions.Center;
            }

            noticeText.color = new Color(1f, 0.78f, 0.45f);
            if (Time.unscaledTime > noticeUntil)
            {
                noticeText.text = "";
            }
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
            // The card-to-tile map is rebuilt as tiles are painted, and a stale map survives a rebuild only
            // because the tiles themselves are the same objects. Dropping validity here means a repaint after
            // a manual hierarchy edit re-collects the pickables rather than clicking on ghosts.
            pickablesValid = false;
            Refresh(state);
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

        void Refresh(MatchState state)
        {
            if (noticeText != null && Time.unscaledTime > noticeUntil)
            {
                noticeText.text = "";
            }

            // Rebuilt from the tiles this frame paints, so a unit that moved slots cannot keep an old tile.
            tileByCard.Clear();
            if (state == null)
            {
                headerText.text = "Waiting for the match to initialize...";
                highlightedTile = null;
                return;
            }

            // A board with no coordinator (a test fixture, or an editor preview) still paints: the engine is
            // only ever consulted for the pending-block shading, which PaintTile null-guards.
            MatchEngine engine = coordinator != null ? coordinator.GetMatchEngine() : null;
            bool over = state.IsMatchOver();

            string active = state.ActivePlayer == PlayerIndex.Player ? "YOU" : "OPPONENT";
            headerText.text = $"TURN {state.TurnNumber}   \u2022   {state.CurrentPhase.ToString().ToUpper()}   \u2022   ACTIVE: {active}";

            PaintCore(state, engine, PlayerIndex.Opponent, opponentCoreActive, opponentCoreBench, opponentProtocol);
            PaintCore(state, engine, PlayerIndex.Player, playerCoreActive, playerCoreBench, playerProtocol);
            PaintNodes(state, engine);
            PaintPiles(state);

            // The opponent's Core counter is a click target for an unblocked direct attack, so it is the one
            // piece of board furniture that takes a pointer mark. It has no cube to tint, only its text.
            if (opponentStatus != null)
            {
                opponentStatus.color = coreMarked ? CoreTargetMark : StatusIdle;
            }

            RefreshLog(state);

            if (over)
            {
                string word = state.Result == MatchResult.PlayerWin ? "VICTORY"
                    : state.Result == MatchResult.OpponentWin ? "DEFEAT"
                    : state.Result == MatchResult.Draw ? "DRAW" : "";

                // The reason rides on the same line at a third of the size: at full 72pt a phrase like
                // "BOTH CORES DESTROYED" would run past the 14-unit banner box in both directions.
                string reason = string.IsNullOrEmpty(state.ResultReason)
                    ? ""
                    : $"<size=36%>   \u2022   {state.ResultReason}</size>";

                victoryText.text = word + reason;
                victoryText.color = state.Result == MatchResult.PlayerWin ? new Color(0.45f, 1f, 0.55f)
                    : state.Result == MatchResult.Draw ? new Color(0.95f, 0.9f, 0.6f)
                    : new Color(1f, 0.45f, 0.45f);
            }
            else
            {
                victoryText.text = "";
            }
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
            Tile[] active, Tile[] bench, Tile protocol)
        {
            PlayerState p = state.GetPlayer(who);
            for (int i = 0; i < active.Length; i++)
            {
                PaintTile(active[i], Occupied(p.Board.CoreActive[i]), engine);
            }
            for (int i = 0; i < bench.Length; i++)
            {
                PaintTile(bench[i], Occupied(p.Board.CoreBench[i]), engine);
            }
            PaintTile(protocol, p.Board.Protocol, engine);

            TextMeshPro strip = who == PlayerIndex.Player ? playerStatus : opponentStatus;
            string label = who == PlayerIndex.Player ? "YOU" : "OPPONENT";
            int nodes = p.Board.CountControlledNodes(who);
            strip.text =
                $"{label}   CORE {p.CoreDurability}/{PlayerState.StartingCoreDurability}   " +
                $"NP {p.CurrentNP}/{PlayerState.NpCap(nodes)}   NODES {nodes}/{Board.NodeCount}   " +
                $"HAND {p.Hand.Count}   DECK {p.Deck.Count}";
        }

        void PaintNodes(MatchState state, MatchEngine engine)
        {
            PlayerState player = state.GetPlayer(PlayerIndex.Player);
            PlayerState opp = state.GetPlayer(PlayerIndex.Opponent);

            for (int n = 0; n < Board.NodeCount; n++)
            {
                bool pControls = player.Board.IsNodeControlled(n, PlayerIndex.Player);
                bool oControls = opp.Board.IsNodeControlled(n, PlayerIndex.Opponent);

                SetBodyColor(nodePlate[n].Body, TintForHover(nodePlate[n], TintForMark(nodePlate[n],
                    pControls && oControls ? NodeFree
                    : pControls ? NodePlayer
                    : oControls ? NodeOpponent
                    : NodeFree)));

                for (int i = 0; i < Board.NodeActiveSlots; i++)
                {
                    PaintTile(nodePlayerActive[n][i], Occupied(player.Board.NodeActive[n][i]), engine);
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
        /// Shader property id for a body's colour, resolved from whatever shader the board was built with.
        /// URP Lit spells it <c>_BaseColor</c>; the built-in fallback spells it <c>_Color</c>.
        /// </summary>
        static int bodyColorId = -1;

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

            if (bodyColorId < 0)
            {
                Material shared = renderer.sharedMaterial;
                bool urp = shared != null && shared.shader != null &&
                    shared.shader.name.StartsWith("Universal", System.StringComparison.Ordinal);
                bodyColorId = Shader.PropertyToID(urp ? "_BaseColor" : "_Color");
            }

            tintBlock.Clear();
            tintBlock.SetColor(bodyColorId, color);
            renderer.SetPropertyBlock(tintBlock);
        }

        void PaintTile(Tile tile, CardInstance card, MatchEngine engine)
        {
            if (tile == null)
            {
                return;
            }

            // The pointer layer resolves a unit to the tile under it on every click, so the pairing is
            // refreshed with the rest of the view rather than cached across frames: a card promoted from
            // Bench to Active moves, and a stale tile would light the wrong socket.
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
                Color empty = highlightedTile == tile
                    ? Color.Lerp(tile.BaseColor, new Color(1f, 0.9f, 0.5f), 0.4f)
                    : tile.BaseColor;
                SetBodyColor(tile.Body, TintForHover(tile, TintForMark(tile, empty)));
                tile.Label.text = tile.EmptyLabel;
                tile.Label.color = new Color(0.62f, 0.66f, 0.74f);
                return;
            }

            Color baseTint = card.Owner == PlayerIndex.Player ? PlayerTint : OpponentTint;
            if (engine != null && engine.GetPendingBlocker(card.InstanceId) != 0)
            {
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
            tile.Label.color = Color.white;
            tile.Label.text = CardText(card);
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
                default: return baseTint;
            }
        }

        /// <summary>
        /// The hover sheen, applied after every other colour. A quarter of the way to white is enough to
        /// read as "the pointer is here" without competing with the marks, which say something the player
        /// has to act on.
        /// </summary>
        static Color TintForHover(Tile tile, Color tint)
        {
            return tile == null ? tint : Color.Lerp(tint, Color.white, 0.22f);
        }

        static string CardText(CardInstance card)
        {
            var sb = new StringBuilder();
            string name = card.Definition.Name;
            if (name.Length > 14)
            {
                name = name.Substring(0, 13) + "\u2026";
            }
            sb.Append(name).Append('\n');
            sb.Append(card.CurrentAtk).Append('/').Append(card.CurrentHp);
            if (card.CurrentShield > 0)
            {
                sb.Append(" +").Append(card.CurrentShield).Append('S');
            }
            if (card.State == UnitState.Summoned)
            {
                sb.Append(" (S)");
            }
            else if (card.State == UnitState.Exhausted)
            {
                sb.Append(" (E)");
            }
            return sb.ToString();
        }

        static CardInstance Occupied(BoardSlot slot)
        {
            return slot != null && slot.IsOccupied() ? slot.Card : null;
        }

        // ---- Primitive helpers ------------------------------------------------------

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
            renderer.sharedMaterial = MaterialFor(color);
            return renderer;
        }

        /// <summary>
        /// The material a tile body renders with.
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
        static Material MaterialFor(Color color)
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

        /// <summary>
        /// Writes the control legend onto a hint label. Called from both construction and binding: the
        /// legend is owned by this class rather than by the authored prefab, so a key binding added to
        /// <c>GameCoordinator</c> shows up without rebuilding <c>Battlefield.prefab</c>.
        /// </summary>
        static void ApplyControlsHint(TextMeshPro hint)
        {
            if (hint == null)
            {
                return;
            }

            hint.fontSize = ControlsHintFontSize;
            hint.text = ControlsHintText;
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
                    new Vector3(size.x + 0.06f, size.y + 0.06f, 0.04f), ActiveRim);
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
                BaseColor = emptyColor,
                EmptyLabel = emptyLabel,
            };
            // Same name-keyed identity as the prefab-bind path, so a board built from scratch and a board
            // bound from the prefab resolve drop targets identically.
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
            /// <summary>Board furniture: the tabletop, the header, the status strips.</summary>
            Other,

            /// <summary>An Active or Bench socket, either player's.</summary>
            Socket,

            /// <summary>The Protocol socket, one per player.</summary>
            Protocol,

            /// <summary>A Node's plate, behind its two ranks.</summary>
            Plate,

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
        }
    }
}
