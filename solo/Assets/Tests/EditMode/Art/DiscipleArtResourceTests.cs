using NUnit.Framework;
using UnityEngine;
using Wizard.Solo.Art;

namespace Wizard.Solo.Tests.Art
{
    public class DiscipleArtResourceTests
    {
        [Test]
        public void DiscipleThreeUsesNewSelectionAndCombatArt()
        {
            using (var art = new ArtLibrary())
            {
                var selection = art.CharacterSelection(Ids.Char.C3);
                Assert.AreEqual("selection_192x192_v2", selection.texture.name);
                Assert.AreEqual(FilterMode.Point, selection.texture.filterMode);

                foreach (var facing in new[] { Facing3.Down, Facing3.Right, Facing3.Up })
                {
                    var fire = art.CharacterClip(Ids.Char.C3, "fire", facing);
                    var frames = art.CharacterClipInfo(Ids.Char.C3, "fire", facing).Frames;
                    Assert.That(fire.Frames.Length, Is.EqualTo(frames.Count));
                    for (int i = 0; i < frames.Count; i++)
                    {
                        Assert.AreEqual("body_v2", fire.Frames[i].texture.name);
                        Assert.AreEqual(FilterMode.Point, fire.Frames[i].texture.filterMode);
                        Assert.AreEqual(new Vector2(30, 4), fire.Frames[i].pivot);
                    }
                    Assert.That(frames[1].Hand.X, Is.GreaterThan(frames[0].Hand.X));
                }

                Assert.AreEqual("body", art.CharacterClip(Ids.Char.C1, "idle", Facing3.Down).Frames[0].texture.name);
                Assert.IsEmpty(art.Warnings);
            }
        }
    }
}
