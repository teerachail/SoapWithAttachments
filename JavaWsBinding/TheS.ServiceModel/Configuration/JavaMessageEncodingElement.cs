using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
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

        [ConfigurationProperty(ConfigurationStrings.MessageVersion, DefaultValue = TextEncoderDefaults.MessageVersionString)]
        [TypeConverter(typeof(MessageVersionConverter))]
        public MessageVersion MessageVersion
        {
            get { return (MessageVersion)base[ConfigurationStrings.MessageVersion]; }
            set { base[ConfigurationStrings.MessageVersion] = value; }
        }

        protected override BindingElement CreateBindingElement()
        {
            JavaMessageEncodingBindingElement bindingElement = new JavaMessageEncodingBindingElement();
            ApplyConfiguration(bindingElement);
            return bindingElement;
        }
    }
}
