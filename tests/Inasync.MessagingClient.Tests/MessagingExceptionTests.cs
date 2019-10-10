using System;
using Inasync;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Inasync.MessagingClient.Tests {

    [TestClass]
    public class MessagingExceptionTests {

        [TestMethod]
        public void Ctor() {
            new TestCaseRunner()
                .Run(() => new MessagingException())
                .Verify((actual, desc) => {
                    Assert.IsNull(actual.InnerException, desc);
                }, (Type)null);

            var message = "MSG:" + Rand.String();
            new TestCaseRunner()
                .Run(() => new MessagingException(message))
                .Verify((actual, desc) => {
                    Assert.AreEqual(message, actual.Message, desc);
                    Assert.IsNull(actual.InnerException, desc);
                }, (Type)null);

            var innerException = new Exception();
            new TestCaseRunner()
                .Run(() => new MessagingException(message, innerException))
                .Verify((actual, desc) => {
                    Assert.AreEqual(message, actual.Message, desc);
                    Assert.AreSame(innerException, actual.InnerException, desc);
                }, (Type)null);
        }
    }
}
