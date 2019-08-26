using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Rebus.Activation;
using Rebus.Config;
using Rebus.Exceptions;
using Rebus.Logging;
using Rebus.Routing.TypeBased;
using Rebus.Tests.Contracts;
using Rebus.Transport.InMem;
// ReSharper disable RedundantArgumentDefaultValue
// ReSharper disable ArgumentsStyleLiteral
// ReSharper disable UnusedVariable

namespace Rebus.Async.Tests
{
    [TestFixture]
    public class VerifyThatCleanupHappensAutomatically : FixtureBase
    {
        [TestCase(100)]
        [TestCase(1000)]
        [TestCase(10000)]
        public async Task ItSureDoes(int count)
        {
            var network = new InMemNetwork(outputEventsToConsole: false);
            var server = Using(new BuiltinHandlerActivator());

            server.Handle<SimpleRequest>((bus, request) =>
            {
                if (request.Number % 97 == 0)
                {
                    throw new TripUpTheHandlerException();
                }
                return bus.Reply(new SimpleReply());
            });

            Configure.With(server)
                .Logging(l => l.None())
                .Transport(t => t.UseInMemoryTransport(network, "server"))
                .Options(o => o.EnableSynchronousRequestReply())
                .Start();

            var client = Using(new BuiltinHandlerActivator());

            Configure.With(client)
                .Logging(l => l.None())
                .Transport(t => t.UseInMemoryTransport(network, "client"))
                .Routing(r => r.TypeBased().Map<SimpleRequest>("server"))
                .Options(o => o.EnableSynchronousRequestReply())
                .Start();

            var operations = Enumerable.Range(0, count)
                .Select(async n =>
                {
                    try
                    {
                        var reply = await client.Bus.SendRequest<SimpleReply>(new SimpleRequest(n));

                        // some will complete,
                    }
                    catch (TimeoutException)
                    {
                        // others will time out
                    }
                })
                .ToList();

            await Task.WhenAll(operations);

            Assert.That(AsyncBusExtensions.Messages.Count, Is.EqualTo(0), 
                "There should be exactly zero task completion sources in the dictionary by now");
        }

        class SimpleRequest
        {
            public int Number { get; }

            public SimpleRequest(int number)
            {
                Number = number;
            }
        }
        class SimpleReply { }

        class TripUpTheHandlerException : Exception, IFailFastException
        {
        }
    }
}