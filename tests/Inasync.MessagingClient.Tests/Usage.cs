using System;
using System.Collections.Concurrent;
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
                await messageChannel.SubscribeAsync((message, cancellationToken) => {
                    Console.WriteLine(message);  // "foo bar"
                    return Task.FromResult(true);
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

            public Task SubscribeAsync(MessageConsumer<TMessage> consumer, CancellationToken cancellationToken) {
                if (consumer == null) { throw new ArgumentNullException(nameof(consumer)); }

                return Task.Run(async () => {
                    while (true) {
                        cancellationToken.ThrowIfCancellationRequested();

                        while (_queue.TryDequeue(out var message)) {
                            await consumer(message, cancellationToken).ConfigureAwait(false);
                        }
                        await Task.Delay(100, cancellationToken);
                    }
                });
            }
        }

        #endregion Helpers
    }
}
