using ReceptyOks.BlazorComponents.Services;

namespace ReceptyOks_UnitTests.Services
{
    [TestFixture]
    public class InstructionsEditorStateTests
    {
        [Test]
        public async Task ContentSetBeforeSignalReady_IsFlushedOnSignalReady()
        {
            var state = new InstructionsEditorState();

            string? received = null;
            var tcs = new TaskCompletionSource<string?>();
            int invokeCount = 0;

            state.ContentChanged += (_, s) =>
            {
                invokeCount++;
                received = s;
                tcs.TrySetResult(s);
            };

            // Set content before signalling readiness - should be queued
            state.Content = "<p>pending</p>";

            // Underlying Content property should still be empty because editor isn't ready
            Assert.That(state.Content, Is.EqualTo(string.Empty));

            // Now signal ready - queued content should be flushed and event raised
            state.SignalReady();

            var completed = await Task.WhenAny(tcs.Task, Task.Delay(1000));
            Assert.That(completed == tcs.Task, Is.True, "ContentChanged was not raised within timeout");

            Assert.That(invokeCount, Is.EqualTo(1), "ContentChanged should be invoked exactly once for the pending content");
            Assert.That(received, Is.EqualTo("<p>pending</p>"));
            Assert.That(state.Content, Is.EqualTo("<p>pending</p>"));
        }

        [Test]
        public void Reset_ClearsStateAndPreventsImmediateApply()
        {
            var state = new InstructionsEditorState();

            // Make editor ready and set content
            state.SignalReady();
            state.Content = "initial";
            Assert.That(state.Content, Is.EqualTo("initial"));

            // Reset should clear internal state
            state.Reset();
            Assert.That(state.Content, Is.EqualTo(string.Empty));

            // After reset, setting Content should not apply until SignalReady
            state.Content = "queued";
            Assert.That(state.Content, Is.EqualTo(string.Empty));
        }
    }
}
