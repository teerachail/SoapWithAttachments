using System;
using System.Collections.Generic;
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

        public JavaMessageEncodingBindingElement(MessageVersion version)
        {
            this.version = version;
            this.readerQuotas = new XmlDictionaryReaderQuotas();
            EncoderDefaults.ReaderQuotas.CopyTo(this.readerQuotas);
            this.maxBufferSize = MtomEncoderDefaults.MaxBufferSize;
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
            return new JavaMessageEncoderFactory(MessageVersion, this.maxBufferSize, this.readerQuotas);
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
