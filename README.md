# Rebus.Async

[![install from nuget](https://img.shields.io/nuget/v/Rebus.Async.svg?style=flat-square)](https://www.nuget.org/packages/Rebus.Async)

Provides an experimental async extension to Rebus that allows for emulating synchronous request/reply in an asyncronous fashion.

Check this out:

	var bus = Configure.With(_activator)
		.(...)
		.Options(o => o.EnableSynchronousRequestReply(replyMaxAgeSeconds: 7))
		.(...);


	// (...)


    var reply = await _bus.SendRequest<SomeReply>(new SomeRequest());

	// we have the reply here :)



![](https://raw.githubusercontent.com/rebus-org/Rebus/master/artwork/little_rebusbus2_copy-200x200.png)

---


