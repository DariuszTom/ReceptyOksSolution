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

        [Test]
        public void Pause_PreservesContentForNextSignalReady()
        {
            var state = new InstructionsEditorState();

            state.SignalReady();
            state.Content = "<p>hello</p>";

            // Pause keeps content available for the next Blazor instance
            state.Pause();
            Assert.That(state.IsBlazorReady, Is.False);

            string? flushed = null;
            state.ContentChanged += (_, s) => flushed = s;

            // Next SignalReady should re-deliver the preserved content
            state.SignalReady();
            Assert.That(flushed, Is.EqualTo("<p>hello</p>"));
        }

        [Test]
        public void Pause_ThenNewContent_DeliversNewContentOnSignalReady()
        {
            var state = new InstructionsEditorState();

            state.SignalReady();
            state.Content = "old";
            state.Pause();

            // MAUI side sets new content while Blazor is re-creating
            state.Content = "new recipe";

            string? flushed = null;
            state.ContentChanged += (_, s) => flushed = s;

            state.SignalReady();
            Assert.That(flushed, Is.EqualTo("new recipe"));
            Assert.That(state.Content, Is.EqualTo("new recipe"));
        }

        [Test]
        public void TwoSubclasses_AreIndependent()
        {
            var editor = new InstructionsEditorState();
            var viewer = new HtmlViewerState();

            editor.SignalReady();
            viewer.SignalReady();

            editor.Content = "editor content";
            viewer.Content = "viewer content";

            Assert.That(editor.Content, Is.Not.EqualTo(viewer.Content));
        }

        [Test]
        public void Pause_ThenEmptyContent_ClearsOnSignalReady()
        {
            var state = new InstructionsEditorState();

            // Simulate editing Recipe A
            state.SignalReady();
            state.Content = "<p>Recipe A instructions</p>";
            Assert.That(state.Content, Is.EqualTo("<p>Recipe A instructions</p>"));

            // Navigate away - Blazor component disposes
            state.Pause();

            // Open new recipe (empty instructions)
            state.Content = string.Empty;

            string? flushed = null;
            state.ContentChanged += (_, s) => flushed = s;

            // Blazor component re-initializes
            state.SignalReady();

            // Must flush the empty string and clear old content
            Assert.That(flushed, Is.EqualTo(string.Empty));
            Assert.That(state.Content, Is.EqualTo(string.Empty));
        }
    }
}
