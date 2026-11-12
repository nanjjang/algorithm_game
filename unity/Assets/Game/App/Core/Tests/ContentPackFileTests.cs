#nullable enable
using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using Wizard.Content;

namespace Wizard.Unity.App.Core.Tests
{
    public sealed class ContentPackFileTests
    {
        [Test]
        public void CandidatePaths_StartWithStreamingAssets_ThenTheEditorBuildOutput()
        {
            var paths = ContentPackFile.CandidatePaths();
            Assert.That(paths[0], Is.EqualTo(Path.Combine(Application.streamingAssetsPath, "content", "base.wzpack")));
            Assert.That(paths.Count, Is.EqualTo(2));
            Assert.That(paths[1].Replace('\\', '/'), Does.EndWith("/artifacts/content/base.wzpack"));
        }

        [Test]
        public void TryLoad_MissingFile_FailsWithThePath()
        {
            string path = Path.Combine(Path.GetTempPath(), "wizard-missing-" + Guid.NewGuid().ToString("N") + ".wzpack");
            Assert.That(ContentPackFile.TryLoad(path, out ContentDb? db, out string message), Is.False);
            Assert.That(db, Is.Null);
            Assert.That(message, Does.Contain(path));
        }

        [Test]
        public void TryLoad_CorruptPack_Fails()
        {
            string path = Path.Combine(Path.GetTempPath(), "wizard-corrupt-" + Guid.NewGuid().ToString("N") + ".wzpack");
            File.WriteAllBytes(path, new byte[] { (byte)'W', (byte)'Z', (byte)'P', (byte)'K', 0, 0, 0 });
            try
            {
                Assert.That(ContentPackFile.TryLoad(path, out ContentDb? db, out string message), Is.False);
                Assert.That(db, Is.Null);
                Assert.That(message, Does.Contain(path));
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
