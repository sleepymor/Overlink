using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace Overlink.Match.Tests
{
    /// <summary>
    /// The zoom the Gameplay scene actually renders, as serialized on its BattlefieldCameraController.
    /// <para>
    /// The component's code defaults (22 / 12 / 28) are not what ships: the scene overrides them. Framing tests
    /// read these values rather than the defaults, so they measure the frame the player sees. The test below
    /// reads the scene file and fails if the two drift apart, so a retune in the editor is caught here rather
    /// than leaving the framing pins quietly measuring the wrong camera.
    /// </para>
    /// </summary>
    public static class ShippedFraming
    {
        public const float Distance = 15f;
        public const float MinDistance = 8.8f;
        public const float MaxDistance = 26f;
    }

    public class ShippedFramingTests
    {
        [Test]
        public void GameplayScene_SerializesTheFramingTheTestsMeasure()
        {
            string scenePath = Path.Combine(Application.dataPath, "Scenes", "Gameplay.unity");
            string text = File.ReadAllText(scenePath);

            var match = Regex.Match(text,
                @"focus: \{[^}]*\}\s*pitch: [\d.]+\s*yaw: [\d.]+\s*distance: ([\d.]+)\s*fieldOfView: [\d.]+\s*" +
                @"minDistance: ([\d.]+)\s*maxDistance: ([\d.]+)");

            Assert.IsTrue(match.Success,
                "Gameplay.unity must serialize a BattlefieldCameraController with distance, minDistance and maxDistance.");

            Assert.AreEqual(ShippedFraming.Distance, float.Parse(match.Groups[1].Value,
                System.Globalization.CultureInfo.InvariantCulture), 1e-4f,
                "The scene's distance moved. Update ShippedFraming.Distance and re-measure the framing pins.");
            Assert.AreEqual(ShippedFraming.MinDistance, float.Parse(match.Groups[2].Value,
                System.Globalization.CultureInfo.InvariantCulture), 1e-4f,
                "The scene's minDistance moved. Update ShippedFraming.MinDistance.");
            Assert.AreEqual(ShippedFraming.MaxDistance, float.Parse(match.Groups[3].Value,
                System.Globalization.CultureInfo.InvariantCulture), 1e-4f,
                "The scene's maxDistance moved. Update ShippedFraming.MaxDistance.");
        }
    }
}
