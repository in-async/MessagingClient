using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon.SQS;
using Amazon.SQS.Model;

namespace Inasync.MessagingClient.Amazon {

    /// <summary>
    /// <see cref="IMessageChannel{TMessage}"/> の実装。
    /// Amazon SQS メッセージ チャネル。
    /// </summary>
    public sealed class AmazonSQSChannel : IMessageChannel<string> {
        private readonly IAmazonSQS _client;
        private readonly string _queueUrl;
        private readonly TimeSpan _pollingIdleTime;
        private readonly Action<string> _logger;

        /// <summary>
        /// <see cref="AmazonSQSChannel"/> クラスの新しいインスタンスを初期化します。
        /// </summary>
        /// <param name="client">Amazon SQS のクライアント。</param>
        /// <param name="queueUrl">Amazon SQS に存在する対象キューの URL。</param>
        /// <param name="pollingIdleTime"><see cref="SubscribeAsync(MessageConsumer{string}, CancellationToken)"/> を実行中、受信メッセージが無い場合に待機する時間。</param>
        /// <param name="logger">処理の途中経過を通知するロガー。主に <see cref="SubscribeAsync(MessageConsumer{string}, CancellationToken)"/> 用。</param>
        /// <exception cref="ArgumentNullException"><paramref name="client"/> or <paramref name="queueUrl"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentOutOfRangeException">The <paramref name="pollingIdleTime"/> millisecond is less than -1 or greater than <see cref="int.MaxValue"/>.</exception>
        public AmazonSQSChannel(IAmazonSQS client, string queueUrl, TimeSpan pollingIdleTime, Action<string> logger) {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _queueUrl = queueUrl ?? throw new ArgumentNullException(nameof(queueUrl));
            if (pollingIdleTime.TotalMilliseconds < -1 || int.MaxValue < pollingIdleTime.TotalMilliseconds) { throw new ArgumentOutOfRangeException(nameof(pollingIdleTime), pollingIdleTime, null); }
            _pollingIdleTime = pollingIdleTime;
            _logger = logger;
        }

        /// <summary>
        /// <see cref="IInputMessageChannel{TMessage}.PostAsync(TMessage, CancellationToken)"/> の実装。
        /// </summary>
        public async Task PostAsync(string message, CancellationToken cancellationToken) {
            Debug.Assert(message != null);

            var request = new SendMessageRequest(_queueUrl, message);
            SendMessageResponse response;
            try {
                response = await _client.SendMessageAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { throw new MessagingException("The message registration request to Amazon SQS failed.", ex); }
            if ((int)response.HttpStatusCode >= 400) { throw new MessagingException($"The response of Amazon SQS for sending a message is {response.HttpStatusCode}."); }
        }

        /// <summary>
        /// <see cref="IOutputMessageChannel{TMessage}.SubscribeAsync(MessageConsumer{TMessage}, CancellationToken)"/> の実装。
        /// <para>
        /// SQS キューからメッセージを受信し、消費したメッセージをキューから削除します。
        /// </para>
        /// </summary>
        public Task SubscribeAsync(MessageConsumer<string> consumer, CancellationToken cancellationToken) {
            if (consumer == null) { throw new ArgumentNullException(nameof(consumer)); }

            return Task.Run(async () => {
                while (true) {
                    cancellationToken.ThrowIfCancellationRequested();
                    const int limits = 10;

                    var messages = await BulkReceiveAsync(limits, cancellationToken).ConfigureAwait(false);
                    if (messages.Count == 0) {
                        _logger?.Invoke("no message");
                        await Task.Delay(_pollingIdleTime, cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    bool[] consumeResults;
                    try {
                        consumeResults = await Task.WhenAll(messages.Select(x => consumer(x.Body, cancellationToken))).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex) { throw new MessagingException("The consumer delegate failed.", ex); }
                    var consumedMessages = messages.Where((x, index) => consumeResults[index]);

                    var deletes = await BulkDeleteAsync(consumedMessages).ConfigureAwait(false);

                    _logger?.Invoke($"({limits}, {messages.Count}, {consumeResults.Count(x => x)}, {deletes})");
                }
            });
        }

        private async Task<IReadOnlyList<Message>> BulkReceiveAsync(int limits, CancellationToken cancellationToken) {
            Debug.Assert(1 <= limits && limits <= 10);

            var request = new ReceiveMessageRequest(_queueUrl) {
                MaxNumberOfMessages = limits,
            };

            ReceiveMessageResponse response;
            try {
                response = await _client.ReceiveMessageAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { throw new MessagingException("The message reception request to Amazon SQS failed.", ex); }
            if ((int)response.HttpStatusCode >= 400) { throw new MessagingException($"The response of Amazon SQS for receiving messages is {response.HttpStatusCode}."); }

            return response.Messages;
        }

        private async Task<int> BulkDeleteAsync(IEnumerable<Message> messages) {
            Debug.Assert(messages != null);

            var entries = messages.Select(x => new DeleteMessageBatchRequestEntry(id: x.MessageId, receiptHandle: x.ReceiptHandle)).ToList();
            if (entries.Count == 0) { return 0; }

            DeleteMessageBatchResponse response;
            try {
                response = await _client.DeleteMessageBatchAsync(new DeleteMessageBatchRequest(_queueUrl, entries)).ConfigureAwait(false);
            }
            catch (Exception ex) { throw new MessagingException("The message deletion request to Amazon SQS failed.", ex); }
            if ((int)response.HttpStatusCode >= 400) { throw new MessagingException($"The response of Amazon SQS for deleting messages is {response.HttpStatusCode}."); }

            return response.Successful.Count;
        }
    }
}
