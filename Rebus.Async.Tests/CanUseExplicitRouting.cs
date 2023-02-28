using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using NUnit.Framework;
using Rebus.Activation;
using Rebus.Bus;
using Rebus.Config;
using Rebus.Pipeline;
using Rebus.Tests.Contracts;
using Rebus.Transport.InMem;
#pragma warning disable CS1998

namespace Rebus.Async.Tests;

[TestFixture]
public class CanUseExplicitRouting : FixtureBase
{
    private InMemNetwork _network;

    protected override void SetUp()
    {
        base.SetUp();
        _network = new InMemNetwork();
    }

    [Test]
    public async Task CanSpecifyDestinationWhenSending()
    {
        var messageReceivedByRecipient1 = new ConcurrentQueue<string>();
        var messageReceivedByRecipient2 = new ConcurrentQueue<string>();

        async Task HandlerFunction(IBus bus, IMessageContext context, ConcurrentQueue<string> receivedMessages, string str)
        {
            receivedMessages.Enqueue(str);
            await bus.Reply($"got request: '{str}'");
        }

        GetBus("recipient1", handlers: activator => activator.Handle<string>((bus, context, str) => HandlerFunction(bus, context, messageReceivedByRecipient1, str)));

        GetBus("recipient2", handlers: activator => activator.Handle<string>((bus, context, str) => HandlerFunction(bus, context, messageReceivedByRecipient2, str)));

        var sender = GetBus("sender");

        var reply1 = await sender.Advanced.Routing.SendRequest<string>("recipient1", "hello recipient1");
        var reply2 = await sender.Advanced.Routing.SendRequest<string>("recipient2", "hello recipient2");

        Assert.That(reply1, Is.EqualTo("got request: 'hello recipient1'"));
        Assert.That(reply2, Is.EqualTo("got request: 'hello recipient2'"));
        Assert.That(messageReceivedByRecipient1, Is.EqualTo(new[] { "hello recipient1" }));
        Assert.That(messageReceivedByRecipient2, Is.EqualTo(new[] { "hello recipient2" }));
    }

    IBus GetBus(string queueName, Action<BuiltinHandlerActivator> handlers = null)
    {
        var activator = Using(new BuiltinHandlerActivator());

        handlers?.Invoke(activator);

        return Configure.With(activator)
            .Transport(t => t.UseInMemoryTransport(_network, queueName))
            .Options(o => o.EnableSynchronousRequestReply())
            .Start();
    }
}