using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Rebus.Activation;
using Rebus.Config;
using Rebus.Extensions;
using Rebus.Messages;
using Rebus.Routing.TypeBased;
using Rebus.Tests.Contracts;
using Rebus.Transport;
using Rebus.Transport.InMem;

namespace Rebus.Async.Tests;

[TestFixture]
public class DoesNotUseTheseCharactersInMessageIdHeaderValue : FixtureBase
{
    [Test]
    [Description("Solves the special case where Amazon SQS refuses to send messages, because the rbs2-msg-id header is transferred as a native SQS message ID, and those are only allowed to contain alphanumeric characters and _ and -")]
    public async Task ItsTrueItDoesnt()
    {
        var activator = new BuiltinHandlerActivator();

        Using(activator);

        activator.Handle<TestRequest>(async (bus, request) => await bus.Reply(new TestReply()));

        var network = new InMemNetwork();

        var client = Configure.With(activator)
            .Transport(t =>
            {
                t.UseInMemoryTransport(network, "test-queue");
                t.Decorate(c => new ThrowOnInvalidCharactersTransportDecorator(c.Get<ITransport>()));
            })
            .Routing(t => t.TypeBased().Map<TestRequest>("test-queue"))
            .Options(o => o.EnableSynchronousRequestReply())
            .Start();

        var reply = await client.SendRequest<TestReply>(new TestRequest());

        Assert.That(reply, Is.TypeOf<TestReply>());
    }

    class TestRequest { }
    class TestReply { }

    class ThrowOnInvalidCharactersTransportDecorator : ITransport
    {
        readonly ITransport _transport;
        readonly Regex _validMessageIdRegex = new("^[a-zA-Z0-9\\-_]+$");

        public ThrowOnInvalidCharactersTransportDecorator(ITransport transport)
        {
            _transport = transport;
        }

        public void CreateQueue(string address) => _transport.CreateQueue(address);

        public string Address => _transport.Address;

        public Task<TransportMessage> Receive(ITransactionContext context, CancellationToken cancellationToken) => _transport.Receive(context, cancellationToken);

        public Task Send(string destinationAddress, TransportMessage message, ITransactionContext context)
        {
            var headers = message.Headers;
            var messageId = headers.GetValue(Headers.MessageId);

            if (!_validMessageIdRegex.IsMatch(messageId))
            {
                throw new ArgumentException($@"Sorry, but the message ID

    {messageId}

contains one or more invalid characters.");
            }

            return _transport.Send(destinationAddress, message, context);
        }
    }
}