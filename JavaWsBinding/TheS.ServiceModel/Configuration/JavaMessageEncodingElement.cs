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

        public override void ApplyConfiguration(BindingElement bindingElement)
        {
            base.ApplyConfiguration(bindingElement);

            JavaMessageEncodingBindingElement binding = (JavaMessageEncodingBindingElement)bindingElement;
            binding.MessageVersion = this.MessageVersion;
//            binding.WriteEncoding = this.WriteEncoding;
//            binding.MaxReadPoolSize = this.MaxReadPoolSize;
//            binding.MaxWritePoolSize = this.MaxWritePoolSize;
//#pragma warning suppress 56506 //[....]; base.ApplyConfiguration() checks for 'binding' being null
//            this.ReaderQuotas.ApplyConfiguration(binding.ReaderQuotas);
            //binding.MaxBufferSize = this.MaxBufferSize;
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
            JavaMessageEncodingBindingElement bindingElement = new JavaMessageEncodingBindingElement(MessageVersion);
            ApplyConfiguration(bindingElement);
            return bindingElement;
        }
    }
}
