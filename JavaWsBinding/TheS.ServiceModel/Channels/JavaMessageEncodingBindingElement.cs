using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace TheS.ServiceModel.Channels
{
    class JavaMessageEncodingBindingElement : MessageEncodingBindingElement
    {
        private MessageVersion msgVersion = MessageVersion.Default;
        private XmlDictionaryReaderQuotas readerQuotas;
        private int maxBufferSize;

        public JavaMessageEncodingBindingElement(MessageVersion version)
        {
            this.msgVersion = version;
            this.readerQuotas = new XmlDictionaryReaderQuotas();
            EncoderDefaults.ReaderQuotas.CopyTo(this.readerQuotas);
            this.maxBufferSize = MtomEncoderDefaults.MaxBufferSize;
        }

        public override MessageVersion MessageVersion
        {
            get
            {
                return this.msgVersion;
            }
            set
            {
                this.msgVersion = value;
            }
        }

        public override BindingElement Clone()
        {
            return this;
        }

        public override MessageEncoderFactory CreateMessageEncoderFactory()
        {
            return new JavaMessageEncoderFactory(MessageVersion, this.maxBufferSize, this.readerQuotas);
        }
    }
}
