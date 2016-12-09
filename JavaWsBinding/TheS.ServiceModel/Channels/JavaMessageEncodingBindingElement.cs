using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading.Tasks;

namespace TheS.ServiceModel.Channels
{
    class JavaMessageEncodingBindingElement : MessageEncodingBindingElement
    {
        private MessageVersion msgVersion = MessageVersion.Default;

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
            return new JavaMessageEncoderFactory(MessageVersion);
        }
    }
}
