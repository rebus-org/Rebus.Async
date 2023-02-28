using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using Rebus.Activation;
using Rebus.Config;
using Rebus.Routing.TypeBased;
using Rebus.Tests.Contracts;
using Rebus.Transport.InMem;
#pragma warning disable 1998

namespace Rebus.Async.Tests;

[TestFixture]
public class DoesNotDeadlock : FixtureBase
{
    readonly InMemNetwork _network = new(true);

    protected override void SetUp()
    {
        _network.Reset();
    }

    [Test]
    public async Task CanDoItInHandler()
    {
        var replier = CreateBus("server");
        replier.Activator.Handle<string>(async (bus, context, text) =>
        {
            await bus.Reply($"Got text: {text}");
        });
        replier.BusStarter.Start();

        var requestor = CreateBus("requestor");

        requestor.Activator.Handle<Request>(async (bus, _, request) =>
        {
            var texts = new List<string>();

            for (var index = 0; index < request.Count; index++)
            {
                Console.WriteLine("SENDING REQUEST");
                var result = await bus.SendRequest<string>($"Text {index}");
                Console.WriteLine("GOT REPLY REQUEST");

                texts.Add(result);
            }

            await bus.Reply(new Response(texts));
        });
        requestor.BusStarter.Start();

        var client = CreateBus("client");

        var theBus = client.BusStarter.Start();
        var response = await theBus.SendRequest<Response>(new Request(4));

        Assert.That(response.Texts.Count, Is.EqualTo(4));
    }

    class Request
    {
        public int Count { get; }

        public Request(int count)
        {
            Count = count;
        }
    }

    class Response
    {
        public IReadOnlyCollection<string> Texts { get; }

        public Response(IReadOnlyCollection<string> texts) => Texts = texts;
    }

    class BusWrapper
    {
        public IBusStarter BusStarter;
        public BuiltinHandlerActivator Activator;
    }

    BusWrapper CreateBus(string queueName)
    {
        var activator = new BuiltinHandlerActivator();

        Using(activator);

        var busStarter = Configure.With(activator)
            .Transport(t => t.UseInMemoryTransport(_network, queueName))
            .Routing(r => r.TypeBased()
                .Map<Request>("requestor")
                .Map<string>("server"))
            .Options(o => o.EnableSynchronousRequestReply())
            .Create();

        return new BusWrapper
        {
            BusStarter = busStarter,
            Activator = activator,
        };
    }
}