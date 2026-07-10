using NUnit.Framework;

namespace TeekayUtils.Tests
{
    /// Proves the PlayMode test assembly compiles and is discovered by the
    /// Test Runner (requires "testables" in the host project's manifest).
    public class RuntimeSmokeTests
    {
        [Test]
        public void TestPipeline_PlayMode_IsWired()
        {
            Assert.Pass("TeekayUtils.Tests is compiled and visible to the Test Runner.");
        }
    }
}
