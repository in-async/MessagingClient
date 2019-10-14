using System;

#if NET45
using System.Runtime.Serialization;
#endif

namespace Inasync.MessagingClient {
    /// <summary>
    /// メッセージング システムとのやり取りの過程 (主に <see cref="IMessageChannel{TMessage}"/>) で生じたエラーを表す例外クラス。
    /// </summary>
#if NET45
    [Serializable]
#endif

    public class MessagingException : Exception {

        /// <summary>
        /// <see cref="MessagingException"/> クラスの新しいインスタンスを初期化します。
        /// </summary>
        public MessagingException() {
        }

        /// <summary>
        /// 指定したエラー メッセージを使用して、<see cref="MessagingException"/> クラスの新しいインスタンスを初期化します。
        /// </summary>
        /// <param name="message">エラーを説明するメッセージ。</param>
        public MessagingException(string message) : base(message) {
        }

        /// <summary>
        /// 指定したエラー メッセージおよびこの例外の原因となった内部例外への参照を使用して、<see cref="MessagingException"/> クラスの新しいインスタンスを初期化します。
        /// </summary>
        /// <param name="message">例外の原因を説明するエラー メッセージ。</param>
        /// <param name="innerException">現在の例外の原因である例外。内部例外が指定されていない場合は <c>null</c> 参照。</param>
        public MessagingException(string message, Exception innerException) : base(message, innerException) {
        }

#if NET45
        /// <summary>
        /// シリアル化したデータを使用して、<see cref="MessagingException"/> クラスの新しいインスタンスを初期化します。
        /// </summary>
        /// <param name="info">スローされている例外に関するシリアル化済みオブジェクト データを保持している <see cref="SerializationInfo"/>。</param>
        /// <param name="context">転送元または転送先についてのコンテキスト情報を含む <see cref="StreamingContext"/>。</param>
        protected MessagingException(SerializationInfo info, StreamingContext context) : base(info, context: context) {
        }
#endif
    }
}
