# Rebus.Async

[![install from nuget](https://img.shields.io/nuget/v/Rebus.Async.svg?style=flat-square)](https://www.nuget.org/packages/Rebus.Async)

Provides an extension to Rebus that allows for emulating synchronous request/reply in an asynchronous fashion. 

⚠ While it can be handy in a few select places, if you're not too keen on introducing HTTP, Protobuf, or another RPC protocol, I do not really recommend
that this package be used too much!(*) 

Check this out – first you enable synchronous request/reply (must be done at both ends! i.e. both in the requestor and in the replier!):

```csharp
services.AddRebus(
	configure => configure
		.(...)
		.Options(o => o.EnableSynchronousRequestReply())
		.(...)
);
```

and then you can

```csharp

var reply = await _bus.SendRequest<SomeReply>(new SomeRequest(), timeout: TimeSpan.FromSeconds(7));

// we have the reply here :)
```


![](https://raw.githubusercontent.com/rebus-org/Rebus/master/artwork/little_rebusbus2_copy-200x200.png)

---


(*) The reason is that everything in Rebus revolves around providing nice mechanisms for doing asynchronous, durable messaging, and the "synchronous request/reply" thing conflicts a great deal with that. 

The reason is that the requestor holds transient state after having issued a request, which will be lost if the process crashes, which in turn will result in a reply that has noone to handle it.

This might not be a problem, but I generally recommend that you try to model your code such that your messaging is truly asynchronous (i.e. requestors send their requests, replies are handled in a separate handler, possibly correlated with whatever you were doing by means of a correlation ID).
