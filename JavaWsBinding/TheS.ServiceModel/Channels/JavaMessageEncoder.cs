using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using TheS.Runtime;
using TheS.Xml;

namespace TheS.ServiceModel.Channels
{
    class JavaMessageEncoderFactory : MessageEncoderFactory
    {
        private JavaMessageEncoder encoder;

        internal static ContentEncoding[] Soap11Content = GetContentEncodingMap(MessageVersion.Soap11WSAddressing10);
        internal static ContentEncoding[] Soap12Content = GetContentEncodingMap(MessageVersion.Soap12WSAddressing10);
        internal static ContentEncoding[] SoapNoneContent = GetContentEncodingMap(MessageVersion.None);
        internal const string Soap11MediaType = "text/xml";
        internal const string Soap12MediaType = "application/soap+xml";
        internal const string XmlMediaType = "application/xml";

        public JavaMessageEncoderFactory(MessageVersion version, Encoding writeEncoding, int maxReadPoolSize, int maxWritePoolSize, int maxBufferSize, XmlDictionaryReaderQuotas quotas)
        {
            this.encoder = new JavaMessageEncoder(version, writeEncoding, maxReadPoolSize, maxWritePoolSize, maxBufferSize, quotas);
        }

        public override MessageEncoder Encoder
        {
            get
            {
                return this.encoder;
            }
        }

        public override MessageVersion MessageVersion
        {
            get { return encoder.MessageVersion; }
        }

        public int MaxWritePoolSize
        {
            get { return encoder.MaxWritePoolSize; }
        }

        public int MaxReadPoolSize
        {
            get { return encoder.MaxReadPoolSize; }
        }

        public XmlDictionaryReaderQuotas ReaderQuotas
        {
            get
            {
                return encoder.ReaderQuotas;
            }
        }

        public int MaxBufferSize
        {
            get { return encoder.MaxBufferSize; }
        }

        public static Encoding[] GetSupportedEncodings()
        {
            return new Encoding[] { Encoding.UTF8, Encoding.Unicode, Encoding.BigEndianUnicode };
        }

        internal static string GetMediaType(MessageVersion version)
        {
            string mediaType = null;
            if (version.Envelope == EnvelopeVersion.Soap12)
            {
                mediaType = JavaMessageEncoderFactory.Soap12MediaType;
            }
            else if (version.Envelope == EnvelopeVersion.Soap11)
            {
                mediaType = JavaMessageEncoderFactory.Soap11MediaType;
            }
            else if (version.Envelope == EnvelopeVersion.None)
            {
                mediaType = JavaMessageEncoderFactory.XmlMediaType;
            }
            else
            {
                throw new InvalidOperationException(
                    string.Format("SR.EnvelopeVersionNotSupported, {0}", version.Envelope));
            }
            return mediaType;
        }

        internal static string GetContentType(string mediaType, Encoding encoding)
        {
            return String.Format(CultureInfo.InvariantCulture, "{0}; charset={1}", mediaType, TextEncoderDefaults.EncodingToCharSet(encoding));
        }

        static ContentEncoding[] GetContentEncodingMap(MessageVersion version)
        {
            Encoding[] readEncodings = JavaMessageEncoderFactory.GetSupportedEncodings();
            string media = GetMediaType(version);
            ContentEncoding[] map = new ContentEncoding[readEncodings.Length];
            for (int i = 0; i < readEncodings.Length; i++)
            {
                ContentEncoding contentEncoding = new ContentEncoding();
                contentEncoding.contentType = GetContentType(media, readEncodings[i]);
                contentEncoding.encoding = readEncodings[i];
                map[i] = contentEncoding;
            }
            return map;
        }

        internal static Encoding GetEncodingFromContentType(string contentType, ContentEncoding[] contentMap)
        {
            if (contentType == null)
            {
                return null;
            }

            // Check for known/expected content types
            for (int i = 0; i < contentMap.Length; i++)
            {
                if (contentMap[i].contentType == contentType)
                {
                    return contentMap[i].encoding;
                }
            }

            // then some heuristic matches (since System.Mime.ContentType is a performance hit)
            // start by looking for a parameter. 

            // If none exists, we don't have an encoding
            int semiColonIndex = contentType.IndexOf(';');
            if (semiColonIndex == -1)
            {
                return null;
            }

            // optimize for charset being the first parameter
            int charsetValueIndex = -1;

            // for Indigo scenarios, we'll have "; charset=", so check for the c
            if ((contentType.Length > semiColonIndex + 11) // need room for parameter + charset + '=' 
                && contentType[semiColonIndex + 2] == 'c'
                && string.Compare("charset=", 0, contentType, semiColonIndex + 2, 8, StringComparison.OrdinalIgnoreCase) == 0)
            {
                charsetValueIndex = semiColonIndex + 10;
            }
            else
            {
                // look for charset= somewhere else in the message
                int paramIndex = contentType.IndexOf("charset=", semiColonIndex + 1, StringComparison.OrdinalIgnoreCase);
                if (paramIndex != -1)
                {
                    // validate there's only whitespace or semi-colons beforehand
                    for (int i = paramIndex - 1; i >= semiColonIndex; i--)
                    {
                        if (contentType[i] == ';')
                        {
                            charsetValueIndex = paramIndex + 8;
                            break;
                        }

                        if (contentType[i] == '\n')
                        {
                            if (i == semiColonIndex || contentType[i - 1] != '\r')
                            {
                                break;
                            }

                            i--;
                            continue;
                        }

                        if (contentType[i] != ' '
                            && contentType[i] != '\t')
                        {
                            break;
                        }
                    }
                }
            }

            string charSet;
            Encoding enc;

            // we have a possible charset value. If it's easy to parse, do so
            if (charsetValueIndex != -1)
            {
                // get the next semicolon
                semiColonIndex = contentType.IndexOf(';', charsetValueIndex);
                if (semiColonIndex == -1)
                {
                    charSet = contentType.Substring(charsetValueIndex);
                }
                else
                {
                    charSet = contentType.Substring(charsetValueIndex, semiColonIndex - charsetValueIndex);
                }

                // and some minimal quote stripping
                if (charSet.Length > 2 && charSet[0] == '"' && charSet[charSet.Length - 1] == '"')
                {
                    charSet = charSet.Substring(1, charSet.Length - 2);
                }

                Debug.Assert(charSet == (new ContentType(contentType)).CharSet,
                        "CharSet parsing failed to correctly parse the ContentType header.");

                if (TryGetEncodingFromCharSet(charSet, out enc))
                {
                    return enc;
                }
            }

            // our quick heuristics failed. fall back to System.Net
            try
            {
                ContentType parsedContentType = new ContentType(contentType);
                charSet = parsedContentType.CharSet;
            }
            catch (FormatException e)
            {
                throw new ProtocolException("SR.GetString(SR.EncoderBadContentType)", e);
            }

            if (TryGetEncodingFromCharSet(charSet, out enc))
                return enc;

            throw new ProtocolException(string.Format("SR.EncoderUnrecognizedCharSet, {0}", charSet));
        }

        internal static bool TryGetEncodingFromCharSet(string charSet, out Encoding encoding)
        {
            encoding = null;
            if (charSet == null || charSet.Length == 0)
                return true;

            return TextEncoderDefaults.TryGetEncoding(charSet, out encoding);
        }

        internal class ContentEncoding
        {
            internal string contentType;
            internal Encoding encoding;
        }
    }

    class JavaMessageEncoder : MessageEncoder
    {
        private MessageVersion version;
        private MessageEncoder innerMessageEncoder;

        internal const string DefaultNamespace = "http://tempuri.org/";
        const string mtomMediaType = "multipart/related";
        //const string mtomContentType = mtomMediaType + "; type=\"application/xop+xml\"";
        //const string mtomStartUri = NamingHelper.DefaultNamespace + "0";
        const string swaContentType = mtomMediaType + "; type=\"text/xml\"";
        const string swaStartUri = DefaultNamespace + "0";

        volatile SynchronizedPool<XmlDictionaryWriter> streamedWriterPool;
        volatile SynchronizedPool<XmlDictionaryReader> streamedReaderPool;

        object thisLock;
        const int maxPooledXmlReadersPerMessage = 2;
        int maxReadPoolSize;
        int maxWritePoolSize;
        //static UriGenerator mimeBoundaryGenerator;
        XmlDictionaryReaderQuotas readerQuotas;
        //XmlDictionaryReaderQuotas bufferedReadReaderQuotas;
        int maxBufferSize;

        internal JavaMessageEncoderFactory.ContentEncoding[] contentEncodingMap;
        OnXmlDictionaryReaderClose onStreamedReaderClose;

        private Encoding writeEncoding;

        public JavaMessageEncoder(MessageVersion version, Encoding writeEncoding, int maxReadPoolSize, int maxWritePoolSize, int maxBufferSize, XmlDictionaryReaderQuotas quotas)
        {
            this.version = version;
            var bindingElement = new MtomMessageEncodingBindingElement(
                version, Encoding.UTF8);

            bindingElement.WriteEncoding = writeEncoding;
            bindingElement.MaxReadPoolSize = maxReadPoolSize;
            bindingElement.MaxWritePoolSize = maxWritePoolSize;
            bindingElement.MaxBufferSize = maxBufferSize;
            this.innerMessageEncoder = bindingElement.CreateMessageEncoderFactory().Encoder;

            this.writeEncoding = writeEncoding;

            this.maxReadPoolSize = maxReadPoolSize;
            this.maxWritePoolSize = maxWritePoolSize;

            this.readerQuotas = new XmlDictionaryReaderQuotas();
            quotas.CopyTo(this.readerQuotas);

            //this.bufferedReadReaderQuotas = EncoderHelpers.GetBufferedReadQuotas(this.readerQuotas);

            this.maxBufferSize = maxBufferSize;

            this.thisLock = new object();

            this.onStreamedReaderClose = new OnXmlDictionaryReaderClose(ReturnStreamedReader);

            if (version.Envelope == EnvelopeVersion.Soap12)
            {
                this.contentEncodingMap = JavaMessageEncoderFactory.Soap12Content;
            }
            else if (version.Envelope == EnvelopeVersion.Soap11)
            {
                this.contentEncodingMap = JavaMessageEncoderFactory.Soap11Content;
            }
            else
            {
                Debug.Fail("Invalid MessageVersion");
                throw new InvalidOperationException(String.Format(CultureInfo.InvariantCulture, "Invalid MessageVersion"));
            }
        }

        public override string ContentType
        {
            get
            {
                return swaContentType;
            }
        }

        public override string MediaType
        {
            get
            {
                return this.innerMessageEncoder.MediaType;
            }
        }

        public override MessageVersion MessageVersion
        {
            get
            {
                return this.version;
            }
        }

        public int MaxWritePoolSize
        {
            get { return maxWritePoolSize; }
        }

        public int MaxReadPoolSize
        {
            get { return maxReadPoolSize; }
        }

        public XmlDictionaryReaderQuotas ReaderQuotas
        {
            get
            {
                return readerQuotas;
            }
        }

        public int MaxBufferSize
        {
            get { return maxBufferSize; }
        }

        public override Message ReadMessage(ArraySegment<byte> buffer, BufferManager bufferManager, string contentType)
        {
            throw new NotSupportedException();
        }

        public override Message ReadMessage(Stream stream, int maxSizeOfHeaders, string contentType)
        {
            if (stream == null)
                throw new ArgumentNullException("stream");

            if (contentType == this.ContentType)
                contentType = null;

            XmlReader reader = TakeStreamedReader(stream, contentType);
            Message message = Message.CreateMessage(reader, maxSizeOfHeaders, version);
            message.Properties.Encoder = this;

            return message;
        }

        public override void WriteMessage(Message message, Stream stream)
        {
            this.innerMessageEncoder.WriteMessage(message, stream);
            //WriteMessage(message, stream, GenerateStartInfoString(), null, null, true /*writeMessageHeaders*/);
        }

        //internal void WriteMessage(Message message, Stream stream, string boundary)
        //{
        //    WriteMessage(message, stream, GenerateStartInfoString(), boundary, swaStartUri, false /*writeMessageHeaders*/);
        //}

        public override ArraySegment<byte> WriteMessage(Message message, int maxMessageSize, BufferManager bufferManager, int messageOffset)
        {
            return this.innerMessageEncoder.WriteMessage(message, maxMessageSize, bufferManager, messageOffset);
            //throw new NotSupportedException();
        }

        string GenerateStartInfoString()
        {
            return (version.Envelope == EnvelopeVersion.Soap12) ? JavaMessageEncoderFactory.Soap12MediaType : JavaMessageEncoderFactory.Soap11MediaType;
        }

        void WriteMessage(Message message, Stream stream, string startInfo, string boundary, string startUri, bool writeMessageHeaders)
        {
            if (message == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException("message"));
            if (stream == null)
                throw new ArgumentNullException("stream");
            ThrowIfMismatchedMessageVersion(message);

            //EventTraceActivity eventTraceActivity = null;
            //if (TD.MtomMessageEncodingStartIsEnabled())
            //{
            //    eventTraceActivity = EventTraceActivityHelper.TryExtractActivity(message);
            //    TD.MtomMessageEncodingStart(eventTraceActivity);
            //}

            message.Properties.Encoder = this;
            //if (MessageLogger.LogMessagesAtTransportLevel)
            //    MessageLogger.LogMessage(ref message, MessageLoggingSource.TransportSend);
            XmlDictionaryWriter xmlWriter = TakeStreamedWriter(stream, startInfo, boundary, startUri, writeMessageHeaders);
            if (this.writeEncoding.WebName == "utf-8")
            {
                message.WriteMessage(xmlWriter);
            }
            else
            {
                xmlWriter.WriteStartDocument();
                message.WriteMessage(xmlWriter);
                xmlWriter.WriteEndDocument();
            }
            xmlWriter.Flush();
            ReturnStreamedWriter(xmlWriter);

            //if (TD.StreamedMessageWrittenByEncoderIsEnabled())
            //{
            //    TD.StreamedMessageWrittenByEncoder(eventTraceActivity ?? EventTraceActivityHelper.TryExtractActivity(message));
            //}
        }

        //public override IAsyncResult BeginWriteMessage(Message message, Stream stream, AsyncCallback callback, object state)
        //{
        //    return new WriteMessageAsyncResult(message, stream, this, callback, state);
        //}

        //internal IAsyncResult BeginWriteMessage(Message message, Stream stream, string boundary, AsyncCallback callback, object state)
        //{
        //    return new WriteMessageAsyncResult(message, stream, boundary, this, callback, state);
        //}

        //public override void EndWriteMessage(IAsyncResult result)
        //{
        //    WriteMessageAsyncResult.End(result);
        //}

        XmlReader TakeStreamedReader(Stream stream, string contentType)
        {
            if (streamedReaderPool == null)
            {
                lock (thisLock)
                {
                    if (streamedReaderPool == null)
                    {
                        streamedReaderPool = new SynchronizedPool<XmlDictionaryReader>(maxReadPoolSize);
                    }
                }
            }

            XmlDictionaryReader xmlReader = streamedReaderPool.Take();
            try
            {
                if (contentType == null || IsMTOMContentType(contentType))
                {
                    if (xmlReader != null && xmlReader is IXmlMtomReaderInitializer)
                    {
                        ((IXmlMtomReaderInitializer)xmlReader).SetInput(stream, JavaMessageEncoderFactory.GetSupportedEncodings(), contentType, this.readerQuotas, this.maxBufferSize, onStreamedReaderClose);
                    }
                    else
                    {
                        xmlReader = CreateSwaReader(stream, JavaMessageEncoderFactory.GetSupportedEncodings(), contentType, this.readerQuotas, this.maxBufferSize, onStreamedReaderClose);
                    }
                }
                else
                {
                    if (xmlReader != null && xmlReader is IXmlTextReaderInitializer)
                    {
                        ((IXmlTextReaderInitializer)xmlReader).SetInput(stream, JavaMessageEncoderFactory.GetEncodingFromContentType(contentType, this.contentEncodingMap), this.readerQuotas, onStreamedReaderClose);
                    }
                    else
                    {
                        xmlReader = XmlDictionaryReader.CreateTextReader(stream, JavaMessageEncoderFactory.GetEncodingFromContentType(contentType, this.contentEncodingMap), this.readerQuotas, onStreamedReaderClose);
                    }
                }
            }
            catch (FormatException fe)
            {
                throw new CommunicationException(
                    "SR.GetString(SR.SFxErrorCreatingMtomReader)", fe);
            }
            catch (XmlException xe)
            {
                throw new CommunicationException(
                    string.Format("SR.SFxErrorCreatingMtomReader, {0}", xe));
            }

            return xmlReader;
        }

        void ReturnStreamedReader(XmlDictionaryReader xmlReader)
        {
            streamedReaderPool.Return(xmlReader);
        }


        XmlDictionaryWriter TakeStreamedWriter(Stream stream, string startInfo, string boundary, string startUri, bool writeMessageHeaders)
        {
            if (streamedWriterPool == null)
            {
                lock (thisLock)
                {
                    if (streamedWriterPool == null)
                    {
                        streamedWriterPool = new SynchronizedPool<XmlDictionaryWriter>(maxWritePoolSize);
                    }
                }
            }
            XmlDictionaryWriter xmlWriter = streamedWriterPool.Take();
            if (xmlWriter == null)
            {
                xmlWriter = CreateSwaWriter(stream, this.writeEncoding, int.MaxValue, startInfo, boundary, startUri, writeMessageHeaders, false);
                //if (TD.WritePoolMissIsEnabled())
                //{
                //    TD.WritePoolMiss(xmlWriter.GetType().Name);
                //}
            }
            else
            {
                ((IXmlMtomWriterInitializer)xmlWriter).SetOutput(stream, this.writeEncoding, int.MaxValue, startInfo, boundary, startUri, writeMessageHeaders, false);
            }
            return xmlWriter;
        }

        void ReturnStreamedWriter(XmlDictionaryWriter xmlWriter)
        {
            xmlWriter.Close();
            streamedWriterPool.Return(xmlWriter);
        }

        internal bool IsMTOMContentType(string contentType)
        {
            // check for MTOM contentType: multipart/related; type=\"text/xml\"
            return IsContentTypeSupported(contentType, this.ContentType, this.MediaType);
        }

        internal bool IsTextContentType(string contentType)
        {
            // check for Text contentType: text/xml or application/soap+xml
            string textMediaType = JavaMessageEncoderFactory.GetMediaType(version);
            string textContentType = JavaMessageEncoderFactory.GetContentType(textMediaType, writeEncoding);
            return IsContentTypeSupported(contentType, textContentType, textMediaType);
        }

        public override bool IsContentTypeSupported(string contentType)
        {
            if (contentType == null)
                throw new ArgumentNullException("contentType");
            return (IsMTOMContentType(contentType) || IsTextContentType(contentType));
        }

        private bool IsContentTypeSupported(string contentType, string supportedContentType, string supportedMediaType)
        {
            if (supportedContentType == contentType)
                return true;

            if (contentType.Length > supportedContentType.Length &&
                contentType.StartsWith(supportedContentType, StringComparison.Ordinal) &&
                contentType[supportedContentType.Length] == ';')
                return true;

            // now check case-insensitively
            if (contentType.StartsWith(supportedContentType, StringComparison.OrdinalIgnoreCase))
            {
                if (contentType.Length == supportedContentType.Length)
                {
                    return true;
                }
                else if (contentType.Length > supportedContentType.Length)
                {
                    char ch = contentType[supportedContentType.Length];

                    // Linear Whitespace is allowed to appear between the end of one property and the semicolon.
                    // LWS = [CRLF]? (SP | HT)+
                    if (ch == ';')
                    {
                        return true;
                    }

                    // Consume the [CRLF]?
                    int i = supportedContentType.Length;
                    if (ch == '\r' && contentType.Length > supportedContentType.Length + 1 && contentType[i + 1] == '\n')
                    {
                        i += 2;
                        ch = contentType[i];
                    }

                    // Look for a ';' or nothing after (SP | HT)+
                    if (ch == ' ' || ch == '\t')
                    {
                        i++;
                        while (i < contentType.Length)
                        {
                            ch = contentType[i];
                            if (ch != ' ' && ch != '\t')
                                break;
                            ++i;
                        }
                    }
                    if (ch == ';' || i == contentType.Length)
                        return true;
                }
            }

            // sometimes we get a contentType that has parameters, but our encoders
            // merely expose the base content-type, so we will check a stripped version
            try
            {
                ContentType parsedContentType = new ContentType(contentType);

                if (supportedMediaType.Length > 0 && !supportedMediaType.Equals(parsedContentType.MediaType, StringComparison.OrdinalIgnoreCase))
                    return false;

                if (!IsCharSetSupported(parsedContentType.CharSet))
                    return false;
            }
            catch (FormatException)
            {
                // bad content type, so we definitely don't support it!
                return false;
            }

            return true;
        }

        private bool IsCharSetSupported(string charSet)
        {
            if (charSet == null || charSet.Length == 0)
                return true;

            Encoding tmp;
            return TextEncoderDefaults.TryGetEncoding(charSet, out tmp);
        }


        static public XmlDictionaryReader CreateMtomReader(Stream stream, Encoding[] encodings, string contentType,
            XmlDictionaryReaderQuotas quotas, int maxBufferSize, OnXmlDictionaryReaderClose onClose)
        {
            XmlMtomReader reader = new XmlMtomReader();
            reader.SetInput(stream, encodings, contentType, quotas, maxBufferSize, onClose);
            return reader;
        }

        static public XmlDictionaryReader CreateSwaReader(Stream stream, Encoding[] encodings, string contentType,
            XmlDictionaryReaderQuotas quotas, int maxBufferSize, OnXmlDictionaryReaderClose onClose)
        {
            XmlSwaReader reader = new XmlSwaReader();
            reader.SetInput(stream, encodings, contentType, quotas, maxBufferSize, onClose);
            return reader;
        }

        static public XmlDictionaryWriter CreateSwaWriter(Stream stream, Encoding encoding, int maxSizeInBytes, string startInfo, string boundary, string startUri, bool writeMessageHeaders, bool ownsStream)
        {
            XmlSwaWriter writer = new XmlSwaWriter();
            writer.SetOutput(stream, encoding, maxSizeInBytes, startInfo, boundary, startUri, writeMessageHeaders, ownsStream);
            return writer;
        }

        private void ThrowIfMismatchedMessageVersion(Message message)
        {
            if (message.Version != MessageVersion)
            {
                throw new ProtocolException(string.Format("SR.EncoderMessageVersionMismatch, {0}, {1}, {2}", message.Version, MessageVersion,
                    message));
            }
        }

        class WriteMessageAsyncResult : ScheduleActionItemAsyncResult
        {
            string boundary;
            JavaMessageEncoder encoder;
            Message message;
            Stream stream;
            bool writeBoundary;

            public WriteMessageAsyncResult(Message message, Stream stream, JavaMessageEncoder encoder, AsyncCallback callback, object state)
                : base(callback, state)
            {
                Fx.Assert(encoder != null, "encoder should never be null");

                this.encoder = encoder;
                this.message = message;
                this.stream = stream;

                Schedule();
            }

            public WriteMessageAsyncResult(Message message, Stream stream, string boundary, JavaMessageEncoder encoder, AsyncCallback callback, object state)
                : base(callback, state)
            {
                Fx.Assert(encoder != null, "encoder should never be null");

                this.encoder = encoder;
                this.message = message;
                this.stream = stream;
                this.boundary = boundary;

                this.writeBoundary = true;

                Schedule();
            }

            protected override void OnDoWork()
            {
                this.encoder.WriteMessage(this.message, this.stream, this.encoder.GenerateStartInfoString(), string.IsNullOrEmpty(this.boundary) ? null : this.boundary, this.writeBoundary ? JavaMessageEncoder.swaStartUri : null, !this.writeBoundary /*writeMessageHeaders*/);
            }
        }
    }
}
