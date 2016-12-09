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
        public override MessageEncoder Encoder
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public override MessageVersion MessageVersion
        {
            get
            {
                throw new NotImplementedException();
            }
        }
    }

    class JavaMessageEncoder : MessageEncoder
    {
        public override string ContentType
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public override string MediaType
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public override MessageVersion MessageVersion
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public override Message ReadMessage(ArraySegment<byte> buffer, BufferManager bufferManager, string contentType)
        {
            throw new NotImplementedException();
        }

        public override Message ReadMessage(Stream stream, int maxSizeOfHeaders, string contentType)
        {
            throw new NotImplementedException();
        }

        public override void WriteMessage(Message message, Stream stream)
        {
            throw new NotImplementedException();
        }

        public override ArraySegment<byte> WriteMessage(Message message, int maxMessageSize, BufferManager bufferManager, int messageOffset)
        {
            throw new NotImplementedException();
        }
    }
}
