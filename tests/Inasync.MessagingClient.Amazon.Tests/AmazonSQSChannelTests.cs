using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Amazon.SQS;
using Amazon.SQS.Model;
using Inasync;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Inasync.MessagingClient.Amazon.Tests {

    [TestClass]
    public class AmazonSQSChannelTests {

        [TestMethod]
        public void Ctor() {
            Action TestCase(int testNumber, IAmazonSQS client, string queueUrl, TimeSpan pollingIdleTime, Action<string> logger, Type expectedExceptionType) => () => {
                new TestCaseRunner($"No.{testNumber}")
                    .Run(() => new AmazonSQSChannel(client, queueUrl, pollingIdleTime, logger))
                    .Verify((actual, desc) => { }, expectedExceptionType);
            };

            new[] {
                TestCase( 0, null              , ""  , TimeSpan.Zero, _ => { }, typeof(ArgumentNullException)),
                TestCase( 1, new SpyAmazonSQS(), null, TimeSpan.Zero, _ => { }, typeof(ArgumentNullException)),
                TestCase( 2, new SpyAmazonSQS(), ""  , TimeSpan.Zero, null    , null),
                TestCase( 3, new SpyAmazonSQS(), ""  , TimeSpan.FromMilliseconds(-2)               , _ => { }, typeof(ArgumentOutOfRangeException)),
                TestCase( 4, new SpyAmazonSQS(), ""  , TimeSpan.FromMilliseconds(-1)               , _ => { }, null),
                TestCase( 5, new SpyAmazonSQS(), ""  , TimeSpan.FromMilliseconds(int.MaxValue)     , _ => { }, null),
                TestCase( 6, new SpyAmazonSQS(), ""  , TimeSpan.FromMilliseconds(int.MaxValue + 1d), _ => { }, typeof(ArgumentOutOfRangeException)),
                TestCase(10, new SpyAmazonSQS(), ""  , TimeSpan.Zero, _ => { }, null),
            }.Run();
        }

        [TestMethod]
        public void PostAsync() {
            Action TestCase(int testNumber, string message, HttpStatusCode responseStatus, CancellationToken cancellationToken, Type expectedExceptionType) => () => {
                var queueUrl = Rand.AlphaNums();
                var client = new SpyAmazonSQS(sendResponse: new SendMessageResponse { HttpStatusCode = responseStatus });
                var channel = new AmazonSQSChannel(client, queueUrl, default, null);

                new TestCaseRunner($"No.{testNumber}")
                    .Run(() => channel.PostAsync(message, cancellationToken))
                    .Verify(desc => {
                        client.ActualSendRequest.DeepIs(new SendMessageRequest(queueUrl, message), desc);
                        client.ActualReceiveRequest.DeepIs(null, desc);
                        client.ActualDeleteRequest.DeepIs(null, desc);
                    }, expectedExceptionType);
            };

            var canceledCts = new CancellationTokenSource();
            canceledCts.Cancel();
            new[] {
                TestCase( 0, ""  , (HttpStatusCode)399, canceledCts.Token, typeof(OperationCanceledException)),
                TestCase( 1, ""  , (HttpStatusCode)400, default          , typeof(MessagingException)),
                TestCase( 2, ""  , (HttpStatusCode)399, default          , null),
            }.Run();
        }

        [TestMethod]
        public void SubscribeAsync_NoLoop() {
            Action TestCase(int testNumber, SpyMessageConsumer consumer, int receiveStatusCode, int deleteStatusCode, bool expectedDeleted, Type expectedExceptionType = null) => () => {
                const int messageCount = 2;
                const int receiveCount = 1;
                const int expectedMaxNumberOfMessages = 10;

                var messages = Messages(messageCount);
                var client = new FakeAmazonSQS(messages, receiveStatusCode, deleteStatusCode);
                var queueUrl = "QURL:" + Rand.AlphaNums();
                var channel = new AmazonSQSChannel(client, queueUrl, pollingIdleTime: default, s => Console.WriteLine(s));

                var cts = new CancellationTokenSource(millisecondsDelay: 1000 /* テストが終わらない事に対する保険 */);
                client.OnPreReceive += () => {
                    if (client.ReceiveCount >= receiveCount) {
                        cts.Cancel();
                    }
                };

                new TestCaseRunner($"No.{testNumber}")
                    .Run(async () => {
                        try {
                            await channel.SubscribeAsync(consumer?.Delegate, cts.Token).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException) { }
                    })
                    .Verify(desc => {
                        client.ActualReceiveParams.DeepIs((new ReceiveMessageRequest(queueUrl) { MaxNumberOfMessages = expectedMaxNumberOfMessages }, cts.Token), desc);
                        if (expectedDeleted) {
                            client.ActualDeleteRequest.QueueUrl.Is(queueUrl, desc);
                            client.ActualDeleteEntries.DeepIs(messages.Select(x => new DeleteMessageBatchRequestEntry(x.MessageId, x.ReceiptHandle)), desc);
                            client.MessageCount.Is(0, desc);
                        }
                        else {
                            client.ActualDeleteRequest.Is(null, desc);
                            client.ActualDeleteEntries.Count.Is(0, desc);
                            client.MessageCount.Is(messageCount, desc);
                        }
                        client.ReceiveCount.Is(receiveCount, desc);

                        consumer.ActualParamList.DeepIs(messages.Select(x => (x.Body, cts.Token)), desc);
                    }, expectedExceptionType);
            };

            new[] {
                TestCase( 0, null                         , receiveStatusCode: 399, deleteStatusCode: 399, expectedDeleted: true , typeof(ArgumentNullException)),
                TestCase( 1, new SpyMessageConsumer(true ), receiveStatusCode: 399, deleteStatusCode: 399, expectedDeleted: true , null),
                TestCase( 2, new SpyMessageConsumer(true ), receiveStatusCode: 399, deleteStatusCode: 399, expectedDeleted: true , null),
                TestCase( 3, new SpyMessageConsumer(false), receiveStatusCode: 399, deleteStatusCode: 399, expectedDeleted: false, null),
                TestCase( 4, new SpyMessageConsumer(true ), receiveStatusCode: 400, deleteStatusCode: 399, expectedDeleted: false, typeof(MessagingException)),
                TestCase( 5, new SpyMessageConsumer(true ), receiveStatusCode: 399, deleteStatusCode: 400, expectedDeleted: true , typeof(MessagingException)),
            }.Run();
        }

        [TestMethod]
        public void SubscribeAsync_ConsumeAll() {
            Action TestCase(int testNumber, bool hasLogger, int messageCount, int receiveCount, bool expectedDeleted, Type expectedExceptionType = null) => () => {
                const int expectedMaxNumberOfMessages = 10;

                var messages = Messages(messageCount);
                var client = new FakeAmazonSQS(messages, receiveStatusCode: 200, deleteStatusCode: 200);
                var queueUrl = "QURL:" + Rand.AlphaNums();
                var logger = hasLogger ? s => Console.WriteLine(s) : (Action<string>)null;
                var channel = new AmazonSQSChannel(client, queueUrl, pollingIdleTime: default, logger);
                var consumer = new SpyMessageConsumer(true);

                var cts = new CancellationTokenSource(millisecondsDelay: 1000 /* テストが終わらない事に対する保険 */);
                client.OnPreReceive += () => {
                    if (client.ReceiveCount >= receiveCount) {
                        cts.Cancel();
                    }
                };

                new TestCaseRunner($"No.{testNumber}")
                    .Run(async () => {
                        try {
                            await channel.SubscribeAsync(consumer.Delegate, cts.Token).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException) { }
                    })
                    .Verify(desc => {
                        client.ActualReceiveParams.DeepIs((new ReceiveMessageRequest(queueUrl) { MaxNumberOfMessages = expectedMaxNumberOfMessages }, cts.Token), desc);
                        if (expectedDeleted) {
                            client.ActualDeleteRequest.QueueUrl.Is(queueUrl, desc);
                            client.ActualDeleteEntries.DeepIs(messages.Select(x => new DeleteMessageBatchRequestEntry(x.MessageId, x.ReceiptHandle)), desc);
                        }
                        else {
                            client.ActualDeleteRequest.Is(null, desc);
                            client.ActualDeleteEntries.Count.Is(0, desc);
                        }
                        client.MessageCount.Is(0, desc);
                        client.ReceiveCount.Is(receiveCount, desc);

                        consumer.ActualParamList.DeepIs(messages.Select(x => (x.Body, cts.Token)), desc);
                    }, expectedExceptionType);
            };

            new[] {
                TestCase( 0, hasLogger: true , messageCount:  0, receiveCount: 1, expectedDeleted: false),
                TestCase( 1, hasLogger: true , messageCount:  9, receiveCount: 1, expectedDeleted: true ),
                TestCase( 2, hasLogger: false, messageCount:  9, receiveCount: 2, expectedDeleted: true ),
                TestCase( 3, hasLogger: true , messageCount: 10, receiveCount: 1, expectedDeleted: true ),
                TestCase( 4, hasLogger: true , messageCount: 11, receiveCount: 2, expectedDeleted: true ),
            }.Run();
        }

        #region Helpers

        private static Message[] Messages(int count) => Enumerable.Range(0, count).Select(i => new Message {
            Body = "Body:" + i.ToString(),
            MessageId = "MID:" + Guid.NewGuid(),
            ReceiptHandle = "RH:" + Guid.NewGuid(),
        }).ToArray();

        private sealed class SpyMessageConsumer {
            private readonly Task<bool> _result;

            public SpyMessageConsumer(bool result) => _result = Task.FromResult(result);

            public List<(string message, CancellationToken cancellationToken)> ActualParamList { get; } = new List<(string message, CancellationToken cancellationToken)>();

            public MessageConsumer<string> Delegate => (message, cancellationToken) => {
                ActualParamList.Add((message, cancellationToken));
                return _result;
            };
        }

        public class FakeAmazonSQS : AbstractAmazonSQS {
            private readonly Dictionary<string, Message> _messages;
            private readonly int _receiveStatusCode;
            private readonly int _deleteStatusCode;

            public FakeAmazonSQS(Message[] messages, int receiveStatusCode, int deleteStatusCode) {
                _messages = messages?.ToDictionary(x => x.MessageId) ?? new Dictionary<string, Message>();
                _receiveStatusCode = receiveStatusCode;
                _deleteStatusCode = deleteStatusCode;
            }

            public event Action OnPreReceive;

            public (ReceiveMessageRequest request, CancellationToken cancellationToken) ActualReceiveParams { get; private set; }
            public DeleteMessageBatchRequest ActualDeleteRequest { get; private set; }
            public List<DeleteMessageBatchRequestEntry> ActualDeleteEntries { get; } = new List<DeleteMessageBatchRequestEntry>();
            public int MessageCount => _messages.Count;
            public int ReceiveCount { get; private set; }

            public override Task<SendMessageResponse> SendMessageAsync(SendMessageRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

            public override Task<ReceiveMessageResponse> ReceiveMessageAsync(ReceiveMessageRequest request, CancellationToken cancellationToken = default) {
                OnPreReceive?.Invoke();
                cancellationToken.ThrowIfCancellationRequested();
                ActualReceiveParams = (request, cancellationToken);
                ReceiveCount++;

                var response = new ReceiveMessageResponse { HttpStatusCode = (HttpStatusCode)_receiveStatusCode };
                response.Messages.AddRange(_messages.Values.Take(request.MaxNumberOfMessages));
                return Task.FromResult(response);
            }

            public override Task<DeleteMessageBatchResponse> DeleteMessageBatchAsync(DeleteMessageBatchRequest request, CancellationToken cancellationToken = default) {
                cancellationToken.ThrowIfCancellationRequested();
                ActualDeleteRequest = request;
                ActualDeleteEntries.AddRange(request.Entries);

                var response = new DeleteMessageBatchResponse { HttpStatusCode = (HttpStatusCode)_deleteStatusCode };
                foreach (var entry in request.Entries) {
                    if (_messages.Remove(entry.Id)) {
                        response.Successful.Add(new DeleteMessageBatchResultEntry { Id = entry.Id });
                    }
                }
                return Task.FromResult(response);
            }
        }

        #endregion Helpers
    }
}
