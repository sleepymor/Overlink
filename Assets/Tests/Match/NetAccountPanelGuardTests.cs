using NUnit.Framework;
using UnityEngine;

namespace Overlink.Match.Tests
{
    /// <summary>
    /// The Net layer self-installs a sign-in panel into whichever scene it happens to be loaded in, and the
    /// Gameplay scene does not want it: this is a local match against the AI, and the panel draws across the
    /// board header. <see cref="NetAccountPanelGuard"/> is the Gameplay side of that arrangement, and these
    /// tests lock the two properties that make it polite rather than destructive - it hides somebody else's
    /// object, and it gives it back.
    /// <para>
    /// The guard's <c>Update</c> is not exercised here, because the panel appears on a schedule owned by
    /// another assembly's <c>Start()</c>; <c>TryHideAccountPanel</c> and <c>RestoreAccountPanel</c> are public
    /// precisely so the lookup can be driven without entering play mode. <c>OnDisable</c> is not among them:
    /// Unity does not send it when an editor test flips <c>enabled</c>, so the restore-on-teardown glue is
    /// verified against a running scene rather than asserted here.
    /// </para>
    /// </summary>
    public class NetAccountPanelGuardTests
    {
        GameObject panel;
        GameObject host;
        NetAccountPanelGuard guard;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("NetAccountPanelGuardTests_Host");
            guard = host.AddComponent<NetAccountPanelGuard>();
        }

        [TearDown]
        public void TearDown()
        {
            // Restore before destroying, so a failed assertion cannot leave the scene dirty for the next test.
            if (guard != null)
            {
                guard.RestoreAccountPanel();
            }

            if (panel != null) Object.DestroyImmediate(panel);
            if (host != null) Object.DestroyImmediate(host);
        }

        /// <summary>
        /// Mirrors how <c>AccountMenuUI.BuildPanel</c> really creates it: a plain child of whatever canvas it
        /// found, at the top-left of the screen.
        /// </summary>
        void CreatePanel()
        {
            panel = new GameObject(NetAccountPanelGuard.AccountPanelName, typeof(RectTransform));
        }

        // ---- The lookup ---------------------------------------------------------------

        [Test]
        public void TryHide_WithNoPanelInstalledFindsNothing()
        {
            // A scene whose Net layer never got installed must not wedge the guard into a permanent search.
            Assert.IsFalse(guard.TryHideAccountPanel(), "Nothing to hide.");
            Assert.IsFalse(guard.IsHiding);
        }

        [Test]
        public void TryHide_HidesThePanelItFound()
        {
            CreatePanel();

            Assert.IsTrue(guard.TryHideAccountPanel(), "The panel exists, so it must be found.");
            Assert.IsTrue(guard.IsHiding);
            Assert.IsFalse(panel.activeSelf, "The panel is switched off rather than destroyed.");
        }

        [Test]
        public void TryHide_IsIdempotent()
        {
            CreatePanel();

            Assert.IsTrue(guard.TryHideAccountPanel());
            Assert.IsTrue(guard.TryHideAccountPanel(), "A second call must report the same state, not fail.");
            Assert.IsFalse(panel.activeSelf);
        }

        [Test]
        public void TryHide_CannotSeeAPanelSomebodyElseAlreadyHid()
        {
            // Documented limitation rather than a bug: GameObject.Find only returns active objects, so a panel
            // that arrives switched off is not the guard's business. It must report that honestly instead of
            // claiming custody of an object it never touched.
            panel = new GameObject(NetAccountPanelGuard.AccountPanelName, typeof(RectTransform));
            panel.SetActive(false);

            Assert.IsFalse(guard.TryHideAccountPanel());
            Assert.IsFalse(guard.IsHiding);
        }

        // ---- Giving it back -------------------------------------------------------------

        [Test]
        public void Restore_PutsThePanelBackWhereItWas()
        {
            CreatePanel();
            guard.TryHideAccountPanel();

            guard.RestoreAccountPanel();

            Assert.IsTrue(panel.activeSelf, "The panel belongs to a DontDestroyOnLoad object; leaving it " +
                "hidden would strand the sign-in flow in whatever Net scene is opened next.");
            Assert.IsFalse(guard.IsHiding);
        }

        [Test]
        public void Restore_WithoutAHideIsANoOp()
        {
            Assert.DoesNotThrow(() => guard.RestoreAccountPanel());
            Assert.IsFalse(guard.IsHiding);
        }

        [Test]
        public void Restore_AfterThePanelHasBeenDestroyedDoesNotThrow()
        {
            // The panel normally outlives this scene, but a teardown that destroys the Net objects first must
            // not turn the guard's last act into a MissingReferenceException.
            CreatePanel();
            guard.TryHideAccountPanel();
            Object.DestroyImmediate(panel);
            panel = null;

            Assert.DoesNotThrow(() => guard.RestoreAccountPanel());
            Assert.IsFalse(guard.IsHiding, "A dead panel is released rather than held.");
        }

        [Test]
        public void Hide_ThenRestore_ThenHideAgainWorks()
        {
            // Re-entering the Gameplay scene builds a fresh guard over the same long-lived panel, so the
            // sequence has to be repeatable rather than one-shot.
            CreatePanel();

            Assert.IsTrue(guard.TryHideAccountPanel());
            guard.RestoreAccountPanel();
            Assert.IsTrue(panel.activeSelf);
            Assert.IsTrue(guard.TryHideAccountPanel(), "And it can be found a second time.");
            Assert.IsFalse(panel.activeSelf);
        }
    }
}
