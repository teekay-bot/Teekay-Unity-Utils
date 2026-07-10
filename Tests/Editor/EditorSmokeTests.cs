using NUnit.Framework;

namespace TeekayUtils.Tests
{
    /// Proves the EditMode test assembly compiles and is discovered by the
    /// Test Runner (requires "testables" in the host project's manifest).
    public class EditorSmokeTests
    {
        [Test]
        public void TestPipeline_EditMode_IsWired()
        {
            Assert.Pass("TeekayUtils.Tests.Editor is compiled and visible to the Test Runner.");
        }
    }
}
