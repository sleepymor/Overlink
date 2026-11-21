using System;
using NUnit.Framework;

namespace Overlink.EditorTools.Tests
{
    public sealed class PlayerManagerCredentialsTests
    {
        [Test]
        public void CompleteProcessPair_TakesPriority()
        {
            bool found = TestingPlayerManagerClient.ResolveCredentials((name, target) =>
                (target == EnvironmentVariableTarget.Process ? "process-" : "user-") + name,
                true, out string id, out string secret, out string source);
            Assert.IsTrue(found);
            Assert.AreEqual("process-" + TestingPlayerManagerClient.KeyIdVariable, id);
            Assert.AreEqual("process-" + TestingPlayerManagerClient.SecretVariable, secret);
            Assert.AreEqual("Process environment", source);
        }

        [Test]
        public void MissingProcessPair_ReadsWindowsUserPairWithoutRestart()
        {
            bool found = TestingPlayerManagerClient.ResolveCredentials((name, target) =>
                target == EnvironmentVariableTarget.User ? "user-" + name : null,
                true, out string id, out string secret, out string source);
            Assert.IsTrue(found);
            Assert.AreEqual("user-" + TestingPlayerManagerClient.KeyIdVariable, id);
            Assert.AreEqual("user-" + TestingPlayerManagerClient.SecretVariable, secret);
            Assert.AreEqual("Windows user environment", source);
        }

        [Test]
        public void PartialPairs_AreNeverMixed()
        {
            bool found = TestingPlayerManagerClient.ResolveCredentials((name, target) =>
                (target == EnvironmentVariableTarget.Process && name == TestingPlayerManagerClient.KeyIdVariable)
                || (target == EnvironmentVariableTarget.User && name == TestingPlayerManagerClient.SecretVariable)
                    ? "partial" : null,
                true, out string id, out string secret, out _);
            Assert.IsFalse(found);
            Assert.IsNull(id);
            Assert.IsNull(secret);
        }

        [Test]
        public void NonWindows_DoesNotReadUserEnvironment()
        {
            bool found = TestingPlayerManagerClient.ResolveCredentials((name, target) =>
            {
                Assert.AreEqual(EnvironmentVariableTarget.Process, target);
                return null;
            }, false, out _, out _, out _);
            Assert.IsFalse(found);
        }
    }
}
