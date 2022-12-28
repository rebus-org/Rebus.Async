using System;
using Rebus.Internals;
using Rebus.Logging;
using Rebus.Pipeline;
using Rebus.Pipeline.Receive;

namespace Rebus.Config;

/// <summary>
/// Configuration extensions for Async
/// </summary>
public static class AsyncConfigurationExtensions
{
    /// <summary>
    /// Enables async/await-based request/reply whereby a request can be sent using the <see cref="AsyncBusExtensions.SendRequest{TReply}"/> method
    /// which can be awaited for a corresponding reply.
    /// </summary>
    public static void EnableSynchronousRequestReply(this OptionsConfigurer configurer)
    {
        if (configurer == null) throw new ArgumentNullException(nameof(configurer));

        configurer.Register(c =>
        {
            var rebusLoggerFactory = c.Get<IRebusLoggerFactory>();
            var step = new ReplyHandlerStep(AsyncBusExtensions.Messages, rebusLoggerFactory);
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