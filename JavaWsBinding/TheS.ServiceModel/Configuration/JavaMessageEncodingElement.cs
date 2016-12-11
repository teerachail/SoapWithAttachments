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
            binding.WriteEncoding = this.WriteEncoding;
            binding.MaxReadPoolSize = this.MaxReadPoolSize;
            binding.MaxWritePoolSize = this.MaxWritePoolSize;
//#pragma warning suppress 56506 //[....]; base.ApplyConfiguration() checks for 'binding' being null
//            this.ReaderQuotas.ApplyConfiguration(binding.ReaderQuotas);
            binding.MaxBufferSize = this.MaxBufferSize;
        }

        //protected internal override void InitializeFrom(BindingElement bindingElement)
        //{
        //    base.InitializeFrom(bindingElement);
        //    MtomMessageEncodingBindingElement binding = (MtomMessageEncodingBindingElement)bindingElement;
        //    SetPropertyValueIfNotDefaultValue(ConfigurationStrings.MessageVersion, binding.MessageVersion);
        //    SetPropertyValueIfNotDefaultValue(ConfigurationStrings.WriteEncoding, binding.WriteEncoding);
        //    SetPropertyValueIfNotDefaultValue(ConfigurationStrings.MaxReadPoolSize, binding.MaxReadPoolSize);
        //    SetPropertyValueIfNotDefaultValue(ConfigurationStrings.MaxWritePoolSize, binding.MaxWritePoolSize);
        //    this.ReaderQuotas.InitializeFrom(binding.ReaderQuotas);
        //    SetPropertyValueIfNotDefaultValue(ConfigurationStrings.MaxBufferSize, binding.MaxBufferSize);
        //}

        [ConfigurationProperty(ConfigurationStrings.MaxReadPoolSize, DefaultValue = EncoderDefaults.MaxReadPoolSize)]
        [IntegerValidator(MinValue = 1)]
        public int MaxReadPoolSize
        {
            get { return (int)base[ConfigurationStrings.MaxReadPoolSize]; }
            set { base[ConfigurationStrings.MaxReadPoolSize] = value; }
        }

        [ConfigurationProperty(ConfigurationStrings.MaxWritePoolSize, DefaultValue = EncoderDefaults.MaxWritePoolSize)]
        [IntegerValidator(MinValue = 1)]
        public int MaxWritePoolSize
        {
            get { return (int)base[ConfigurationStrings.MaxWritePoolSize]; }
            set { base[ConfigurationStrings.MaxWritePoolSize] = value; }
        }

        [ConfigurationProperty(ConfigurationStrings.MessageVersion, DefaultValue = TextEncoderDefaults.MessageVersionString)]
        [TypeConverter(typeof(MessageVersionConverter))]
        public MessageVersion MessageVersion
        {
            get { return (MessageVersion)base[ConfigurationStrings.MessageVersion]; }
            set { base[ConfigurationStrings.MessageVersion] = value; }
        }


        [ConfigurationProperty(ConfigurationStrings.ReaderQuotas)]
        public XmlDictionaryReaderQuotasElement ReaderQuotas
        {
            get { return (XmlDictionaryReaderQuotasElement)base[ConfigurationStrings.ReaderQuotas]; }
        }

        [ConfigurationProperty(ConfigurationStrings.MaxBufferSize, DefaultValue = MtomEncoderDefaults.MaxBufferSize)]
        [IntegerValidator(MinValue = 1)]
        public int MaxBufferSize
        {
            get { return (int)base[ConfigurationStrings.MaxBufferSize]; }
            set { base[ConfigurationStrings.MaxBufferSize] = value; }
        }

        [ConfigurationProperty(ConfigurationStrings.WriteEncoding, DefaultValue = TextEncoderDefaults.EncodingString)]
        [TypeConverter(typeof(EncodingConverter))]
        public Encoding WriteEncoding
        {
            get { return (Encoding)base[ConfigurationStrings.WriteEncoding]; }
            set { base[ConfigurationStrings.WriteEncoding] = value; }
        }

        protected override BindingElement CreateBindingElement()
        {
            JavaMessageEncodingBindingElement bindingElement = new JavaMessageEncodingBindingElement(MessageVersion, WriteEncoding);
            ApplyConfiguration(bindingElement);
            return bindingElement;
        }
    }
}
