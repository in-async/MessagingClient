using System.Threading;
using System.Threading.Tasks;
using Amazon.SQS.Model;

namespace Inasync.MessagingClient.Amazon.Tests {

    public class SpyAmazonSQS : AbstractAmazonSQS {
        private readonly SendMessageResponse _sendResponse;
        private readonly ReceiveMessageResponse _receiveResponse;
        private readonly DeleteMessageBatchResponse _deleteResponse;

        public SpyAmazonSQS(
              SendMessageResponse sendResponse = null
            , ReceiveMessageResponse receiveResponse = null
            , DeleteMessageBatchResponse deleteResponse = null
        ) {
            _sendResponse = sendResponse;
            _receiveResponse = receiveResponse;
            _deleteResponse = deleteResponse;
        }

        public SendMessageRequest ActualSendRequest { get; private set; }
        public ReceiveMessageRequest ActualReceiveRequest { get; private set; }
        public DeleteMessageBatchRequest ActualDeleteRequest { get; private set; }

        public override Task<SendMessageResponse> SendMessageAsync(SendMessageRequest request, CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();

            ActualSendRequest = request;
            return Task.FromResult(_sendResponse);
        }

        public override Task<ReceiveMessageResponse> ReceiveMessageAsync(ReceiveMessageRequest request, CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();

            ActualReceiveRequest = request;
            return Task.FromResult(_receiveResponse);
        }

        public override Task<DeleteMessageBatchResponse> DeleteMessageBatchAsync(DeleteMessageBatchRequest request, CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();

            ActualDeleteRequest = request;
            return Task.FromResult(_deleteResponse);
        }
    }
}
