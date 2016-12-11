using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using TheS.ServiceModel.Description;

namespace TheS.ServiceModel.Channels
{
    class JavaMessageEncodingBindingElement : MessageEncodingBindingElement, IWsdlExportExtension, IPolicyExportExtension
    {
        private MessageVersion version;
        private XmlDictionaryReaderQuotas readerQuotas;
        private int maxBufferSize;

        private int maxReadPoolSize;
        private int maxWritePoolSize;
        private Encoding writeEncoding;

        public JavaMessageEncodingBindingElement(MessageVersion messageVersion, Encoding writeEncoding)
        {
            if (messageVersion == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("messageVersion");

            if (messageVersion == MessageVersion.None)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentException(string.Format("SR.MtomEncoderBadMessageVersion, {0}", messageVersion.ToString()), "messageVersion"));

            if (writeEncoding == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("writeEncoding");

            this.version = messageVersion;

            this.maxReadPoolSize = EncoderDefaults.MaxReadPoolSize;
            this.maxWritePoolSize = EncoderDefaults.MaxWritePoolSize;
            this.readerQuotas = new XmlDictionaryReaderQuotas();
            EncoderDefaults.ReaderQuotas.CopyTo(this.readerQuotas);
            this.maxBufferSize = MtomEncoderDefaults.MaxBufferSize;
            this.writeEncoding = writeEncoding;
        }

        [DefaultValue(EncoderDefaults.MaxReadPoolSize)]
        public int MaxReadPoolSize
        {
            get
            {
                return this.maxReadPoolSize;
            }
            set
            {
                if (value <= 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException("value", value,
                                                    "SR.ValueMustBePositive"));
                }
                this.maxReadPoolSize = value;
            }
        }

        [DefaultValue(EncoderDefaults.MaxWritePoolSize)]
        public int MaxWritePoolSize
        {
            get
            {
                return this.maxWritePoolSize;
            }
            set
            {
                if (value <= 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException("value", value,
                                                    "SR.ValueMustBePositive"));
                }
                this.maxWritePoolSize = value;
            }
        }

        public XmlDictionaryReaderQuotas ReaderQuotas
        {
            get
            {
                return this.readerQuotas;
            }
        }

        [DefaultValue(MtomEncoderDefaults.MaxBufferSize)]
        public int MaxBufferSize
        {
            get
            {
                return this.maxBufferSize;
            }
            set
            {
                if (value <= 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException("value", value,
                                                    "SR.ValueMustBePositive"));
                }
                this.maxBufferSize = value;
            }
        }

        [TypeConverter(typeof(TheS.ServiceModel.Configuration.EncodingConverter))]
        public Encoding WriteEncoding
        {
            get
            {
                return this.writeEncoding;
            }
            set
            {
                if (value == null)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("value");

                TextEncoderDefaults.ValidateEncoding(value);
                this.writeEncoding = value;
            }
        }

        public override MessageVersion MessageVersion
        {
            get
            {
                return this.version;
            }
            set
            {
                this.version = value;
            }
        }

        public override BindingElement Clone()
        {
            return this;
        }

        public override MessageEncoderFactory CreateMessageEncoderFactory()
        {
            return new JavaMessageEncoderFactory(MessageVersion, this.writeEncoding, this.MaxReadPoolSize, this.maxWritePoolSize, this.maxBufferSize, this.readerQuotas);
        }

        public override IChannelFactory<TChannel> BuildChannelFactory<TChannel>(BindingContext context)
        {
            context.BindingParameters.Add(this);
            return context.BuildInnerChannelFactory<TChannel>();
        }
        public override IChannelListener<TChannel> BuildChannelListener<TChannel>(BindingContext context)
        {
            context.BindingParameters.Add(this);
            return context.BuildInnerChannelListener<TChannel>();
        }
        public override bool CanBuildChannelFactory<TChannel>(BindingContext context)
        {
            return context.CanBuildInnerChannelFactory<TChannel>();
        }
        public override bool CanBuildChannelListener<TChannel>(BindingContext context)
        {
            return context.CanBuildInnerChannelListener<TChannel>();
        }

        void IPolicyExportExtension.ExportPolicy(MetadataExporter exporter, PolicyConversionContext policyContext)
        {
            if (policyContext == null)
            {
                throw new ArgumentNullException("policyContext");
            }
            XmlDocument document = new XmlDocument();

            policyContext.GetBindingAssertions().Add(document.CreateElement(
                MessageEncodingPolicyConstants.OptimizedMimeSerializationPrefix,
                MessageEncodingPolicyConstants.MtomEncodingName,
                MessageEncodingPolicyConstants.OptimizedMimeSerializationNamespace));
        }

        void IWsdlExportExtension.ExportContract(WsdlExporter exporter, WsdlContractConversionContext context) { }
        void IWsdlExportExtension.ExportEndpoint(WsdlExporter exporter, WsdlEndpointConversionContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            SoapHelper.SetSoapVersion(context, exporter, this.version.Envelope);
        }
    }

    static class MessageEncodingPolicyConstants
    {
        public const string BinaryEncodingName = "BinaryEncoding";
        public const string BinaryEncodingNamespace = "http://schemas.microsoft.com/ws/06/2004/mspolicy/netbinary1";
        public const string BinaryEncodingPrefix = "msb";
        public const string OptimizedMimeSerializationNamespace = "http://schemas.xmlsoap.org/ws/2004/09/policy/optimizedmimeserialization";
        public const string OptimizedMimeSerializationPrefix = "wsoma";
        public const string MtomEncodingName = "OptimizedMimeSerialization";
    }
}
