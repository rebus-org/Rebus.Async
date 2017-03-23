using System;
using Rebus.Async;
using Rebus.Logging;
using Rebus.Pipeline;
using Rebus.Pipeline.Receive;
using Rebus.Threading;

namespace Rebus.Config
{
    /// <summary>
    /// Configuration extensions for Async
    /// </summary>
    public static class AsyncConfigurationExtensions
    {
        /// <summary>
        /// Enables async/await-based request/reply whereby a request can be sent using the <see cref="AsyncBusExtensions.SendRequest{TReply}"/> method
        /// which can be awaited for a corresponding reply.
        /// </summary>
        public static void EnableSynchronousRequestReply(this OptionsConfigurer configurer, int replyMaxAgeSeconds = 10)
        {
            configurer.Register(c =>
            {
                var rebusLoggerFactory = c.Get<IRebusLoggerFactory>();
                var asyncTaskFactory = c.Get<IAsyncTaskFactory>();
                var replyMaxAge = TimeSpan.FromSeconds(replyMaxAgeSeconds);
                var step = new ReplyHandlerStep(AsyncBusExtensions.Messages, rebusLoggerFactory, asyncTaskFactory, replyMaxAge);
                return step;
            });

            configurer.Decorate<IPipeline>(c =>
            {
                var pipeline = c.Get<IPipeline>();
                var step = c.Get<ReplyHandlerStep>();
                return new PipelineStepInjector(pipeline)
                    .OnReceive(step, PipelineRelativePosition.Before, typeof(ActivateHandlersStep));
            });
        }

    }
}