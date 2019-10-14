using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Inasync.MessagingClient.Tests {

    [TestClass]
    public class Usage {

        [TestMethod]
        public async Task Usage_Readme() {
            var messageChannel = new FakeMessageChannel<string>();

            await messageChannel.PostAsync(message: "foo bar");

            try {
                var cts = new CancellationTokenSource(millisecondsDelay: 100);
                await messageChannel.SubscribeAsync((messages, results, cancellationToken) => {
                    Debug.Assert(messages != null);
                    Debug.Assert(results != null);
                    Debug.Assert(messages.Count == results.Length);

                    for (var i = 0; i < results.Length; i++) {
                        Console.WriteLine(messages[i]);  // "foo bar"
                        results[i] = true;
                    }
                    return Task.CompletedTask;
                }, cts.Token);
            }
            catch (OperationCanceledException) { }
        }

        #region Helpers

        private sealed class FakeMessageChannel<TMessage> : IMessageChannel<TMessage> {
            private readonly ConcurrentQueue<TMessage> _queue = new ConcurrentQueue<TMessage>();

            public Task PostAsync(TMessage message, CancellationToken cancellationToken) {
                cancellationToken.ThrowIfCancellationRequested();

                _queue.Enqueue(message);
                return Task.CompletedTask;
            }

            public Task SubscribeAsync(MessageChunkConsumerFunc<TMessage> consumer, CancellationToken cancellationToken) {
                if (consumer == null) { throw new ArgumentNullException(nameof(consumer)); }

                return Task.Run(async () => {
                    while (true) {
                        cancellationToken.ThrowIfCancellationRequested();

                        var messages = new TMessage[1];
                        var results = new bool[1];
                        while (_queue.TryDequeue(out var message)) {
                            messages[0] = message;
                            await consumer(messages, results, cancellationToken).ConfigureAwait(false);
                        }

                        await Task.Delay(100, cancellationToken);
                    }
                });
            }
        }

        #endregion Helpers
    }
}
