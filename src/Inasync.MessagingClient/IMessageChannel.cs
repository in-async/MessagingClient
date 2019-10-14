using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Inasync.MessagingClient {

    /// <summary>
    /// メッセージ チャネルを表すインターフェース。
    /// </summary>
    /// <typeparam name="TMessage">メッセージを表す非 <c>null</c> 型。</typeparam>
    public interface IMessageChannel<TMessage> : IInputMessageChannel<TMessage>, IOutputMessageChannel<TMessage> {
    }

    /// <summary>
    /// メッセージ チャネルの入力を表すインターフェース。
    /// </summary>
    /// <typeparam name="TMessage">メッセージを表す非 <c>null</c> 型。</typeparam>
    public interface IInputMessageChannel<TMessage> {

        /// <summary>
        /// メッセージ チャネルにメッセージを送信します。
        /// </summary>
        /// <param name="message">チャネルへ送信するメッセージ。</param>
        /// <param name="cancellationToken">キャンセル トークン。</param>
        /// <exception cref="MessagingException">メッセージ チャネルのエラーにより、メッセージの送信に失敗した。</exception>
        /// <exception cref="OperationCanceledException">キャンセル要求に従い、操作が中止された。</exception>
        Task PostAsync(TMessage message, CancellationToken cancellationToken);
    }

    /// <summary>
    /// <see cref="IInputMessageChannel{TMessage}"/> の拡張メソッドを定義するクラス。
    /// </summary>
    public static class InputMessageChannelExtensions {

        /// <summary>
        /// メッセージ チャネルにメッセージを送信します。
        /// </summary>
        /// <typeparam name="TMessage">メッセージを表す非 <c>null</c> 型。</typeparam>
        /// <param name="messageChannel">対象のメッセージ チャネル。</param>
        /// <param name="message">チャネルへ送信するメッセージ。</param>
        /// <exception cref="MessagingException">メッセージ チャネルのエラーにより、メッセージの送信に失敗した。</exception>
        /// <exception cref="OperationCanceledException">キャンセル要求に従い、操作が中止された。</exception>
        public static Task PostAsync<TMessage>(this IInputMessageChannel<TMessage> messageChannel, TMessage message) => messageChannel.PostAsync(message, cancellationToken: default);
    }

    /// <summary>
    /// メッセージ チャネルの出力を表すインターフェース。
    /// </summary>
    /// <typeparam name="TMessage">メッセージを表す非 <c>null</c> 型。</typeparam>
    public interface IOutputMessageChannel<TMessage> {

        /// <summary>
        /// キャンセルされるまで、別スレッド上でメッセージを受信・処理し続けます。
        /// </summary>
        /// <param name="consumer">メッセージを処理するデリゲート。</param>
        /// <param name="cancellationToken">キャンセル トークン。</param>
        /// <exception cref="ArgumentNullException"><paramref name="consumer"/> is <c>null</c>.</exception>
        /// <exception cref="MessagingException">メッセージ チャネルまたは <paramref name="consumer"/> のエラーにより、メッセージの受信または消費に失敗した。</exception>
        /// <exception cref="OperationCanceledException">キャンセル要求に従い、操作が中止された。</exception>
        Task SubscribeAsync(MessageChunkConsumerFunc<TMessage> consumer, CancellationToken cancellationToken);
    }

    /// <summary>
    /// メッセージを処理するデリゲート。
    /// </summary>
    /// <typeparam name="TMessage">メッセージを表す非 <c>null</c> 型。</typeparam>
    /// <param name="messages">処理するメッセージのリスト。非 <c>null</c>。</param>
    /// <param name="results">各メッセージを消費したかどうかを示す <see cref="bool"/> 配列。非 <c>null</c>。配列の長さは <paramref name="messages" /> のそれと同じ。 </param>
    /// <param name="cancellationToken">キャンセル トークン。</param>
    /// <exception cref="OperationCanceledException">キャンセル要求に従い、操作が中止された。</exception>
    /// <exception cref="Exception">回復不能な例外やアプリケーションに通知すべき任意の例外が生じた。</exception>
    public delegate Task MessageChunkConsumerFunc<TMessage>(IReadOnlyList<TMessage> messages, bool[] results, CancellationToken cancellationToken);
}
