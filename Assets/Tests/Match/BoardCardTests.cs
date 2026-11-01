using System.Collections.Generic;
using NUnit.Framework;
using Overlink.Cards;
using Overlink.Core;
using UnityEditor;
using UnityEngine;

namespace Overlink.Match.Tests
{
    /// <summary>
    /// A unit standing on the tabletop used to be a name and three numbers printed on a tinted cube, while the
    /// very same card in the fan was a framed card with art, cost, ability text and rarity materials. One object,
    /// two appearances, and a player re-reading every unit from scratch each time it left the hand.
    /// <para>
    /// <b>The board now draws the hand's own card, and these tests are why that is safe rather than merely
    /// possible.</b> The face is pooled per socket, so the three things that can change are tested apart: nothing
    /// changing (the frame in ninety-nine out of a hundred, and the one that must allocate and relayout nothing),
    /// the same card's numbers moving (a re-projection, and the reason a damaged unit shows the health the engine
    /// holds), and a <em>different</em> card arriving (a rebuild, because a pooled face that only re-reads numbers
    /// would go on showing the unit that died).
    /// </para>
    /// <para>
    /// <b>Two channels share one socket and neither is allowed to swallow the other.</b> The cube says what a click
    /// here would do - owner, mark, pending block, the gold rim behind it - and the card says what the thing is.
    /// The card is sized under the socket rather than into it, hangs off the tile's holder rather than its cube,
    /// carries no collider, and draws in the narrow lane between its own socket's geometry and the hand's lowest
    /// slice. Each of those is a
    /// separate way the pair could have been made to fight, and a card that wins any of them silently takes
    /// something the player needs.
    /// </para>
    /// <para>
    /// <b>The bare board is pinned from the inside.</b> Every other suite in this folder, and every editor preview
    /// in the project, builds a board with no catalogs wired and reads the caption text off the tiles. So
    /// <see cref="BareBoardWithNoCatalogs_StillPaintsTheCaptionTile"/> is not a formality: if the card path ever
    /// starts returning true without catalogs, those suites go on passing over a board that has stopped describing
    /// anything.
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed class BoardCardTests
    {
        // The three assets the hand carries in Gameplay.unity, loaded rather than synthesised: a face built from
        // a stand-in catalog proves the plumbing and not the card, and the whole claim here is that the socket
        // shows the same card the fan does.
        const string FrameCatalogPath = "Assets/Cards/Presentation/CardFrameCatalog.asset";
        const string TemplateCatalogPath = "Assets/Cards/Presentation/CardTemplateCatalog.asset";
        const string TierMaterialsPath = "Assets/Cards/Presentation/CardTierMaterialSet.asset";

        /// <summary>The hand's lowest sorting lane is its discard flight at 50, so nothing may draw under that.</summary>
        const int LowestHandLane = 50;

        // ---- The rig ------------------------------------------------------------------------------

        readonly List<GameObject> spawned = new List<GameObject>();
        readonly Dictionary<int, CardData> dataByInstance = new Dictionary<int, CardData>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < spawned.Count; i++)
            {
                if (spawned[i] != null)
                {
                    Object.DestroyImmediate(spawned[i]);
                }
            }

            spawned.Clear();

            // Cleared because it keys on instance id, and a record pointing at a destroyed CardInstance would
            // hand the next test a face bound to a dead body.
            dataByInstance.Clear();
        }

        /// <summary>
        /// A board that draws cards: the hand's own three catalogs, and a resolver standing in for the
        /// coordinator's cache. A GameCoordinator boots its match from a coroutine half a second into play mode,
        /// and tying this view's tests to that is the same mistake <c>Repaint</c> exists to avoid.
        /// </summary>
        BattlefieldPresenter CardBoard()
        {
            var go = new GameObject("BoardCardTestPresenter");
            spawned.Add(go);

            var presenter = go.AddComponent<BattlefieldPresenter>();
            presenter.frameCatalog = Load<CardFrameCatalog>(FrameCatalogPath);
            presenter.templateCatalog = Load<CardTemplateCatalog>(TemplateCatalogPath);
            presenter.tierMaterials = Load<CardTierMaterialSet>(TierMaterialsPath);
            presenter.cardDataResolver = DataFor;
            presenter.BuildBoard();
            return presenter;
        }

        static T Load<T>(string path) where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.IsNotNull(asset,
                $"{path} is the catalog the hand draws from and Gameplay.unity wires. Without it these tests " +
                "would prove the card path runs and say nothing about the card it draws.");
            return asset;
        }

        /// <summary>
        /// One <see cref="CardData"/> per live instance, which is what <c>GameCoordinator.DataFor</c> does and what
        /// makes a card on the table literally the record the fan was holding. Building a fresh one per call would
        /// rebuild every card on every repaint and quietly defeat the pooling these tests are about.
        /// </summary>
        CardData DataFor(CardInstance card)
        {
            if (card == null)
            {
                return null;
            }

            CardData data;
            if (dataByInstance.TryGetValue(card.InstanceId, out data))
            {
                return data;
            }

            CardDefinition def = card.Definition;
            data = new CardData(def.Id, def.Name, def.Cost, def.AbilityText, Color.white);
            data.AttachSource(def, card);
            dataByInstance[card.InstanceId] = data;
            return data;
        }

        /// <summary>A seeded match, opening on the player's turn, with nothing fielded but decks.</summary>
        static MatchState NewMatch()
        {
            var state = new MatchState();
            state.InitializeMatch(Deck(), Deck());
            return state;
        }

        /// <summary>A match sitting in lineup setup, so a nomination has something to be a nomination of.</summary>
        static MatchState SetupMatch()
        {
            var state = new MatchState(1234);
            state.InitializeSetup(Deck(), Deck());
            return state;
        }

        static List<CardDefinition> Deck()
        {
            var list = new List<CardDefinition>();
            for (int i = 0; i < 20; i++)
            {
                list.Add(UnitDef("deck_" + i, "Deck Unit", 1, 1, 2));
            }

            return list;
        }

        /// <param name="type">
        /// The socket family, or null for the Protocol socket - which is not a <see cref="SlotType"/> at all but a
        /// single property on the board, one per player, because nothing is ever deployed onto it by targeting a
        /// slot. It still draws a card, so it still has to fit one.
        /// </param>
        static CardInstance Place(MatchState state, PlayerIndex owner, SlotType? type, int index, int node,
            string name, UnitState unitState = UnitState.Ready)
        {
            PlayerState host = state.GetPlayer(owner);

            if (type == null)
            {
                // A Protocol card, because that is what the socket carries in a real match: no ATK, no HP, no
                // shield, and a frame of its own. Fitting a Unit there would test the arithmetic and not the card.
                var protocol = new CardInstance(new CardDefinition
                {
                    Id = "def_" + name,
                    Name = name,
                    Faction = Faction.Fsti,
                    Rarity = Rarity.Common,
                    Type = CardType.Protocol,
                    Cost = 2,
                    AbilityText = "Hosts one Relay."
                }, owner);
                host.Board.Protocol = protocol;
                return protocol;
            }

            var unit = new CardInstance(UnitDef("def_" + name, name, 3, 6, 2), owner)
            {
                State = unitState
            };
            BoardSlot slot = host.Board.GetSlot(type.Value, index, node);
            Assert.IsNotNull(slot, $"{type}[{index}] node {node} must exist on the board.");
            slot.Card = unit;
            return unit;
        }

        static CardDefinition UnitDef(string id, string name, int atk, int hp, int cost)
        {
            return new CardDefinition
            {
                Id = id,
                Name = name,
                Faction = Faction.Fsti,
                Rarity = Rarity.Common,
                Type = CardType.Unit,
                Cost = cost,
                Atk = atk,
                Hp = hp
            };
        }

        /// <summary>
        /// Every colour a board card draws with, across both channels a shade has to reach: its sprites and its
        /// labels.
        /// <para>
        /// <b>Measuring only the frame would be blind to the half that matters.</b> <c>Bind</c> reassigns the two
        /// sprite colours every time it runs and never touches the labels, which are coloured once at creation and
        /// afterwards belong to the shade alone. So a frame-only reading cannot see a shade compounding into the
        /// colours it is about to multiply - and that is precisely the failure the restore-before-cache at the end
        /// of <c>Bind</c> exists to prevent.
        /// </para>
        /// </summary>
        static Color[] FaceColors(CardView card)
        {
            Assert.IsNotNull(card, "expected a card on the socket");
            Assert.IsNotNull(card.Face, "expected a bound face");
            Assert.IsNotNull(card.Face.FrameImage, "expected the frame sprite the catalog instantiates");
            Assert.IsNotNull(card.Face.ArtImage, "expected the art window the catalog instantiates");
            Assert.IsNotNull(card.Face.NameValue, "expected the name label");
            Assert.IsNotNull(card.Face.AtkValue, "expected the attack label");
            return new[]
            {
                card.Face.FrameImage.color,
                card.Face.ArtImage.color,
                card.Face.NameValue.color,
                card.Face.AtkValue.color
            };
        }

        /// <summary>The shade reached the whole face, not just its artwork.</summary>
        static void AssertEveryChannelDimmer(Color[] darker, Color[] lighter, string what)
        {
            Assert.AreEqual(darker.Length, lighter.Length, "the same face channels on both cards");
            for (int i = 0; i < darker.Length; i++)
            {
                Assert.Less(darker[i].grayscale, lighter[i].grayscale,
                    $"{what}: channel {i} of the face did not dim, so the shade is not one multiplier over the " +
                    "whole card. A spent unit whose attack number stays bright is a spent unit that reads as ready " +
                    "to anyone looking at the numbers rather than the picture.");
            }
        }

        /// <summary>
        /// Channel-by-channel rather than by colour: NUnit's <c>Within</c> will not grip a <c>Color</c>, and the
        /// tolerance is the point - a restore that comes back within a rounding step is a clean restore, and one
        /// that comes back a multiplier deep is not.
        /// </summary>
        static void AssertEveryChannelEqual(Color[] actual, Color[] expected, string what)
        {
            Assert.AreEqual(actual.Length, expected.Length, "the same face channels on both readings");
            for (int i = 0; i < actual.Length; i++)
            {
                Assert.That(actual[i].r, Is.EqualTo(expected[i].r).Within(1e-4f), $"{what}: channel {i} red");
                Assert.That(actual[i].g, Is.EqualTo(expected[i].g).Within(1e-4f), $"{what}: channel {i} green");
                Assert.That(actual[i].b, Is.EqualTo(expected[i].b).Within(1e-4f), $"{what}: channel {i} blue");
                Assert.That(actual[i].a, Is.EqualTo(expected[i].a).Within(1e-4f), $"{what}: channel {i} alpha");
            }
        }

        static float Brightness(CardView card)
        {
            Color[] face = FaceColors(card);
            float dimmest = face[0].grayscale;
            for (int i = 1; i < face.Length; i++)
            {
                dimmest = Mathf.Min(dimmest, face[i].grayscale);
            }

            return dimmest;
        }

        static int CardCountUnder(Transform holder)
        {
            return holder.GetComponentsInChildren<CardView>(false).Length;
        }

        /// <summary>
        /// Every sorting order a face occupies: its sprite and text renderers, plus both edges of its art mask,
        /// which are what actually decide whether the art gets clipped. A <see cref="SpriteMask"/> derives from
        /// <c>Renderer</c> but its own <c>sortingOrder</c> is not the range it enforces, so it contributes its
        /// back and front instead.
        /// </summary>
        static int[] OrdersOf(CardView card)
        {
            var orders = new List<int>();
            foreach (Renderer renderer in card.GetComponentsInChildren<Renderer>(true))
            {
                var mask = renderer as SpriteMask;
                if (mask != null)
                {
                    orders.Add(mask.backSortingOrder);
                    orders.Add(mask.frontSortingOrder);
                }
                else
                {
                    orders.Add(renderer.sortingOrder);
                }
            }
            return orders.ToArray();
        }

        // ---- The shipped board ----------------------------------------------------------------------

        /// <summary>
        /// The fallback is not a formality. Every other suite in this folder and every editor preview builds a
        /// board with no catalogs and then reads <c>CardText</c> off the tile labels; if the card path ever began
        /// claiming sockets it cannot draw, those tests would keep passing over a board that had stopped
        /// describing anything.
        /// </summary>
        [Test]
        public void BareBoardWithNoCatalogs_StillPaintsTheCaptionTile()
        {
            var go = new GameObject("BareBoardPresenter");
            spawned.Add(go);
            var presenter = go.AddComponent<BattlefieldPresenter>();
            presenter.BuildBoard();

            MatchState state = NewMatch();
            CardInstance unit = Place(state, PlayerIndex.Player, SlotType.CoreActive, 0, -1, "Named Unit");
            presenter.Repaint(state);

            BattlefieldPresenter.Tile tile = presenter.TileOf(unit);
            Assert.IsNotNull(tile, "A unit on a socket must resolve to its tile.");
            Assert.IsNull(tile.Card, "No catalogs means no card, not a face assembled out of a placeholder.");
            Assert.IsTrue(tile.Label.text.Contains("Named Unit"),
                $"The caption still carries the name it always did, which is what the other suites read: " +
                $"got \"{tile.Label.text}\".");
        }

        // ---- The card itself ------------------------------------------------------------------------

        [Test]
        public void OccupiedSocket_GrowsTheRealCardUnderItsTile()
        {
            BattlefieldPresenter presenter = CardBoard();
            MatchState state = NewMatch();
            CardInstance unit = Place(state, PlayerIndex.Player, SlotType.CoreActive, 0, -1, "Anchor");

            presenter.Repaint(state);

            BattlefieldPresenter.Tile tile = presenter.TileOf(unit);
            Assert.IsNotNull(tile.Card, "A socket with a card on it draws that card, not a caption for it.");
            Assert.AreSame(tile.Body.transform.parent, tile.Card.transform.parent,
                "The card hangs off the tile's holder and not off its cube: the cube carries the socket's " +
                "non-uniform footprint, so a face parented to it arrives flattened to a twentieth of its height.");
            Assert.AreSame(DataFor(unit), tile.Card.Data,
                "One CardData per live instance. The board and the fan must be looking at the same record, or the " +
                "same card is free to read differently in the two places - which is the bug this whole pass " +
                "exists to remove.");
            Assert.IsNotNull(tile.Card.Face.Current, "The face must be bound to a projection.");
            Assert.AreEqual("Anchor", tile.Card.Face.Current.DisplayName);
            Assert.AreEqual(3, tile.Card.Face.Current.Atk.Value, "The face reads the live instance, not the definition.");
            Assert.AreEqual(6, tile.Card.Face.Current.Hp.Value);
        }

        /// <summary>
        /// A card standing on the tabletop is decoration on a socket the click layer already resolves geometrically,
        /// so a collider on it buys nothing and is one physics raycast away from stealing a click meant for an attack
        /// or a retreat. Only the interactive factory adds one, and <see cref="CardView"/> must not declare
        /// BoxCollider required to get there: Unity refuses to remove a required component, so the attribute would
        /// make every board card solid no matter what its factory did.
        /// </summary>
        [Test]
        public void BoardCard_CarriesNoColliderForTheSocketItDecorates()
        {
            BattlefieldPresenter presenter = CardBoard();
            MatchState state = NewMatch();
            CardInstance unit = Place(state, PlayerIndex.Player, SlotType.CoreActive, 0, -1, "Solid Post");

            presenter.Repaint(state);

            Collider[] found = presenter.TileOf(unit).Card.GetComponentsInChildren<Collider>();
            Assert.AreEqual(0, found.Length,
                "A board card is decoration on a socket the click layer already resolves geometrically; any " +
                $"collider on it is in the way. Found {found.Length}.");

            // The other half of the same decision, and the reason the attribute had to go rather than be worked
            // around: a hand card still has to be pickable, so the factory has to keep putting its collider on.
            // Built through the presentation catalogs for the same reason the board's cards are - the legacy cube
            // path destroys a primitive collider, which is not something an EditMode test can ask for.
            var hand = CardView.Create(DataFor(unit), presenter.transform, CardView.CardSize,
                presenter.frameCatalog, presenter.templateCatalog, presenter.placeholderFixture,
                presenter.tierMaterials);
            Assert.IsNotNull(hand.GetComponent<BoxCollider>(),
                "A card in the fan is picked up by a physics raycast, so it has to be solid. Losing this is losing " +
                "the hand, not the board.");
        }

        [Test]
        public void BoardCard_FitsInsideItsSocketOnTheTighterAxis()
        {
            // One case per socket family, because the families disagree about which axis binds. A Core Active is
            // 1.10 x 1.54 against a card's 1.0 x 1.4615, so height runs out first; a Node Bench at 0.54 x 0.756 is
            // the same shape a great deal smaller. Fitting on max() instead of min() would spill one of them past
            // the tile it stands on, and stretching to the socket's aspect would bend the frame.
            BattlefieldPresenter presenter = CardBoard();
            MatchState state = NewMatch();

            var cases = new[]
            {
                new SlotCase(SlotType.CoreActive, 0, -1, "Core Active"),
                new SlotCase(SlotType.CoreBench, 0, -1, "Core Bench"),
                new SlotCase(SlotType.CoreBench, 1, -1, "Core Bench 2"),
                new SlotCase(SlotType.NodeActive, 0, 0, "Node Active"),
                new SlotCase(SlotType.NodeBench, 0, 0, "Node Bench"),
                new SlotCase(null, 0, -1, "Protocol"),
            };

            for (int i = 0; i < cases.Length; i++)
            {
                SlotCase which = cases[i];
                CardInstance unit = Place(state, PlayerIndex.Player, which.Type, which.Index, which.Node,
                    "Case " + i);
                presenter.Repaint(state);

                BattlefieldPresenter.Tile tile = presenter.TileOf(unit);
                Assert.IsNotNull(tile.Card, $"{which.Label}: every socket draws the same model, small or large.");

                CardView card = tile.Card;
                Assert.LessOrEqual(card.Size.x, tile.Size.x + 1e-4f,
                    $"{which.Label}: the card overhangs its socket in width, so it covers the ring that says whose " +
                    "unit this is and what a click here would do.");
                Assert.LessOrEqual(card.Size.y, tile.Size.y + 1e-4f,
                    $"{which.Label}: same in height.");

                float fill = presenter.boardCardFill;
                Assert.LessOrEqual(card.Size.x / tile.Size.x, fill + 1e-3f,
                    $"{which.Label}: never covers more of the socket than boardCardFill allows.");
                Assert.LessOrEqual(card.Size.y / tile.Size.y, fill + 1e-3f,
                    $"{which.Label}: never covers more of the socket than boardCardFill allows.");
                Assert.That(Mathf.Max(card.Size.x / tile.Size.x, card.Size.y / tile.Size.y),
                    Is.EqualTo(fill).Within(1e-3f),
                    $"{which.Label}: the fit is tight on the binding axis, so the ring left showing is the knob's " +
                    "doing rather than an accident of rounding - and a socket that could take a bigger card is one " +
                    "the player is being denied.");

                Assert.That(card.Size.y / card.Size.x,
                    Is.EqualTo(CardView.CardSize.y / CardView.CardSize.x).Within(1e-3f),
                    $"{which.Label}: the frame's own aspect survives, so the card is scaled and not stretched.");
            }
        }

        /// <summary>
        /// Depth on this table is one axis: local -Z faces the pitched camera, so a smaller Z draws in front. The
        /// card has to sit in front of its cube or the opaque cube eats it, and behind its caption or it eats the one
        /// line on the socket that has to read over a card.
        /// </summary>
        [Test]
        public void BoardCard_LiesInFrontOfItsTileAndBehindItsCaption()
        {
            BattlefieldPresenter presenter = CardBoard();
            MatchState state = NewMatch();
            CardInstance unit = Place(state, PlayerIndex.Player, SlotType.CoreActive, 0, -1, "Posed Post");

            presenter.Repaint(state);

            BattlefieldPresenter.Tile tile = presenter.TileOf(unit);
            float cardZ = tile.Card.transform.localPosition.z;
            float surface = -tile.Size.z * 0.5f;
            Assert.Less(cardZ, surface,
                "The card is on the camera-facing side of the cube rather than buried inside it.");
            Assert.Greater(cardZ, tile.Label.transform.localPosition.z,
                "and behind the caption, which is the one thing on a socket that has to read over a card.");
        }

        /// <summary>
        /// The card inherits the pose its own socket's caption uses rather than a rotation this file remembers.
        /// The caption's orientation is the one on this table that is known to read, because it is the one being
        /// read every frame - so a board re-posed in the scene takes its cards with it instead of stranding them
        /// face-down, and <c>boardCardExtraRotation</c> stays the only escape hatch.
        /// </summary>
        [Test]
        public void BoardCard_InheritsItsSocketCaptionPose()
        {
            BattlefieldPresenter presenter = CardBoard();
            MatchState state = NewMatch();
            CardInstance unit = Place(state, PlayerIndex.Player, SlotType.CoreActive, 0, -1, "Posed Post");

            presenter.Repaint(state);

            BattlefieldPresenter.Tile tile = presenter.TileOf(unit);
            Assert.AreEqual(tile.Label.transform.localRotation, tile.Card.transform.localRotation,
                "A card facing any other way is a card nobody can read, and the caption is the proof of what " +
                "reads on this board.");

            // Re-posing the caption is the whole reason the pose is inherited rather than remembered: a board
            // authored at an angle, or tilted later in the scene, has to carry its cards with it. Identity on both
            // sides would prove nothing, so the caption is turned off its own axis first.
            tile.Label.transform.localRotation = Quaternion.Euler(24f, 0f, -11f);
            presenter.Repaint(state);
            Assert.AreEqual(tile.Label.transform.localRotation, tile.Card.transform.localRotation,
                "the card follows its caption rather than the rotation this file last saw");

            presenter.boardCardExtraRotation = new Vector3(0f, 180f, 0f);
            presenter.Repaint(state);
            Assert.AreEqual(tile.Label.transform.localRotation * Quaternion.Euler(0f, 180f, 0f),
                tile.Card.transform.localRotation,
                "The override is applied in the caption's own frame, on top of the inherited pose, so a board that " +
                "gets re-posed under the feature can still be corrected in the scene without touching code.");
        }

        /// <summary>
        /// The two halves of this table ship with one card orientation, and only the scene can break that.
        /// <para>
        /// Every caption on the board is authored at the identity, so inheriting the caption's pose gives all
        /// thirty-six sockets the same rotation - which is why an opponent's card reads upright from your seat
        /// exactly as your own does. <c>opponentCardExtraRotation</c> exists to turn that on purpose for the
        /// real-table convention, and this test holds it to three things: it moves their cards, it does not move
        /// yours, and it does not move a single caption, because SPENT and SUMMONING are the words that have to
        /// stay readable whichever way the card beneath them lies.
        /// </para>
        /// </summary>
        [Test]
        public void BoardCard_OpponentDialTurnsTheirCardsOnlyAndNoCaption()
        {
            BattlefieldPresenter presenter = CardBoard();
            MatchState state = NewMatch();
            CardInstance mine = Place(state, PlayerIndex.Player, SlotType.CoreActive, 0, -1, "Near Post");
            CardInstance theirs = Place(state, PlayerIndex.Opponent, SlotType.CoreActive, 0, -1, "Far Post");

            presenter.Repaint(state);

            BattlefieldPresenter.Tile mineTile = presenter.TileOf(mine);
            BattlefieldPresenter.Tile theirTile = presenter.TileOf(theirs);
            Assert.IsNotNull(mineTile.Card, "the near card must be on the board");
            Assert.IsNotNull(theirTile.Card, "and the far card too, or there is no second half to measure");

            Assert.AreEqual(mineTile.Card.transform.localRotation, theirTile.Card.transform.localRotation,
                "By default one card orientation serves the whole table. A half that reads differently from the " +
                "other is a change somebody has to ask for in the scene, not something the pose drifts into.");

            // The captions are the board's voice and they belong to the seat reading the screen, not to the card
            // they describe. A dial that turned them with the card would put SPENT upside down. Read before the
            // dial is set, because the claim is "unchanged", not "identity" - a board re-posed in the scene would
            // still be owed that guarantee.
            Quaternion captionBefore = theirTile.Label.transform.localRotation;

            presenter.opponentCardExtraRotation = new Vector3(0f, 0f, 180f);
            presenter.Repaint(state);

            Assert.AreEqual(Quaternion.Euler(0f, 0f, 180f), theirTile.Card.transform.localRotation,
                "Turning the opponent's half about the card's own normal is the whole dial: their card must come " +
                "round to face its owner.");
            Assert.AreEqual(mineTile.Label.transform.localRotation, mineTile.Card.transform.localRotation,
                "and the near half is untouched by a dial that names the opponent, or the knob would be a " +
                "whole-board rotation wearing an owner's label.");
            Assert.AreEqual(captionBefore, theirTile.Label.transform.localRotation,
                "A state word turns with nothing: the dial is the card's business alone.");
        }

        /// <summary>
        /// The opponent dial is a half-turn about the card's own centre, and that is the whole reason it is safe
        /// to ship: a rect spun about its centre maps onto itself, so their cards cannot leave their sockets.
        /// <para>
        /// <c>BoardCard_FitsInsideItsSocketOnTheTighterAxis</c> measures <see cref="CardView.Size"/>, which no
        /// rotation changes, so it would keep passing over a card hung from a socket's edge instead of centred on
        /// it - and a spun card hanging from an edge sweeps a full diameter of table, straight into the neighbour
        /// beside it. This measures the corners in the socket's own frame, which is the claim a screenshot of the
        /// far half would actually be making.
        /// </para>
        /// </summary>
        [Test]
        public void BoardCard_OpponentDialSpinsAboutTheCardCentreAndNotItsSocket()
        {
            BattlefieldPresenter presenter = CardBoard();
            MatchState state = NewMatch();
            CardInstance theirs = Place(state, PlayerIndex.Opponent, SlotType.CoreActive, 0, -1, "Far Post");

            presenter.Repaint(state);
            BattlefieldPresenter.Tile tile = presenter.TileOf(theirs);
            Assert.IsNotNull(tile.Card, "no card means no corners, and this test would pass over nothing");

            Rect before = CardFootprintInSocketSpace(tile.Card);
            Vector2 upBefore = UpInSocketSpace(tile.Card);

            presenter.opponentCardExtraRotation = new Vector3(0f, 0f, 180f);
            presenter.Repaint(state);

            Vector2 upAfter = UpInSocketSpace(tile.Card);
            Assert.AreEqual(-1f, upAfter.y, 1e-3f,
                "the dial has to actually turn the card, or every assertion below is measuring the pose it " +
                "already had");
            Assert.AreEqual(0f, upAfter.x, 1e-3f, "and in the table plane only: no tilt, no lift");

            Rect after = CardFootprintInSocketSpace(tile.Card);
            // Compared edge by edge with a tolerance rather than as Rect.Equals, which is exact over floats. A
            // half-turn is carried as a quaternion whose sine is not quite zero (about 8.7e-8 at 180 degrees), so
            // the corners come back the same to within noise and a bit-exact assert would fail on arithmetic no
            // player can see. The tolerance is a hundredth of a pixel at the table's scale, far below anything
            // that could let a card reach its neighbour.
            Assert.That(after.xMin, Is.EqualTo(before.xMin).Within(1e-4f), "the spun card's left edge moved");
            Assert.That(after.xMax, Is.EqualTo(before.xMax).Within(1e-4f), "and its right edge");
            Assert.That(after.yMin, Is.EqualTo(before.yMin).Within(1e-4f), "and its bottom edge");
            Assert.That(after.yMax, Is.EqualTo(before.yMax).Within(1e-4f),
                "and its top edge - together these say a half-turn about the card's centre leaves its corners " +
                "exactly where they were, so the far half cannot reach outside the socket it stands on.");

            // The socket itself, as the bound the footprint has to respect. Spelled out rather than trusted to
            // the comparison above, so a failure says which of the two has moved.
            Assert.LessOrEqual(Mathf.Max(Mathf.Abs(after.xMin), Mathf.Abs(after.xMax)), tile.Size.x * 0.5f + 1e-4f,
                "the spun card overhangs its socket in width, so it covers the ring that says whose unit this is.");
            Assert.LessOrEqual(Mathf.Max(Mathf.Abs(after.yMin), Mathf.Abs(after.yMax)), tile.Size.y * 0.5f + 1e-4f,
                "and in height.");
        }

        /// <summary>
        /// The card's four face corners measured in the space its socket is drawn in. The face is one co-planar
        /// sheet about the card's own origin, so the corners are simply half a size either way in local space;
        /// everything else is the transform's business, which is exactly what is being tested.
        /// </summary>
        static Rect CardFootprintInSocketSpace(CardView card)
        {
            Transform t = card.transform;
            float hx = card.Size.x * 0.5f;
            float hy = card.Size.y * 0.5f;
            Vector2[] locals =
            {
                new Vector2(-hx, hy), new Vector2(hx, hy), new Vector2(hx, -hy), new Vector2(-hx, -hy)
            };

            var box = new Rect();
            for (int i = 0; i < locals.Length; i++)
            {
                Vector3 world = t.TransformPoint(new Vector3(locals[i].x, locals[i].y, 0f));
                Vector3 inSocket = t.parent.InverseTransformPoint(world);
                if (i == 0)
                {
                    box = new Rect(inSocket.x, inSocket.y, 0f, 0f);
                }
                else
                {
                    box = Rect.MinMaxRect(Mathf.Min(box.xMin, inSocket.x), Mathf.Min(box.yMin, inSocket.y),
                        Mathf.Max(box.xMax, inSocket.x), Mathf.Max(box.yMax, inSocket.y));
                }
            }

            return box;
        }

        /// <summary>The card's own up direction, read in the socket's frame so the table's pose cancels out.</summary>
        static Vector2 UpInSocketSpace(CardView card)
        {
            Vector3 up = card.transform.parent.InverseTransformDirection(card.transform.up);
            return new Vector2(up.x, up.y);
        }

        // ---- The inspected card --------------------------------------------------------------

        /// <summary>
        /// A reader HUD the presenter can be wired to, under a name <c>BattlefieldHud.Find</c> cannot match, for
        /// the reason written in <c>BattlefieldHudTests</c>: an EditMode run happens with the gameplay scene open,
        /// and a fixture that bound to that HUD would be editing a real scene object.
        /// </summary>
        BattlefieldHud NewHud()
        {
            var go = new GameObject(BattlefieldHud.RootName + " (Test)", typeof(RectTransform));
            spawned.Add(go);

            BattlefieldHud hud = go.AddComponent<BattlefieldHud>();
            hud.Build();
            return hud;
        }

        /// <summary>
        /// The preview is the card the player already has on the table, drawn by the same <c>CardView</c> the
        /// sockets and the fan use - so it needs a <see cref="CardData"/> behind the instance, and a bare board
        /// is exactly what does not have one.
        /// <para>
        /// The fallback matters because the alternative is a card assembled from the placeholder fixture and
        /// handed to the player as if it were the one they clicked: a fake card in a real reader, which is the
        /// failure <c>SyncBoardCard</c> already refuses for the same reason.
        /// </para>
        /// </summary>
        [Test]
        public void ShowCard_WithNoFaceToDraw_KeepsTheTextSheetAndBuildsNoPlaceholder()
        {
            BattlefieldPresenter presenter = CardBoard();
            presenter.hud = NewHud();

            MatchState state = NewMatch();
            CardInstance card = Place(state, PlayerIndex.Player, SlotType.CoreActive, 0, -1, "Near Post");
            presenter.Repaint(state);

            // No resolver: the bare-board case, where nothing can project a face.
            presenter.cardDataResolver = null;
            presenter.ShowCard(card, state, "YOUR CORE ACTIVE");

            Assert.IsFalse(presenter.hud.ReaderShowsCard,
                "outside play mode, or with no data behind the instance, the reader stays a sheet of text");
            Assert.AreEqual("Near Post", presenter.hud.ReaderTitle);
            Assert.IsNotEmpty(presenter.hud.ReaderBody, "and it still says what the card does");
        }

        /// <summary>
        /// The one bridge in the preview: the reader hands over a box in screen pixels and the card is posed in
        /// world units, so the ratio between them decides whether the card fits the reader or overflows it.
        /// <para>
        /// Aspect is asserted first, because every label zone on a face is authored against the 832&times;1216
        /// frame canvas. A size that kept the height and lost the aspect would put the attack plate outside its
        /// own plate, and that is a bug no amount of "it is the right height" would catch.
        /// </para>
        /// </summary>
        [Test]
        public void SizeForReaderBox_KeepsTheFrameAspectAndGrowsWithTheBox()
        {
            Camera cam = NewCamera();

            Vector3 size = BattlefieldPresenter.SizeForReaderBox(cam, 5f, 520f, 1f);
            Assert.Greater(size.y, 0f, "a box of 520 pixels has to produce a card, not nothing");
            Assert.AreEqual(CardView.CardSize.y / CardView.CardSize.x, size.y / size.x, 1e-3f,
                "the card keeps the frame canvas aspect the face zones are authored against");

            Vector3 doubled = BattlefieldPresenter.SizeForReaderBox(cam, 5f, 1040f, 1f);
            Assert.AreEqual(size.y * 2f, doubled.y, 1e-2f,
                "twice the box is twice the card: the fill is a linear ratio, not a curve somebody guessed");

            Vector3 halved = BattlefieldPresenter.SizeForReaderBox(cam, 5f, 520f, 0.5f);
            Assert.AreEqual(size.y * 0.5f, halved.y, 1e-2f, "and the fill knob scales it the same way");
        }

        /// <summary>
        /// Perspective grows a card with its distance from the lens, so the depth the preview stands at cannot be
        /// tuned independently of the size it is built at - which is why the size is measured through the camera
        /// rather than authored. An orthographic lens has no such growth, and the same box must give the same card
        /// however far the rig pulls back.
        /// </summary>
        [Test]
        public void SizeForReaderBox_TracksTheLensThatWillDrawIt()
        {
            var perspective = NewCamera();
            perspective.orthographic = false;
            perspective.fieldOfView = 38f;

            Vector3 near = BattlefieldPresenter.SizeForReaderBox(perspective, 4f, 520f, 1f);
            Vector3 far = BattlefieldPresenter.SizeForReaderBox(perspective, 8f, 520f, 1f);
            Assert.Greater(far.y, near.y,
                "the same pixel box is a wider slice of world space further from the lens");

            var ortho = NewCamera();
            ortho.orthographic = true;
            ortho.orthographicSize = 5f;

            Vector3 orthoNear = BattlefieldPresenter.SizeForReaderBox(ortho, 4f, 520f, 1f);
            Vector3 orthoFar = BattlefieldPresenter.SizeForReaderBox(ortho, 8f, 520f, 1f);
            Assert.AreEqual(orthoNear.y, orthoFar.y, 1e-3f,
                "and an orthographic lens owes the box the same card at every depth");
        }

        /// <summary>
        /// Garbage in, no card out. A zero or negative box would otherwise reach <c>CardView.SetSize</c> and lay a
        /// face out at nothing, which reads as a preview that opened on an invisible card.
        /// </summary>
        [Test]
        public void SizeForReaderBox_WithNothingToFill_ReturnsNoCard()
        {
            Camera cam = NewCamera();

            Assert.AreEqual(Vector3.zero, BattlefieldPresenter.SizeForReaderBox(cam, 5f, 0f, 1f));
            Assert.AreEqual(Vector3.zero, BattlefieldPresenter.SizeForReaderBox(cam, 5f, -20f, 1f));
            Assert.AreEqual(Vector3.zero, BattlefieldPresenter.SizeForReaderBox(cam, 5f, 520f, 0f));
            Assert.AreEqual(Vector3.zero, BattlefieldPresenter.SizeForReaderBox(null, 5f, 520f, 1f));
        }

        /// <summary>
        /// The line under the card. It carries the two facts the frame cannot: where the card stands, and whether
        /// it can act. Both halves are optional, and the separator has to disappear with them - a footer ending in
        /// a dangling bullet reads as a truncated string.
        /// </summary>
        [Test]
        public void ComposeFooter_JoinsTheTwoHalvesWithoutADanglingSeparator()
        {
            Assert.AreEqual("YOUR NODE 2 ACTIVE   •   EXHAUSTED",
                BattlefieldPresenter.ComposeFooter("YOUR NODE 2 ACTIVE", "EXHAUSTED"));
            Assert.AreEqual("DISCARD  3 / 9", BattlefieldPresenter.ComposeFooter("DISCARD  3 / 9", null),
                "a spent card has no state on the table to report");
            Assert.AreEqual("SUMMONING DELAY", BattlefieldPresenter.ComposeFooter(null, "SUMMONING DELAY"));
            Assert.IsNull(BattlefieldPresenter.ComposeFooter(null, null));
        }

        /// <summary>
        /// Whether a card can act, without the slot word <see cref="BattlefieldPresenter"/> reads off the board.
        /// <para>
        /// <c>StatusLine</c> prefixes the socket type because it is the whole body of a text read; here the
        /// position line already names the socket, and "YOUR NODE 2 ACTIVE • ACTIVE" is the same word twice. A
        /// ready unit therefore says nothing at all.
        /// </para>
        /// </summary>
        [Test]
        public void ReadinessLine_SaysOnlyWhatTheFrameCannot()
        {
            // A card that has been brought up to Ready is the one state with nothing to report: it can act, and
            // a footnote saying so would be the socket's own colour doing the same job again.
            var ready = new CardInstance(UnitDef("def_ready", "Ready", 1, 1, 1), PlayerIndex.Player)
            {
                State = UnitState.Ready
            };
            Assert.IsNull(BattlefieldPresenter.ReadinessLine(ready), "a unit that can act needs no footnote");

            // A fresh instance arrives Summoned rather than Ready - CardInstance's constructor says so - so the
            // footnote has something to say on the turn it lands, which is exactly the turn a player is most
            // likely to click it wondering why it will not attack.
            var justArrived = new CardInstance(UnitDef("def_new", "New", 1, 1, 1), PlayerIndex.Player);
            Assert.AreEqual("SUMMONED", BattlefieldPresenter.ReadinessLine(justArrived));

            var exhausted = new CardInstance(UnitDef("def_spent", "Spent", 1, 1, 1), PlayerIndex.Player)
            {
                State = UnitState.Exhausted
            };
            Assert.AreEqual("EXHAUSTED", BattlefieldPresenter.ReadinessLine(exhausted));

            var delaying = new CardInstance(UnitDef("def_delay", "Delayed", 1, 1, 1), PlayerIndex.Player)
            {
                HasSummoningDelay = true,
                State = UnitState.Exhausted
            };
            Assert.AreEqual("SUMMONING DELAY", BattlefieldPresenter.ReadinessLine(delaying),
                "the delay outranks the state, because it is the longer wait and the one that cannot be acted on");

            // A Protocol is not deployed into a socket, so Upkeep never readies it and it stays Summoned for the
            // whole match. Printing the word would promise a change that never arrives.
            var protocol = new CardInstance(new CardDefinition
            {
                Id = "def_proto",
                Name = "Uplink",
                Faction = Faction.Fsti,
                Rarity = Rarity.Common,
                Type = CardType.Protocol,
                Cost = 2,
                AbilityText = "Hosts one Relay."
            }, PlayerIndex.Player);
            Assert.IsNull(BattlefieldPresenter.ReadinessLine(protocol),
                "a card the engine never readies has no readiness to report");
        }

        /// <summary>
        /// A lens to measure the box through. <c>Screen.height</c> is the editor's own game view in an EditMode
        /// run, and a headless batch has none, so the sizing tests are skipped rather than passed over a number
        /// that was never there.
        /// </summary>
        Camera NewCamera()
        {
            if (Screen.height <= 1)
            {
                Assert.Ignore("the box is measured in screen pixels, and this run has no viewport to mean them in");
            }

            var go = new GameObject("SizeForReaderBoxCamera", typeof(Camera));
            spawned.Add(go);

            var cam = go.GetComponent<Camera>();
            cam.transform.position = new Vector3(0f, 0f, -10f);
            return cam;
        }

        /// <summary>
        /// A card face writes no depth, so where it lands in the pile is sorting order's business alone - and
        /// this tabletop renders through URP's 2D Renderer, which orders <em>meshes</em> by that same key. So a
        /// board card's lane is a gap with two walls: above the socket it stands on, or the player never sees the
        /// card at all, and below the hand's lowest slice, or a card lifted out of the fan hides behind the unit
        /// it is being played onto. Only the second wall was ever measured here, and the board shipped invisible;
        /// both are read off live renderers rather than off a remembered constant.
        /// <para>
        /// One baseline now serves every socket, so every art mask on the board shares one range. That is safe
        /// only while a fitted card stays inside its own disjoint socket, which the sizing test pins - and while
        /// its art falls inside its own mask, which is asserted here, since art outside its range leaves the
        /// frame window unclipped.
        /// </para>
        /// </summary>
        [Test]
        public void BoardCards_DrawAboveTheBoardAndBelowTheHand()
        {
            BattlefieldPresenter presenter = CardBoard();
            MatchState state = NewMatch();
            CardInstance core = Place(state, PlayerIndex.Player, SlotType.CoreActive, 0, -1, "Core Post");
            CardInstance node = Place(state, PlayerIndex.Player, SlotType.NodeActive, 0, 1, "Node Post");
            presenter.Repaint(state);

            foreach (CardInstance unit in new[] { core, node })
            {
                BattlefieldPresenter.Tile tile = presenter.TileOf(unit);
                string who = tile.Name;
                int[] orders = OrdersOf(tile.Card);
                Assert.Greater(orders.Length, 0, who + ": the face owns nothing to measure.");

                foreach (int order in orders)
                {
                    Assert.Greater(order, tile.Body.sortingOrder, who +
                        ": part of the card draws under its own socket. The table, plates, rims and cubes all " +
                        "keep the default order, so under the 2D Renderer that is drawing under the tabletop - " +
                        "the socket shows a caption and no unit, which is exactly what shipped live.");
                    Assert.Less(order, LowestHandLane, who +
                        ": part of the card draws over the hand, so a card lifted out of the fan hides behind " +
                        "the unit it is being played onto.");
                }

                // The caption names the states a frame cannot speak for itself - SUMMONING, BLOCKING, SPENT,
                // NOMINATION - so burying it behind the card it annotates takes the message off the board again.
                int caption = tile.Label.GetComponent<Renderer>().sortingOrder;
                foreach (int order in orders)
                {
                    Assert.Greater(caption, order, who +
                        ": the caption draws behind its own card, so the state a frame cannot speak for itself " +
                        "stops reaching the player.");
                }
                Assert.Less(caption, LowestHandLane, who +
                    ": the caption has to stay under the hand too, or it travels with a lifted card.");

                SpriteMask mask = tile.Card.GetComponentInChildren<SpriteMask>(true);
                Assert.IsNotNull(mask, who + ": this face clips its art through a mask and has lost it.");
                Assert.AreEqual(SpriteMaskInteraction.VisibleInsideMask,
                    tile.Card.Face.ArtImage.maskInteraction, who +
                    ": the art is no longer asking to be clipped, so the frame window stops holding it.");
                int art = tile.Card.Face.ArtImage.sortingOrder;
                Assert.GreaterOrEqual(art, mask.backSortingOrder, who +
                    ": the art draws behind its own mask, so VisibleInsideMask never clips it.");
                Assert.LessOrEqual(art, mask.frontSortingOrder, who +
                    ": the art draws in front of its own mask, so VisibleInsideMask never clips it.");
            }
        }

        // ---- The three things that can change ----------------------------------------------------------

        /// <summary>
        /// The frame a death lands on. The engine removes a dead card from its slot and promotes a Bench card into
        /// it without telling anyone, so this repaint is the board's only chance to stop showing a unit that no
        /// longer exists - which is exactly the "I never saw it die" the caption was blamed for.
        /// </summary>
        [Test]
        public void SocketEmpties_CardGoesWithIt()
        {
            BattlefieldPresenter presenter = CardBoard();
            MatchState state = NewMatch();
            CardInstance unit = Place(state, PlayerIndex.Player, SlotType.CoreActive, 0, -1, "Doomed Post");
            presenter.Repaint(state);

            BattlefieldPresenter.Tile tile = presenter.TileOf(unit);
            Transform holder = tile.Body.transform.parent;
            Assert.AreEqual(1, CardCountUnder(holder), "the card is on the socket to begin with");

            state.GetPlayer(PlayerIndex.Player).Board.GetSlot(SlotType.CoreActive, 0, -1).Card = null;
            presenter.Repaint(state);

            Assert.IsNull(tile.Card,
                "An empty socket keeps no card: the face would hang there describing a card the engine has " +
                "already discarded, which is a ghost on the table.");
            Assert.AreEqual(0, CardCountUnder(holder),
                "and the socket's GameObject is left clean rather than carrying a destroyed pool around.");
            Assert.AreEqual(tile.EmptyLabel, tile.Label.text,
                "The caption goes back to the socket's own empty label, so an open socket still reads as open.");
        }

        /// <summary>
        /// The promotion case. A Bench card stepping into a vacant Active is a <em>different</em> card, and a
        /// pooled face that only re-reads its numbers would go on showing the dead one's name, art and cost - the
        /// worst possible failure here, because it would look like a card that changed its mind mid-fight.
        /// </summary>
        [Test]
        public void OccupantSwapped_CardIsRebuiltRatherThanReused()
        {
            BattlefieldPresenter presenter = CardBoard();
            MatchState state = NewMatch();
            CardInstance first = Place(state, PlayerIndex.Player, SlotType.CoreActive, 0, -1, "Alpha");
            presenter.Repaint(state);

            CardView old = presenter.TileOf(first).Card;
            Transform holder = old.transform.parent;

            CardInstance second = Place(state, PlayerIndex.Player, SlotType.CoreActive, 0, -1, "Beta");
            presenter.Repaint(state);

            BattlefieldPresenter.Tile tile = presenter.TileOf(second);
            Assert.IsNotNull(tile.Card, "the socket is occupied, so it draws a card");
            Assert.AreNotSame(old, tile.Card,
                "A different occupant is a different card, not a rebind of the old one.");
            Assert.AreEqual("Beta", tile.Card.Face.Current.DisplayName,
                "The face describes the card standing there now.");
            Assert.AreEqual(1, CardCountUnder(holder),
                "the replaced face is gone rather than merely hidden behind its successor");
        }

        /// <summary>
        /// The common case, and the one the player has been missing: a unit that has been hit shows the health the
        /// engine holds. The <see cref="CardData"/> a board card was built from carries the running
        /// <see cref="CardInstance"/>, so a re-projection reads the live numbers - and the pooled face is the same
        /// object afterwards, because rebuilding one every time a number moved is how a card flickers.
        /// </summary>
        [Test]
        public void StatMoves_FaceRepaintsWithoutRebuildingTheCard()
        {
            BattlefieldPresenter presenter = CardBoard();
            MatchState state = NewMatch();
            CardInstance unit = Place(state, PlayerIndex.Player, SlotType.CoreActive, 0, -1, "Battered Post");
            presenter.Repaint(state);

            CardView card = presenter.TileOf(unit).Card;
            Assert.AreEqual(6, card.Face.Current.Hp.Value, "deployed at full health");

            unit.CurrentHp = 2;
            unit.CurrentAtk = 5;
            presenter.Repaint(state);

            Assert.AreSame(card, presenter.TileOf(unit).Card,
                "Same occupant, so the pooled face is re-projected rather than rebuilt.");
            Assert.AreEqual(2, card.Face.Current.Hp.Value,
                "The board shows the health the engine holds, not the health the card was deployed with.");
            Assert.AreEqual(5, card.Face.Current.Atk.Value);
        }

        /// <summary>
        /// Nothing moved, so nothing is redrawn. The board repaints sixty times a second for a socket whose numbers
        /// change a few times a match, and a re-bind lays out every label on the face - so the guard has to be
        /// observable from outside, which is what this pins rather than a private counter.
        /// </summary>
        [Test]
        public void UnchangedOccupant_RefreshIsANoOp()
        {
            BattlefieldPresenter presenter = CardBoard();
            MatchState state = NewMatch();
            CardInstance unit = Place(state, PlayerIndex.Player, SlotType.CoreActive, 0, -1, "Quiet Post");
            presenter.Repaint(state);

            CardView card = presenter.TileOf(unit).Card;
            CardFaceData before = card.Face.Current;

            Assert.IsFalse(card.Refresh(),
                "Re-projecting a card whose instance has not moved must report that it did nothing, or the " +
                "repaint path pays for a full TMP layout every frame.");
            Assert.AreSame(before, card.Face.Current, "and leaves the projection it had, object and all.");
        }

        // ---- What a card cannot say about itself ----------------------------------------------------------

        /// <summary>
        /// The four held states, on the two channels that carry them. A card can show its name, cost, art, ability
        /// and numbers; it cannot show that it is spent, summoned, committed to block, or a nomination rather than a
        /// body - and those four are the difference between "I can swing" and "I cannot", which is precisely what
        /// went unseen before this.
        /// <para>
        /// The whole face dims rather than one label, deliberately: a spent unit has to read as spent from across
        /// the table, and the attack number is the one detail a player is least likely to be looking at when they
        /// decide whether they may swing.
        /// </para>
        /// </summary>
        [Test]
        public void HeldStates_DimTheWholeFaceAndSaySoInWords()
        {
            BattlefieldPresenter presenter = CardBoard();
            MatchState state = NewMatch();
            CardInstance ready = Place(state, PlayerIndex.Player, SlotType.CoreActive, 0, -1, "Ready Post");
            CardInstance spent = Place(state, PlayerIndex.Player, SlotType.CoreBench, 0, -1, "Spent Post",
                UnitState.Exhausted);
            CardInstance summoning = Place(state, PlayerIndex.Player, SlotType.CoreBench, 1, -1, "New Post",
                UnitState.Summoned);
            presenter.Repaint(state);

            Color[] readyFace = FaceColors(presenter.TileOf(ready).Card);
            AssertEveryChannelDimmer(FaceColors(presenter.TileOf(spent).Card), readyFace,
                "a spent unit draws dimmer than the same card with nothing holding it back");
            AssertEveryChannelDimmer(FaceColors(presenter.TileOf(summoning).Card), readyFace,
                "summoning delay is a held state too, and a fresh unit that cannot act yet has to look different " +
                "from one that can");

            Assert.AreEqual(string.Empty, presenter.TileOf(ready).Label.text,
                "Silence is the correct signal for a unit that may act. A word there is noise the player learns " +
                "to ignore, and it spends the one line reserved for the states that need it.");
            Assert.AreEqual("SPENT", presenter.TileOf(spent).Label.text);
            Assert.AreEqual("SUMMONING", presenter.TileOf(summoning).Label.text);
        }

        /// <summary>
        /// A state word is one word, but the socket captions are authored to wrap so a bare socket's full
        /// description can flow. Left on, TMP broke SUMMONING across two lines mid-word on the narrow sockets -
        /// which is what the live board showed, over the very art it was trying to annotate.
        /// <para>
        /// Measured off the generated mesh rather than off <c>enableWordWrapping</c>: the flag is the mechanism,
        /// one line is what the player sees. The narrowest socket that carries a card is used deliberately, since
        /// the wide ones never had the problem and a test on a Core Active would pass over the shipped defect.
        /// </para>
        /// </summary>
        [Test]
        public void StateCaptionStaysOnOneLine_OnTheNarrowestCaptionedSocket()
        {
            BattlefieldPresenter presenter = CardBoard();
            MatchState state = NewMatch();
            CardInstance summoning = Place(state, PlayerIndex.Player, SlotType.NodeBench, 0, 0, "Fresh Post",
                UnitState.Summoned);
            presenter.Repaint(state);

            var label = presenter.TileOf(summoning).Label;
            Assert.AreEqual("SUMMONING", label.text, "the longest state word is the one that needs the room");

            label.ForceMeshUpdate();
            Assert.Greater(label.textInfo.characterCount, 0,
                "TMP generated no glyphs, so this test measures nothing - the font asset is the thing to fix, not " +
                "the assertion.");
            Assert.AreEqual(1, label.textInfo.lineCount,
                "The state word wrapped onto a second line, and a word broken mid-syllable over a card's art " +
                "reads as damage rather than as information.");
        }

        /// <summary>
        /// The shade has to come off as cleanly as it goes on. A card is pooled across every state it will ever be
        /// in, so a multiplier folded into the value it is about to multiply would grey a unit to nothing one dim
        /// per combat step - the failure mode is invisible on the first pass and fatal by the fourth.
        /// </summary>
        [Test]
        public void ShadeNeverCompounds_AcrossHeldStatesAndRebinds()
        {
            BattlefieldPresenter presenter = CardBoard();
            MatchState state = NewMatch();
            CardInstance unit = Place(state, PlayerIndex.Player, SlotType.CoreActive, 0, -1, "Cycling Post");
            presenter.Repaint(state);

            CardView card = presenter.TileOf(unit).Card;
            Color[] pristine = FaceColors(card);

            unit.State = UnitState.Exhausted;
            presenter.Repaint(state);
            Color[] spent = FaceColors(card);
            AssertEveryChannelDimmer(spent, pristine, "the shade went on");

            unit.State = UnitState.Ready;
            presenter.Repaint(state);
            AssertEveryChannelEqual(FaceColors(card), pristine,
                "and came back off exactly. Anything short of equality here is a multiplier folded into the value " +
                "it is about to multiply, and a unit spent twice would be nearly black.");

            // Two re-binds in a row with the shade still on, which is the sequence that exposes a missing restore.
            // A bind rewrites the sprite colours and leaves the labels wearing whatever the shade put on them, so
            // snapshotting the base without un-multiplying first records a multiplied value as the pristine one:
            // the first re-bind looks fine and the second is a quarter of what the card was authored at. The
            // numbers move rather than the state because the state is already what a re-bind has to survive.
            unit.State = UnitState.Exhausted;
            unit.CurrentHp = 1;
            presenter.Repaint(state);
            AssertEveryChannelEqual(FaceColors(card), spent,
                "the first re-bind under a shade must not deepen it");

            unit.CurrentAtk = 4;
            presenter.Repaint(state);
            AssertEveryChannelEqual(FaceColors(card), spent,
                "nor must the second. A card that greys itself one step per combat event is a card that stops " +
                "being readable by the middle of any real fight.");
        }

        /// <summary>
        /// A nomination is a promise the engine has not accepted, and an offered card <em>is</em>
        /// <see cref="UnitState.Summoned"/> by construction - so a preview falling through to the state switch
        /// would print SUMMONING on a lineup the game explicitly waives the delay for. Cool rather than dim is the
        /// same axis the pointer marks use to tell a retreat from an attack, so the reading is at least consistent.
        /// </summary>
        [Test]
        public void Nomination_PaintsTheRealCardAndSaysItIsNotABodyYet()
        {
            BattlefieldPresenter presenter = CardBoard();
            MatchState state = SetupMatch();
            List<CardInstance> offer = state.GetPlayer(PlayerIndex.Player).LineupOffer;
            Assert.IsNotEmpty(offer, "setup must have dealt an offer to nominate from.");

            // A real body on the Bench, as the control: the Bench is never a preview target, so the two are
            // comparable without fighting over one socket.
            CardInstance body = Place(state, PlayerIndex.Player, SlotType.CoreBench, 0, -1, "Fielded Post");

            var ids = new int[ConfirmLineupCommand.RequiredLength];
            ids[0] = offer[0].InstanceId;
            presenter.SetLineupPreview(ids);
            presenter.Repaint(state);

            BattlefieldPresenter.Tile nominated = presenter.TileOf(offer[0]);
            Assert.IsNotNull(nominated.Card,
                "A nomination draws the real card too: the player is choosing between these, and a cube with a " +
                "name on it is not enough to choose on.");
            Assert.AreEqual("NOMINATION", nominated.Label.text,
                "and says what it is, rather than the summoning delay its construction happens to carry.");

            Color shade = presenter.TileOf(offer[0]).Card.Face.FrameImage.color;
            Assert.Greater(shade.b, shade.r,
                "A nomination reads cool, the same axis the marks use, so it cannot be mistaken for a body.");
            Assert.Less(Brightness(nominated.Card), Brightness(presenter.TileOf(body).Card),
                "and it is held back rather than presented as fielded.");
        }

        /// <summary>
        /// The defender's move, and the only board state a player has to read while it is not their turn. The
        /// pending block lives in the engine rather than the state, which is why <c>Repaint</c> takes one: without
        /// that seam this shading would ship untested, and a blocker that looks like any other unit is a blocker
        /// that silently does nothing.
        /// </summary>
        [Test]
        public void CommittedBlocker_GoesWarmAndSaysBlocking()
        {
            BattlefieldPresenter presenter = CardBoard();
            MatchState state = NewMatch();
            CardInstance attacker = Place(state, PlayerIndex.Player, SlotType.CoreActive, 0, -1, "Raider");
            CardInstance blocker = Place(state, PlayerIndex.Opponent, SlotType.CoreActive, 0, -1, "Wall");
            state.CurrentPhase = TurnPhase.Combat;

            presenter.Repaint(state);
            Color uncommitted = presenter.TileOf(blocker).Card.Face.FrameImage.color;
            Assert.AreEqual(string.Empty, presenter.TileOf(blocker).Label.text,
                "before anything is committed the socket says nothing, and a ready unit is not a blocker");

            var engine = new MatchEngine(state);
            Assert.AreEqual(CommandResult.Success, engine.ExecuteCommand(
                    new DeclareBlockCommand(PlayerIndex.Opponent, blocker.InstanceId, attacker.InstanceId)),
                "the block has to actually be pending for this to be testing anything");

            presenter.Repaint(state, engine);

            BattlefieldPresenter.Tile tile = presenter.TileOf(blocker);
            Assert.AreEqual("BLOCKING", tile.Label.text);
            Color committed = tile.Card.Face.FrameImage.color;
            Assert.Greater(committed.r, committed.b,
                "A committed blocker reads warm, on the opposite side of the same axis a nomination sits on.");
            Assert.AreNotEqual(uncommitted, committed,
                "and the commitment is visible on the card, not only on the tile ring behind it - the ring is " +
                "roughly a twentieth of a socket wide on a Node.");

            // The inversion this test exists to catch. The pending block is keyed by attacker, so the near-identical
            // engine call answers the opposite question, and every one of the three sites that ask it - this tile's
            // tint, the card's shade, the caption - would light the swing instead of the wall standing in front of
            // it. A defender who committed a blocker and saw the attacker glow would have learned that the game
            // marks threats rather than promises.
            BattlefieldPresenter.Tile swung = presenter.TileOf(attacker);
            Assert.AreEqual(string.Empty, swung.Label.text,
                "the attacker is not the one that made a commitment, so it gets no BLOCKING caption");
            Color swungShade = swung.Card.Face.FrameImage.color;
            Assert.That(swungShade.r, Is.EqualTo(swungShade.b).Within(1e-3f),
                "and its face stays unshaded rather than wearing the blocker's warmth: " +
                $"got {swungShade}.");
        }

        /// <summary>One socket family, addressed the way the board addresses it (null type = the Protocol socket).</summary>
        struct SlotCase
        {
            public readonly SlotType? Type;
            public readonly int Index;
            public readonly int Node;
            public readonly string Label;

            public SlotCase(SlotType? type, int index, int node, string label)
            {
                Type = type;
                Index = index;
                Node = node;
                Label = label;
            }
        }
    }
}
