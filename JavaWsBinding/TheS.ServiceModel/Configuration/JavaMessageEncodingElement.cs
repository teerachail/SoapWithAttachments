using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel.Channels;
using System.ServiceModel.Configuration;
using System.Text;
using System.Threading.Tasks;
using TheS.ServiceModel.Channels;

namespace TheS.ServiceModel.Configuration
{
    class JavaMessageEncodingElement : BindingElementExtensionElement
    {
        public override Type BindingElementType
        {
            get
            {
                return typeof(JavaMessageEncodingBindingElement);
            }
        }

        protected override BindingElement CreateBindingElement()
        {
            JavaMessageEncodingBindingElement bindingElement = new JavaMessageEncodingBindingElement();
            ApplyConfiguration(bindingElement);
            return bindingElement;
        }
    }
}
