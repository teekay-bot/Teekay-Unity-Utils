using System.Collections.Generic;
using NUnit.Framework;
using TeekayUtils.DevConsole;

namespace TeekayUtils.Tests
{
    /// EditMode tests for the pure-C# console core (no DevConsole singleton involved).
    public class DevConsoleCoreTests
    {
        // --- Parser ---

        [Test]
        public void Tokenize_SplitsOnWhitespaceAndRespectsQuotes()
        {
            Assert.That(ConsoleParser.Tokenize("say hello world"), Is.EqualTo(new[] { "say", "hello", "world" }));
            Assert.That(ConsoleParser.Tokenize("say \"hello world\""), Is.EqualTo(new[] { "say", "hello world" }));
            Assert.That(ConsoleParser.Tokenize("  spaced   out  "), Is.EqualTo(new[] { "spaced", "out" }));
        }

        [Test]
        public void Tokenize_UnterminatedQuote_RunsToEndOfLine()
        {
            Assert.That(ConsoleParser.Tokenize("say \"unterminated rest"), Is.EqualTo(new[] { "say", "unterminated rest" }));
        }

        [Test]
        public void Tokenize_EmptyInput_ReturnsEmptyArray()
        {
            Assert.That(ConsoleParser.Tokenize(null), Is.Empty);
            Assert.That(ConsoleParser.Tokenize("   "), Is.Empty);
        }

        // --- Args ---

        [Test]
        public void ConsoleArgs_TypedAccessors_ParseAndFallBack()
        {
            var args = new ConsoleArgs(new[] { "1.5", "42", "on", "junk" });

            Assert.That(args.AsFloat(0), Is.EqualTo(1.5f));
            Assert.That(args.AsInt(1), Is.EqualTo(42));
            Assert.That(args.AsBool(2), Is.True);

            Assert.That(args.AsInt(3, fallback: 7), Is.EqualTo(7));
            Assert.That(args.LastError, Does.Contain("not an int"));

            Assert.That(args.AsFloat(9, fallback: 2f), Is.EqualTo(2f));
            Assert.That(args.LastError, Does.Contain("missing"));
        }

        // --- Registry ---

        [Test]
        public void Registry_LookupIsCaseInsensitive_AndReplaceOnSameName()
        {
            var registry = new ConsoleRegistry();
            registry.Register(new ConsoleCommand("Player.Speed", "v1", _ => { }));
            registry.Register(new ConsoleCommand("player.speed", "v2", _ => { }));

            Assert.That(registry.AllCommands(), Has.Count.EqualTo(1));
            Assert.That(registry.TryGetCommand("PLAYER.SPEED", out var cmd), Is.True);
            Assert.That(cmd.Description, Is.EqualTo("v2"));
        }

        [Test]
        public void Registry_Unregister_RemovesAndFiresChanged()
        {
            var registry = new ConsoleRegistry();
            int changedCount = 0;
            registry.OnRegistryChanged += () => changedCount++;

            registry.Register(new ConsoleCommand("foo", "", _ => { }));
            registry.Unregister("FOO");

            Assert.That(registry.TryGetCommand("foo", out _), Is.False);
            Assert.That(changedCount, Is.EqualTo(2));
        }

        // --- CVars ---

        [Test]
        public void FloatCVar_ParsesInvariantCulture_AndRejectsJunk()
        {
            float value = 1f;
            var cvar = new FloatCVar("test.f", "", () => value, v => value = v);

            Assert.That(cvar.TrySetFromString("2.5", out _), Is.True);
            Assert.That(value, Is.EqualTo(2.5f));
            Assert.That(cvar.GetValueAsString(), Is.EqualTo("2.5"));

            Assert.That(cvar.TrySetFromString("abc", out string error), Is.False);
            Assert.That(error, Does.Contain("not a float"));
            Assert.That(value, Is.EqualTo(2.5f), "failed parse must not change the value");
        }

        [Test]
        public void BoolCVar_AcceptsCommonSpellings()
        {
            bool value = false;
            var cvar = new BoolCVar("test.b", "", () => value, v => value = v);

            foreach (string s in new[] { "1", "true", "on", "yes" })
            {
                value = false;
                Assert.That(cvar.TrySetFromString(s, out _), Is.True, s);
                Assert.That(value, Is.True, s);
            }

            Assert.That(cvar.TrySetFromString("off", out _), Is.True);
            Assert.That(value, Is.False);
        }

        [Test]
        public void CVar_SnapshotRestore_RoundTrips()
        {
            int value = 10;
            var cvar = new IntCVar("test.i", "", () => value, v => value = v);

            cvar.Snapshot();
            cvar.TrySetFromString("99", out _);
            Assert.That(value, Is.EqualTo(99));

            cvar.RestoreSnapshot();
            Assert.That(value, Is.EqualTo(10));
        }

        [Test]
        public void CVar_SetterException_IsReportedNotThrown()
        {
            var cvar = new StringCVar("test.s", "", () => "x",
                _ => throw new System.InvalidOperationException("boom"));

            Assert.That(cvar.TrySetFromString("anything", out string error), Is.False);
            Assert.That(error, Is.EqualTo("boom"));
        }

        // --- History ---

        [Test]
        public void History_NavigatesLikeBash_AndSkipsConsecutiveDuplicates()
        {
            var history = new ConsoleHistory(10);
            history.Add("first");
            history.Add("second");
            history.Add("second"); // duplicate of last — skipped

            Assert.That(history.Count, Is.EqualTo(2));
            Assert.That(history.NavigatePrevious(), Is.EqualTo("second"));
            Assert.That(history.NavigatePrevious(), Is.EqualTo("first"));
            Assert.That(history.NavigatePrevious(), Is.EqualTo("first"), "clamps at oldest");
            Assert.That(history.NavigateNext(), Is.EqualTo("second"));
            Assert.That(history.NavigateNext(), Is.EqualTo(string.Empty), "past newest returns live empty line");
        }

        [Test]
        public void History_EvictsOldestWhenFull()
        {
            var history = new ConsoleHistory(2);
            history.Add("a");
            history.Add("b");
            history.Add("c");

            Assert.That(history.Count, Is.EqualTo(2));
            Assert.That(history.NavigatePrevious(), Is.EqualTo("c"));
            Assert.That(history.NavigatePrevious(), Is.EqualTo("b"));
        }

        // --- Autocomplete ---

        static ConsoleRegistry MakeRegistry()
        {
            var registry = new ConsoleRegistry();
            registry.Register(new ConsoleCommand("help", "", _ => { }));
            registry.Register(new ConsoleCommand("heal", "", _ => { }));
            registry.Register(new ConsoleCommand("noclip", "", _ => { },
                new[] { new[] { "true", "false" } }));
            float f = 0;
            registry.Register(new FloatCVar("player.speed", "", () => f, v => f = v));
            return registry;
        }

        [Test]
        public void Autocomplete_GhostHint_CompletesAlphabeticallyFirstPrefixMatch()
        {
            Assert.That(ConsoleAutocomplete.TryGetCompletion("he", MakeRegistry(), out string suffix), Is.True);
            Assert.That(suffix, Is.EqualTo("al"), "'heal' < 'help' ordinally");
        }

        [Test]
        public void Autocomplete_Matches_PrefixTierRanksAboveSubstring()
        {
            var output = new List<ConsoleAutocomplete.MatchResult>();
            ConsoleAutocomplete.GetMatches("p", MakeRegistry(), 10, output);

            // "player.speed" is a prefix match; "noclip" and "help" contain 'p' as substring.
            Assert.That(output[0].Name, Is.EqualTo("player.speed"));
            Assert.That(output[0].IsCVar, Is.True);
            Assert.That(output.Count, Is.GreaterThan(1));
        }

        [Test]
        public void Autocomplete_ArgumentValues_CompleteAfterSpace()
        {
            Assert.That(ConsoleAutocomplete.TryGetCompletion("noclip t", MakeRegistry(), out string suffix), Is.True);
            Assert.That(suffix, Is.EqualTo("rue"));
        }
    }
}
