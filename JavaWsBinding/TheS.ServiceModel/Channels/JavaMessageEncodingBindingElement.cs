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
        public override MessageVersion MessageVersion
        {
            get
            {
                throw new NotImplementedException();
            }

            set
            {
                throw new NotImplementedException();
            }
        }

        public override BindingElement Clone()
        {
            throw new NotImplementedException();
        }

        public override MessageEncoderFactory CreateMessageEncoderFactory()
        {
            throw new NotImplementedException();
        }
    }
}
