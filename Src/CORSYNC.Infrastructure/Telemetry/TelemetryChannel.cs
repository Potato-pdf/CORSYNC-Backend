using System.Threading.Channels;
using CORSYNC.Core.Domain;

namespace CORSYNC.Infrastructure.Telemetry
{
    public class TelemetryChannel
    {
        private readonly Channel<LecturaCorazon> _channel;

        public TelemetryChannel()
        {
            _channel = Channel.CreateUnbounded<LecturaCorazon>(new UnboundedChannelOptions
            {
                SingleWriter = true,
                SingleReader = false
            });
        }

        public ChannelWriter<LecturaCorazon> Writer => _channel.Writer;
        public ChannelReader<LecturaCorazon> Reader => _channel.Reader;
    }
}
