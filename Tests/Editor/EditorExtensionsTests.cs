using NUnit.Framework;
using TeekayUtils.Editor;
using UnityEditor;
using UnityEngine;

namespace TeekayUtils.Tests
{
    public class EditorExtensionsTests
    {
        [Test]
        public void ConfirmOverwrite_PathDoesNotExist_ReturnsTrueWithoutDialog()
        {
            string path = System.IO.Path.Combine(Application.temporaryCachePath, "does-not-exist-42.txt");

            Assert.That(EditorFileUtils.ConfirmOverwrite(path), Is.True);
        }

        [Test]
        public void PingAndSelect_SetsActiveSelection()
        {
            Object previous = Selection.activeObject;
            var asset = ScriptableObject.CreateInstance<ScriptableObject>();
            try
            {
                asset.PingAndSelect();

                Assert.That(Selection.activeObject, Is.EqualTo(asset));
            }
            finally
            {
                Selection.activeObject = previous;
                Object.DestroyImmediate(asset);
            }
        }
    }
}
