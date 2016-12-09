using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading.Tasks;

namespace TheS.ServiceModel.Channels
{
    class JavaMessageEncoderFactory : MessageEncoderFactory
    {
        private JavaMessageEncoder encoder;
        private MessageVersion version;

        public JavaMessageEncoderFactory(MessageVersion version)
        {
            this.version = version;
            this.encoder = new JavaMessageEncoder(version);
        }

        public override MessageEncoder Encoder
        {
            get
            {
                return this.encoder;
            }
        }

        public override MessageVersion MessageVersion
        {
            get
            {
                return this.version;
            }
        }
    }

    class JavaMessageEncoder : MessageEncoder
    {
        private MessageVersion version;
        private MessageEncoder innerMessageEncoder;

        public JavaMessageEncoder(MessageVersion version)
        {
            this.version = version;
            this.innerMessageEncoder = new MtomMessageEncodingBindingElement(
                version, Encoding.UTF8).CreateMessageEncoderFactory().Encoder;
        }

        public override string ContentType
        {
            get
            {
                return this.innerMessageEncoder.ContentType;
            }
        }

        public override string MediaType
        {
            get
            {
                return this.innerMessageEncoder.MediaType;
            }
        }

        public override MessageVersion MessageVersion
        {
            get
            {
                return this.version;
            }
        }

        public override Message ReadMessage(ArraySegment<byte> buffer, BufferManager bufferManager, string contentType)
        {
            return this.innerMessageEncoder.ReadMessage(buffer, bufferManager, contentType);
        }

        public override Message ReadMessage(Stream stream, int maxSizeOfHeaders, string contentType)
        {
            return this.ReadMessage(stream, maxSizeOfHeaders, contentType);
        }

        public override void WriteMessage(Message message, Stream stream)
        {
            this.innerMessageEncoder.WriteMessage(message, stream);
        }

        public override ArraySegment<byte> WriteMessage(Message message, int maxMessageSize, BufferManager bufferManager, int messageOffset)
        {
            return this.innerMessageEncoder.WriteMessage(message, maxMessageSize, bufferManager, messageOffset);
        }
    }
}
