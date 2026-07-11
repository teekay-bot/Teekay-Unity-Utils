using System.Collections;
using NUnit.Framework;
using TeekayUtils.DevConsole;
using UnityEngine;
using UnityEngine.TestTools;

namespace TeekayUtils.Tests
{
    /// PlayMode smoke tests for the DevConsole facade. Access is forced to Disabled so
    /// Initialize never builds the uGUI/TMP window — registration and Execute work headlessly
    /// by design, and the tests stay independent of TMP Essential Resources being imported.
    public class DevConsoleTests
    {
        ConsoleAccess previousAccess;

        [SetUp]
        public void SetUp()
        {
            previousAccess = DevConsoleSettings.Access;
            DevConsoleSettings.Access = ConsoleAccess.Disabled;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            var console = DevConsole.DevConsole.TryGetInstance();
            if (console != null) Object.Destroy(console.gameObject);
            yield return null;
            DevConsoleSettings.Access = previousAccess;
        }

        [UnityTest]
        public IEnumerator RegisterCommand_Execute_InvokesHandlerWithParsedArgs()
        {
            DevConsole.DevConsole.Initialize();
            int received = -1;
            DevConsole.DevConsole.RegisterCommand("test.cmd", "test", args => received = args.AsInt(0));

            DevConsole.DevConsole.Execute("test.cmd 42");
            yield return null;

            Assert.That(received, Is.EqualTo(42));
        }

        [UnityTest]
        public IEnumerator RegisterFloat_ExecuteWithValue_SetsCVar()
        {
            DevConsole.DevConsole.Initialize();
            float speed = 1f;
            DevConsole.DevConsole.RegisterFloat("test.speed", "test", () => speed, v => speed = v);

            DevConsole.DevConsole.Execute("test.speed 2.5");
            yield return null;

            Assert.That(speed, Is.EqualTo(2.5f));
        }

        [UnityTest]
        public IEnumerator Unregister_RemovesCommand()
        {
            DevConsole.DevConsole.Initialize();
            int calls = 0;
            DevConsole.DevConsole.RegisterCommand("test.gone", "test", _ => calls++);

            DevConsole.DevConsole.Execute("test.gone");
            DevConsole.DevConsole.Unregister("test.gone");
            DevConsole.DevConsole.Execute("test.gone");
            yield return null;

            Assert.That(calls, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator BuiltInCommands_AreRegistered()
        {
            DevConsole.DevConsole.Initialize();
            yield return null;

            Assert.That(DevConsole.DevConsole.Registry.TryGetCommand("help", out _), Is.True);
            Assert.That(DevConsole.DevConsole.Registry.TryGetCommand("clear", out _), Is.True);
            Assert.That(DevConsole.DevConsole.Registry.TryGetCommand("bind", out _), Is.True);
        }
    }
}
