using NUnit.Framework;
using Overlink.Net;

namespace Overlink.Net.Tests
{
    /// <summary>
    /// The shape of the stored profile, and in particular what makes an account usable.
    /// <para>
    /// <see cref="PlayerProfile.IsComplete"/> is the gate in front of every protected scene —
    /// <c>AuthFlowController</c> routes on it — so a field added to that bar silently changes which
    /// accounts the game accepts. Adding the email did, deliberately: every profile written by an older
    /// build now reads as incomplete and its owner is bounced through the setup step once. These cases
    /// exist so that consequence is a stated decision with a test behind it rather than a surprise
    /// discovered by the first returning player.
    /// </para>
    /// </summary>
    public class PlayerProfileTests
    {
        static PlayerProfile Complete() => new PlayerProfile
        {
            username = "Fox",
            tag = "EU1",
            ageConfirmed = true,
            profileCompleted = true,
            email = "fox@example.com",
            profileVersion = PlayerProfile.CurrentVersion,
            createdAt = 1_700_000_000
        };

        [Test]
        public void AProfileWithEveryField_IsComplete()
        {
            Assert.IsTrue(Complete().IsComplete());
        }

        [Test]
        public void AnAddressIsPartOfWhatMakesAProfileUsable()
        {
            var profile = Complete();
            profile.email = "";
            Assert.IsFalse(profile.IsComplete(),
                "an account we cannot contact should not unlock play; the microtransaction plan is why");
        }

        [Test]
        public void AProfileFromAnOlderBuild_IsBouncedForTheAddressAlone()
        {
            // Exactly the shape stored by schema version 1: identity and age present, address absent.
            // This is the regression that would bite a returning player, pinned as a case so the bounce
            // stays a deliberate, bounded interruption rather than an accident of someone's refactor.
            var legacy = new PlayerProfile
            {
                username = "Fox",
                tag = "EU1",
                ageConfirmed = true,
                profileCompleted = true,
                profileVersion = 1,
                createdAt = 1_690_000_000
            };

            Assert.IsFalse(legacy.IsComplete());
            Assert.AreEqual("Fox", legacy.username, "the bounce must not cost them their name");
            Assert.IsTrue(legacy.ageConfirmed, "nor make them prove their age twice");
        }

        [Test]
        public void TheCurrentVersionIsBumpedForTheAddressFields()
        {
            Assert.GreaterOrEqual(PlayerProfile.CurrentVersion, 2,
                "the shape changed when email/emailVerified were added");
        }

        [Test]
        public void AMissingIdentityField_StillBlocksEvenWithAnAddressPresent()
        {
            // The address must not become a way around the rest of the bar: a profile that supplies an
            // email but no name is still not one the game can show in a lobby.
            var noUsername = Complete();
            noUsername.username = "";
            Assert.IsFalse(noUsername.IsComplete());

            var noTag = Complete();
            noTag.tag = "";
            Assert.IsFalse(noTag.IsComplete());

            var tooYoung = Complete();
            tooYoung.ageConfirmed = false;
            Assert.IsFalse(tooYoung.IsComplete(), "the age gate outranks everything on this screen");
        }

        [Test]
        public void CloneCarriesTheAddressAndItsProof()
        {
            // The setup draft is a clone, so a field dropped here would be erased on the next commit —
            // and emailVerified is the dangerous one, because losing it would quietly revoke a proof
            // that a future build worked to obtain.
            var source = Complete();
            source.emailVerified = true;

            var clone = source.Clone();
            Assert.AreEqual(source.email, clone.email);
            Assert.AreEqual(source.emailVerified, clone.emailVerified);
            Assert.AreEqual(source.profileVersion, clone.profileVersion);
            Assert.AreEqual(source.createdAt, clone.createdAt);
        }

        [Test]
        public void CloneIsIndependentOfTheOriginal()
        {
            var source = Complete();
            var clone = source.Clone();
            clone.email = "other@example.com";
            Assert.AreEqual("fox@example.com", source.email);
        }
    }
}
