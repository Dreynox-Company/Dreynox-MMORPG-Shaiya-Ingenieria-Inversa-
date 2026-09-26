using System.Collections.Generic;
using Dreynox.Mmorpg.World;
using NUnit.Framework;
using UnityEngine;

namespace Dreynox.Mmorpg.Tests.Editor
{
    public sealed class LegacyWorldEnvironmentRuntimeTests
    {
        [Test]
        public void MusicZoneSelectionPrefersNearestContainingZone()
        {
            AudioClip clipA =
                AudioClip.Create(
                    "a",
                    32,
                    1,
                    22050,
                    false);

            AudioClip clipB =
                AudioClip.Create(
                    "b",
                    32,
                    1,
                    22050,
                    false);

            try
            {
                var zones =
                    new List<
                        LegacyMusicZoneRuntimeDescriptor>
                    {
                        new LegacyMusicZoneRuntimeDescriptor
                        {
                            id = 0,
                            bounds =
                                new Bounds(
                                    Vector3.zero,
                                    new Vector3(
                                        100f,
                                        20f,
                                        100f)),
                            radius = 0f,
                            clip = clipA
                        },
                        new LegacyMusicZoneRuntimeDescriptor
                        {
                            id = 1,
                            bounds =
                                new Bounds(
                                    new Vector3(
                                        10f,
                                        0f,
                                        0f),
                                    new Vector3(
                                        20f,
                                        20f,
                                        20f)),
                            radius = 0f,
                            clip = clipB
                        }
                    };

                int selected =
                    LegacyWorldAudioRuntime
                        .SelectMusicZoneIndex(
                            new Vector3(
                                9f,
                                0f,
                                0f),
                            zones);

                Assert.AreEqual(
                    1,
                    selected);
            }
            finally
            {
                Object.DestroyImmediate(
                    clipA);

                Object.DestroyImmediate(
                    clipB);
            }
        }

        [Test]
        public void MusicZoneRadiusExtendsBoundsWithoutInventingInfiniteRange()
        {
            AudioClip clip =
                AudioClip.Create(
                    "radius",
                    32,
                    1,
                    22050,
                    false);

            try
            {
                var zones =
                    new List<
                        LegacyMusicZoneRuntimeDescriptor>
                    {
                        new LegacyMusicZoneRuntimeDescriptor
                        {
                            id = 0,
                            bounds =
                                new Bounds(
                                    Vector3.zero,
                                    new Vector3(
                                        10f,
                                        10f,
                                        10f)),
                            radius = 5f,
                            clip = clip
                        }
                    };

                Assert.AreEqual(
                    0,
                    LegacyWorldAudioRuntime
                        .SelectMusicZoneIndex(
                            new Vector3(
                                9f,
                                0f,
                                0f),
                            zones));

                Assert.AreEqual(
                    -1,
                    LegacyWorldAudioRuntime
                        .SelectMusicZoneIndex(
                            new Vector3(
                                11f,
                                0f,
                                0f),
                            zones));
            }
            finally
            {
                Object.DestroyImmediate(
                    clip);
            }
        }

        [Test]
        public void EmptyMusicZoneSetSelectsNothing()
        {
            Assert.AreEqual(
                -1,
                LegacyWorldAudioRuntime
                    .SelectMusicZoneIndex(
                        Vector3.zero,
                        new List<
                            LegacyMusicZoneRuntimeDescriptor>()));
        }
    }
}
