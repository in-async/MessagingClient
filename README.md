# Inasync.MessagingClient
[![Build status](https://ci.appveyor.com/api/projects/status/9ms1bjmyijv1hnf1/branch/master?svg=true)](https://ci.appveyor.com/project/inasync/messagingclient/branch/master)
[![NuGet](https://img.shields.io/nuget/v/Inasync.MessagingClient.svg)](https://www.nuget.org/packages/Inasync.MessagingClient/)

***Inasync.MessagingClient*** はシンプルなメッセージング API を提供する .NET ヘルパーライブラリです。

## Target Frameworks
- .NET Standard 2.0+
- .NET Standard 1.0+
- .NET Framework 4.5+


## Usage
```cs
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
```


## Licence
This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details
