using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Rebus.Activation;
using Rebus.Config;
using Rebus.Internals;
using Rebus.Messages;
using Rebus.Routing.TypeBased;
using Rebus.Tests.Contracts;
using Rebus.Transport.InMem;

namespace Rebus.Async.Tests
{
    [TestFixture]
    public class CanRegisterInlineHandler : FixtureBase
    {
        const string InputQueueName = "inline-handlers";
        IBusStarter _busStarter;
        BuiltinHandlerActivator _activator;

        protected override void SetUp()
        {
            _activator = new BuiltinHandlerActivator();

            Using(_activator);

            _busStarter = Configure.With(_activator)
                .Transport(t => t.UseInMemoryTransport(new InMemNetwork(), InputQueueName))
                .Options(o => o.EnableSynchronousRequestReply())
                .Routing(r => r.TypeBased().Map<SomeRequest>(InputQueueName))
                .Create();
        }

        [Test]
        public async Task CanDoRequestReply()
        {
            _activator.Handle<SomeRequest>(async (bus, request) =>
            {
                await bus.Reply(new SomeReply());
            });

            var theBus = _busStarter.Start();
            var reply = await theBus.SendRequest<SomeReply>(new SomeRequest());

            Assert.That(reply, Is.Not.Null);
        }

        [Test]
        public void SupportsTimeout()
        {
            var correlationIdOfRequest = "IS SET IN THE INLINE HANDLER";

            _activator.Handle<SomeRequest>(async (bus, context, request) =>
            {
                correlationIdOfRequest = context.Headers[Headers.CorrelationId];

                Console.WriteLine("Got request - waiting for 4 seconds...");
                await Task.Delay(4000);
                Console.WriteLine("Done waiting - sending reply (even though it's too late)");

                await bus.Reply(new SomeReply());
            });

            var theBus = _busStarter.Start();
            var aggregateException = Assert.Throws<AggregateException>(() =>
            {
                var reply = theBus.SendRequest<SomeReply>(new SomeRequest(), timeout: TimeSpan.FromSeconds(2)).Result;
            });

            Assert.That(aggregateException.InnerException, Is.TypeOf<TimeoutException>());

            var timeoutException = (TimeoutException)aggregateException.InnerException;

            Console.WriteLine(timeoutException);

            Assert.That(timeoutException.Message, Contains.Substring(correlationIdOfRequest));
        }

        [Test]
        public async Task CanPassHeadersAlongWithRequest()
        {
            var receivedHeaders = new ConcurrentQueue<KeyValuePair<string, string>>();

            _activator.Handle<SomeRequest>(async (bus, context, request) =>
            {
                foreach (var kvp in context.Headers)
                {
                    receivedHeaders.Enqueue(kvp);
                }

                await bus.Reply(new SomeReply());
            });
            var theBus = _busStarter.Start();

            const string customHeaderKey = "x-custom-header";
            const string customHeaderValue = "it works!";

            var optionalHeaders = new Dictionary<string, string> { { customHeaderKey, customHeaderValue } };

            await theBus.SendRequest<SomeReply>(new SomeRequest(), optionalHeaders);

            var dictionary = receivedHeaders.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            Assert.That(dictionary, Contains.Key(customHeaderKey));
            Assert.That(dictionary[customHeaderKey], Is.EqualTo(customHeaderValue),
                $"Did not find key-value-pair {customHeaderKey}={customHeaderValue} among the received headers");
        }

        [Test]
        public async Task CanPassCustomMessageIdHeaderAlongWithRequest()
        {
            string actualMessageId = null;
            _activator.Handle<SomeRequest>(async (bus, context, request) =>
            {
                actualMessageId = context.Headers[Headers.MessageId];
                await bus.Reply(new SomeReply());
            });

            var theBus = _busStarter.Start();

            var optionalHeaders = new Dictionary<string, string> { { Headers.MessageId, "desired_message_id" } };

            await theBus.SendRequest<SomeReply>(new SomeRequest(), optionalHeaders);

            var expectedMessageId = $"{ReplyHandlerStep.SpecialMessageIdPrefix}_desired_message_id";

            Assert.AreEqual(expectedMessageId, actualMessageId);
        }

        public class SomeRequest { }
        public class SomeReply { }
    }
}
