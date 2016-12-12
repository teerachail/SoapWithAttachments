//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace TheS.Xml
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Runtime;
    using System.Runtime.Serialization;
    using System.Text;
    using System.Security;
    using System.Security.Permissions;
    using System.Xml;
    using ServiceModel;
    using Runtime;
    using Runtime.Serialization;
    using Text;

    class XmlSwaReader : XmlDictionaryReader, IXmlLineInfo, IXmlMtomReaderInitializer
    {
        Encoding[] encodings;
        XmlDictionaryReader xmlReader;
        XmlDictionaryReader infosetReader;
        MimeReader mimeReader;
        Dictionary<string, MimePart> mimeParts;
        OnXmlDictionaryReaderClose onClose;
        bool readingBinaryElement;
        int maxBufferSize;
        int bufferRemaining;
        MimePart part;

        public XmlSwaReader()
        {
        }

        internal static void DecrementBufferQuota(int maxBuffer, ref int remaining, int size)
        {
            if (remaining - size <= 0)
            {
                remaining = 0;
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(string.Format("SR.MtomBufferQuotaExceeded, {0}", maxBuffer)));
            }
            else
            {
                remaining -= size;
            }
        }

        void SetReadEncodings(Encoding[] encodings)
        {
            if (encodings == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("encodings");

            for (int i = 0; i < encodings.Length; i++)
            {
                if (encodings[i] == null)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(String.Format(CultureInfo.InvariantCulture, "encodings[{0}]", i));
            }

            this.encodings = new Encoding[encodings.Length];
            encodings.CopyTo(this.encodings, 0);
        }

        void CheckContentType(string contentType)
        {
            if (contentType != null && contentType.Length == 0)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentException("SR.MtomContentTypeInvalid", "contentType"));
        }

        public void SetInput(byte[] buffer, int offset, int count, Encoding[] encodings, string contentType, XmlDictionaryReaderQuotas quotas, int maxBufferSize, OnXmlDictionaryReaderClose onClose)
        {
            SetInput(new MemoryStream(buffer, offset, count), encodings, contentType, quotas, maxBufferSize, onClose);
        }

        public void SetInput(Stream stream, Encoding[] encodings, string contentType, XmlDictionaryReaderQuotas quotas, int maxBufferSize, OnXmlDictionaryReaderClose onClose)
        {
            SetReadEncodings(encodings);
            CheckContentType(contentType);
            Initialize(stream, contentType, quotas, maxBufferSize);
            this.onClose = onClose;
        }

        void Initialize(Stream stream, string contentType, XmlDictionaryReaderQuotas quotas, int maxBufferSize)
        {
            if (stream == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("stream");

            this.maxBufferSize = maxBufferSize;
            this.bufferRemaining = maxBufferSize;

            string boundary, start, startInfo;

            if (contentType == null)
            {
                MimeMessageReader messageReader = new MimeMessageReader(stream);
                MimeHeaders messageHeaders = messageReader.ReadHeaders(this.maxBufferSize, ref this.bufferRemaining);
                ReadMessageMimeVersionHeader(messageHeaders.MimeVersion);
                ReadMessageContentTypeHeader(messageHeaders.ContentType, out boundary, out start, out startInfo);
                stream = messageReader.GetContentStream();
                if (stream == null)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException("SR.MtomMessageInvalidContent"));
            }
            else
            {
                ReadMessageContentTypeHeader(new ContentTypeHeader(contentType), out boundary, out start, out startInfo);
            }

            this.mimeReader = new MimeReader(stream, boundary);
            this.mimeParts = null;
            this.readingBinaryElement = false;

            MimePart infosetPart = (start == null) ? ReadRootMimePart() : ReadMimePart(GetStartUri(start));
            byte[] infosetBytes = infosetPart.GetBuffer(this.maxBufferSize, ref this.bufferRemaining);
            int infosetByteCount = (int)infosetPart.Length;

            Encoding encoding = ReadRootContentTypeHeader(infosetPart.Headers.ContentType, this.encodings, startInfo);
            CheckContentTransferEncodingOnRoot(infosetPart.Headers.ContentTransferEncoding);

            IXmlTextReaderInitializer initializer = xmlReader as IXmlTextReaderInitializer;

            if (initializer != null)
                initializer.SetInput(infosetBytes, 0, infosetByteCount, encoding, quotas, null);
            else
                xmlReader = XmlDictionaryReader.CreateTextReader(infosetBytes, 0, infosetByteCount, encoding, quotas, null);
        }

        public override XmlDictionaryReaderQuotas Quotas
        {
            get
            {
                return this.xmlReader.Quotas;
            }
        }

        void ReadMessageMimeVersionHeader(MimeVersionHeader header)
        {
            if (header != null && header.Version != MimeVersionHeader.Default.Version)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(string.Format("SR.MtomMessageInvalidMimeVersion, {0}, {1}", header.Version, MimeVersionHeader.Default.Version)));
        }

        void ReadMessageContentTypeHeader(ContentTypeHeader header, out string boundary, out string start, out string startInfo)
        {
            if (header == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException("SR.MtomMessageContentTypeNotFound"));

            if (String.Compare(MtomGlobals.MediaType, header.MediaType, StringComparison.OrdinalIgnoreCase) != 0
                || String.Compare(MtomGlobals.MediaSubtype, header.MediaSubtype, StringComparison.OrdinalIgnoreCase) != 0)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(string.Format("SR.MtomMessageNotMultipart, {0}, {1}", MtomGlobals.MediaType, MtomGlobals.MediaSubtype)));

            string type;
            if (!header.Parameters.TryGetValue(MtomGlobals.TypeParam, out type) || (MtomGlobals.XopType != type && MtomGlobals.SwaType != type))
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(string.Format("SR.MtomMessageNotApplicationXopXml, {0}", MtomGlobals.XopType)));

            if (!header.Parameters.TryGetValue(MtomGlobals.BoundaryParam, out boundary))
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(string.Format("SR.MtomMessageRequiredParamNotSpecified, {0}", MtomGlobals.BoundaryParam)));
            if (!MailBnfHelper.IsValidMimeBoundary(boundary))
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(string.Format("SR.MtomBoundaryInvalid, {0}", boundary)));

            if (!header.Parameters.TryGetValue(MtomGlobals.StartParam, out start))
                start = null;

            if (!header.Parameters.TryGetValue(MtomGlobals.StartInfoParam, out startInfo))
                startInfo = null;
        }

        Encoding ReadRootContentTypeHeader(ContentTypeHeader header, Encoding[] expectedEncodings, string expectedType)
        {
            if (header == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException("SR.MtomRootContentTypeNotFound"));

            if ((String.Compare(MtomGlobals.XopMediaType, header.MediaType, StringComparison.OrdinalIgnoreCase) != 0
                && String.Compare(MtomGlobals.SwaMediaType, header.MediaType, StringComparison.OrdinalIgnoreCase) != 0)
                || (String.Compare(MtomGlobals.XopMediaSubtype, header.MediaSubtype, StringComparison.OrdinalIgnoreCase) != 0
                && String.Compare(MtomGlobals.SwaMediaSubType, header.MediaSubtype, StringComparison.OrdinalIgnoreCase) != 0))
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(string.Format("SR.MtomRootNotApplicationXopXml, {0}, {1}", MtomGlobals.XopMediaType, MtomGlobals.XopMediaSubtype)));

            string charset;
            if (!header.Parameters.TryGetValue(MtomGlobals.CharsetParam, out charset)
                || charset == null || charset.Length == 0)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(string.Format("SR.MtomRootRequiredParamNotSpecified, {0}", MtomGlobals.CharsetParam)));
            Encoding encoding = null;
            for (int i = 0; i < encodings.Length; i++)
            {
                if (String.Compare(charset, expectedEncodings[i].WebName, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    encoding = expectedEncodings[i];
                    break;
                }
            }
            if (encoding == null)
            {
                // Check for alternate names
                if (String.Compare(charset, "utf-16LE", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    for (int i = 0; i < encodings.Length; i++)
                    {
                        if (String.Compare(expectedEncodings[i].WebName, Encoding.Unicode.WebName, StringComparison.OrdinalIgnoreCase) == 0)
                        {
                            encoding = expectedEncodings[i];
                            break;
                        }
                    }
                }
                else if (String.Compare(charset, "utf-16BE", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    for (int i = 0; i < encodings.Length; i++)
                    {
                        if (String.Compare(expectedEncodings[i].WebName, Encoding.BigEndianUnicode.WebName, StringComparison.OrdinalIgnoreCase) == 0)
                        {
                            encoding = expectedEncodings[i];
                            break;
                        }
                    }
                }

                if (encoding == null)
                {
                    StringBuilder expectedCharSetStr = new StringBuilder();
                    for (int i = 0; i < encodings.Length; i++)
                    {
                        if (expectedCharSetStr.Length != 0)
                            expectedCharSetStr.Append(" | ");
                        expectedCharSetStr.Append(encodings[i].WebName);
                    }
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(string.Format("SR.MtomRootUnexpectedCharset, {0}, {1}", charset, expectedCharSetStr.ToString())));
                }
            }

            //if (expectedType != null)
            //{
            //    string rootType;
            //    if (!header.Parameters.TryGetValue(MtomGlobals.TypeParam, out rootType)
            //        || rootType == null || rootType.Length == 0)
            //        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(string.Format("SR.MtomRootRequiredParamNotSpecified, {0}", MtomGlobals.TypeParam)));
            //    if (rootType != expectedType)
            //        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(string.Format("SR.MtomRootUnexpectedType, {0}, {1}", rootType, expectedType)));
            //}

            return encoding;
        }

        // 7bit is default encoding in the absence of content-transfer-encoding header 
        void CheckContentTransferEncodingOnRoot(ContentTransferEncodingHeader header)
        {
            if (header != null && header.ContentTransferEncoding == ContentTransferEncoding.Other)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(string.Format("SR.MtomContentTransferEncodingNotSupported, {0}, {1}, {2}, {3}",
                                                                                      header.Value,
                                                                                      ContentTransferEncodingHeader.SevenBit.ContentTransferEncodingValue,
                                                                                      ContentTransferEncodingHeader.EightBit.ContentTransferEncodingValue,
                                                                                      ContentTransferEncodingHeader.Binary.ContentTransferEncodingValue)));
        }

        void CheckContentTransferEncodingOnBinaryPart(ContentTransferEncodingHeader header)
        {
            if (header == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(string.Format("SR.MtomContentTransferEncodingNotPresent, {0}",
                    ContentTransferEncodingHeader.Binary.ContentTransferEncodingValue)));
            else if (header.ContentTransferEncoding != ContentTransferEncoding.Binary)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(string.Format("SR.MtomInvalidTransferEncodingForMimePart, {0}, {1}",
                    header.Value, ContentTransferEncodingHeader.Binary.ContentTransferEncodingValue)));
        }

        string GetStartUri(string startUri)
        {
            if (startUri.StartsWith("<", StringComparison.Ordinal))
            {
                if (startUri.EndsWith(">", StringComparison.Ordinal))
                    return startUri;
                else
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(string.Format("SR.MtomInvalidStartUri, {0}", startUri)));
            }
            else
                return String.Format(CultureInfo.InvariantCulture, "<{0}>", startUri);
        }

        public override bool Read()
        {
            bool retVal = xmlReader.Read();

            if (xmlReader.NodeType == XmlNodeType.Element)
            {
                XopIncludeReader binaryDataReader = null;
                if (xmlReader.IsStartElement(MtomGlobals.XopIncludeLocalName, MtomGlobals.XopIncludeNamespace))
                {
                    string uri = null;
                    while (xmlReader.MoveToNextAttribute())
                    {
                        if (xmlReader.LocalName == MtomGlobals.XopIncludeHrefLocalName && xmlReader.NamespaceURI == MtomGlobals.XopIncludeHrefNamespace)
                            uri = xmlReader.Value;
                        else if (xmlReader.NamespaceURI == MtomGlobals.XopIncludeNamespace)
                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(string.Format("SR.MtomXopIncludeInvalidXopAttributes, {1}, {2}", xmlReader.LocalName, MtomGlobals.XopIncludeNamespace)));
                    }
                    if (uri == null)
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(string.Format("SR.MtomXopIncludeHrefNotSpecified, {0}", MtomGlobals.XopIncludeHrefLocalName)));

                    MimePart mimePart = ReadMimePart(uri);

                    CheckContentTransferEncodingOnBinaryPart(mimePart.Headers.ContentTransferEncoding);

                    this.part = mimePart;
                    binaryDataReader = new XopIncludeReader(mimePart, xmlReader);
                    binaryDataReader.Read();

                    xmlReader.MoveToElement();
                    if (xmlReader.IsEmptyElement)
                    {
                        xmlReader.Read();
                    }
                    else
                    {
                        int xopDepth = xmlReader.Depth;
                        xmlReader.ReadStartElement();

                        while (xmlReader.Depth > xopDepth)
                        {
                            if (xmlReader.IsStartElement() && xmlReader.NamespaceURI == MtomGlobals.XopIncludeNamespace)
                                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(string.Format("SR.MtomXopIncludeInvalidXopElement, {0}, {1}", xmlReader.LocalName, MtomGlobals.XopIncludeNamespace)));

                            xmlReader.Skip();
                        }

                        xmlReader.ReadEndElement();
                    }
                }

                if (binaryDataReader != null)
                {
                    this.xmlReader.MoveToContent();
                    this.infosetReader = this.xmlReader;
                    this.xmlReader = binaryDataReader;
                    binaryDataReader = null;
                }
            }

            if (xmlReader.ReadState == ReadState.EndOfFile && infosetReader != null)
            {
                // Read past the containing EndElement if necessary
                if (!retVal)
                    retVal = infosetReader.Read();

                this.part.Release(this.maxBufferSize, ref this.bufferRemaining);
                xmlReader = infosetReader;
                infosetReader = null;
            }

            return retVal;
        }

        MimePart ReadMimePart(string uri)
        {
            MimePart part = null;

            if (uri == null || uri.Length == 0)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException("SR.MtomInvalidEmptyURI"));

            string contentID = null;
            if (uri.StartsWith(MimeGlobals.ContentIDScheme, StringComparison.Ordinal))
                contentID = String.Format(CultureInfo.InvariantCulture, "<{0}>", Uri.UnescapeDataString(uri.Substring(MimeGlobals.ContentIDScheme.Length)));
            else if (uri.StartsWith("<", StringComparison.Ordinal))
                contentID = uri;

            if (contentID == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(string.Format("SR.MtomInvalidCIDUri, {0}", uri)));

            if (mimeParts != null && mimeParts.TryGetValue(contentID, out part))
            {
                if (part.ReferencedFromInfoset)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(string.Format("SR.MtomMimePartReferencedMoreThanOnce, {0}", contentID)));
            }
            else
            {
                int maxMimeParts = AppSettings.MaxMimeParts;
                while (part == null && mimeReader.ReadNextPart())
                {
                    MimeHeaders headers = mimeReader.ReadHeaders(this.maxBufferSize, ref this.bufferRemaining);
                    Stream contentStream = mimeReader.GetContentStream();
                    if (contentStream == null)
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException("SR.MtomMessageInvalidContentInMimePart"));

                    ContentIDHeader contentIDHeader = (headers == null) ? null : headers.ContentID;
                    if (contentIDHeader == null || contentIDHeader.Value == null)
                    {
                        // Skip content if Content-ID header is not present
                        int size = 256;
                        byte[] bytes = new byte[size];

                        int read = 0;
                        do
                        {
                            read = contentStream.Read(bytes, 0, size);
                        }
                        while (read > 0);
                        continue;
                    }

                    string currentContentID = headers.ContentID.Value;
                    MimePart currentPart = new MimePart(contentStream, headers);
                    if (mimeParts == null)
                        mimeParts = new Dictionary<string, MimePart>();

                    mimeParts.Add(currentContentID, currentPart);

                    if (mimeParts.Count > maxMimeParts)
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(string.Format("SR.MaxMimePartsExceeded, {0}, {1}", maxMimeParts, AppSettings.MaxMimePartsAppSettingsString)));

                    if (currentContentID.Equals(contentID))
                        part = currentPart;
                    else
                        currentPart.GetBuffer(this.maxBufferSize, ref this.bufferRemaining);
                }

                if (part == null)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(string.Format("SR.MtomPartNotFound, {0}", uri)));
            }

            part.ReferencedFromInfoset = true;
            return part;
        }

        MimePart ReadRootMimePart()
        {
            MimePart part = null;

            if (!mimeReader.ReadNextPart())
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException("SR.MtomRootPartNotFound"));

            MimeHeaders headers = mimeReader.ReadHeaders(this.maxBufferSize, ref this.bufferRemaining);
            Stream contentStream = mimeReader.GetContentStream();
            if (contentStream == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException("SR.MtomMessageInvalidContentInMimePart"));
            part = new MimePart(contentStream, headers);

            return part;
        }

        void AdvanceToContentOnElement()
        {
            if (NodeType != XmlNodeType.Attribute)
            {
                MoveToContent();
            }
        }

        public override int AttributeCount
        {
            get
            {
                return xmlReader.AttributeCount;
            }
        }

        public override string BaseURI
        {
            get
            {
                return xmlReader.BaseURI;
            }
        }

        public override bool CanReadBinaryContent
        {
            get
            {
                return xmlReader.CanReadBinaryContent;
            }
        }

        public override bool CanReadValueChunk
        {
            get
            {
                return xmlReader.CanReadValueChunk;
            }
        }

        public override bool CanResolveEntity
        {
            get
            {
                return xmlReader.CanResolveEntity;
            }
        }

        public override void Close()
        {
            xmlReader.Close();
            mimeReader.Close();
            OnXmlDictionaryReaderClose onClose = this.onClose;
            this.onClose = null;
            if (onClose != null)
            {
                try
                {
                    onClose(this);
                }
                catch (Exception e)
                {
                    if (Fx.IsFatal(e)) throw;

                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperCallback(e);
                }
            }
        }

        public override int Depth
        {
            get
            {
                return xmlReader.Depth;
            }
        }

        public override bool EOF
        {
            get
            {
                return xmlReader.EOF;
            }
        }

        public override string GetAttribute(int index)
        {
            return xmlReader.GetAttribute(index);
        }

        public override string GetAttribute(string name)
        {
            return xmlReader.GetAttribute(name);
        }

        public override string GetAttribute(string name, string ns)
        {
            return xmlReader.GetAttribute(name, ns);
        }

        public override string GetAttribute(XmlDictionaryString localName, XmlDictionaryString ns)
        {
            return xmlReader.GetAttribute(localName, ns);
        }
#if NO
        public override ArraySegment<byte> GetSubset(bool advance) 
        { 
            return xmlReader.GetSubset(advance); 
        }
#endif
        public override bool HasAttributes
        {
            get
            {
                return xmlReader.HasAttributes;
            }
        }

        public override bool HasValue
        {
            get
            {
                return xmlReader.HasValue;
            }
        }

        public override bool IsDefault
        {
            get
            {
                return xmlReader.IsDefault;
            }
        }

        public override bool IsEmptyElement
        {
            get
            {
                return xmlReader.IsEmptyElement;
            }
        }

        public override bool IsLocalName(string localName)
        {
            return xmlReader.IsLocalName(localName);
        }

        public override bool IsLocalName(XmlDictionaryString localName)
        {
            return xmlReader.IsLocalName(localName);
        }

        public override bool IsNamespaceUri(string ns)
        {
            return xmlReader.IsNamespaceUri(ns);
        }

        public override bool IsNamespaceUri(XmlDictionaryString ns)
        {
            return xmlReader.IsNamespaceUri(ns);
        }

        public override bool IsStartElement()
        {
            return xmlReader.IsStartElement();
        }

        public override bool IsStartElement(string localName)
        {
            return xmlReader.IsStartElement(localName);
        }

        public override bool IsStartElement(string localName, string ns)
        {
            return xmlReader.IsStartElement(localName, ns);
        }

        public override bool IsStartElement(XmlDictionaryString localName, XmlDictionaryString ns)
        {
            return xmlReader.IsStartElement(localName, ns);
        }
#if NO
        public override bool IsStartSubsetElement()
        {
            return xmlReader.IsStartSubsetElement();
        }
#endif
        public override string LocalName
        {
            get
            {
                return xmlReader.LocalName;
            }
        }

        public override string LookupNamespace(string ns)
        {
            return xmlReader.LookupNamespace(ns);
        }

        public override void MoveToAttribute(int index)
        {
            xmlReader.MoveToAttribute(index);
        }

        public override bool MoveToAttribute(string name)
        {
            return xmlReader.MoveToAttribute(name);
        }

        public override bool MoveToAttribute(string name, string ns)
        {
            return xmlReader.MoveToAttribute(name, ns);
        }

        public override bool MoveToElement()
        {
            return xmlReader.MoveToElement();
        }

        public override bool MoveToFirstAttribute()
        {
            return xmlReader.MoveToFirstAttribute();
        }

        public override bool MoveToNextAttribute()
        {
            return xmlReader.MoveToNextAttribute();
        }

        public override string Name
        {
            get
            {
                return xmlReader.Name;
            }
        }

        public override string NamespaceURI
        {
            get
            {
                return xmlReader.NamespaceURI;
            }
        }

        public override XmlNameTable NameTable
        {
            get
            {
                return xmlReader.NameTable;
            }
        }

        public override XmlNodeType NodeType
        {
            get
            {
                return xmlReader.NodeType;
            }
        }

        public override string Prefix
        {
            get
            {
                return xmlReader.Prefix;
            }
        }

        public override char QuoteChar
        {
            get
            {
                return xmlReader.QuoteChar;
            }
        }

        public override bool ReadAttributeValue()
        {
            return xmlReader.ReadAttributeValue();
        }

        public override object ReadContentAs(Type returnType, IXmlNamespaceResolver namespaceResolver)
        {
            AdvanceToContentOnElement();
            return xmlReader.ReadContentAs(returnType, namespaceResolver);
        }

        public override byte[] ReadContentAsBase64()
        {
            AdvanceToContentOnElement();
            return xmlReader.ReadContentAsBase64();
        }

        public override int ReadValueAsBase64(byte[] buffer, int offset, int count)
        {
            AdvanceToContentOnElement();
            return xmlReader.ReadValueAsBase64(buffer, offset, count);
        }

        public override int ReadContentAsBase64(byte[] buffer, int offset, int count)
        {
            AdvanceToContentOnElement();
            return xmlReader.ReadContentAsBase64(buffer, offset, count);
        }

        public override int ReadElementContentAsBase64(byte[] buffer, int offset, int count)
        {
            if (!readingBinaryElement)
            {
                if (IsEmptyElement)
                {
                    Read();
                    return 0;
                }

                ReadStartElement();
                readingBinaryElement = true;
            }

            int i = ReadContentAsBase64(buffer, offset, count);

            if (i == 0)
            {
                ReadEndElement();
                readingBinaryElement = false;
            }

            return i;
        }

        public override int ReadElementContentAsBinHex(byte[] buffer, int offset, int count)
        {
            if (!readingBinaryElement)
            {
                if (IsEmptyElement)
                {
                    Read();
                    return 0;
                }

                ReadStartElement();
                readingBinaryElement = true;
            }

            int i = ReadContentAsBinHex(buffer, offset, count);

            if (i == 0)
            {
                ReadEndElement();
                readingBinaryElement = false;
            }

            return i;
        }

        public override int ReadContentAsBinHex(byte[] buffer, int offset, int count)
        {
            AdvanceToContentOnElement();
            return xmlReader.ReadContentAsBinHex(buffer, offset, count);
        }

        public override bool ReadContentAsBoolean()
        {
            AdvanceToContentOnElement();
            return xmlReader.ReadContentAsBoolean();
        }

        public override int ReadContentAsChars(char[] chars, int index, int count)
        {
            AdvanceToContentOnElement();
            return xmlReader.ReadContentAsChars(chars, index, count);
        }

        public override DateTime ReadContentAsDateTime()
        {
            AdvanceToContentOnElement();
            return xmlReader.ReadContentAsDateTime();
        }

        public override decimal ReadContentAsDecimal()
        {
            AdvanceToContentOnElement();
            return xmlReader.ReadContentAsDecimal();
        }

        public override double ReadContentAsDouble()
        {
            AdvanceToContentOnElement();
            return xmlReader.ReadContentAsDouble();
        }

        public override int ReadContentAsInt()
        {
            AdvanceToContentOnElement();
            return xmlReader.ReadContentAsInt();
        }

        public override long ReadContentAsLong()
        {
            AdvanceToContentOnElement();
            return xmlReader.ReadContentAsLong();
        }
#if NO
        public override ICollection ReadContentAsList()
        {
            AdvanceToContentOnElement();
            return xmlReader.ReadContentAsList();
        }
#endif
        public override object ReadContentAsObject()
        {
            AdvanceToContentOnElement();
            return xmlReader.ReadContentAsObject();
        }

        public override float ReadContentAsFloat()
        {
            AdvanceToContentOnElement();
            return xmlReader.ReadContentAsFloat();
        }

        public override string ReadContentAsString()
        {
            AdvanceToContentOnElement();
            return xmlReader.ReadContentAsString();
        }

        public override string ReadInnerXml()
        {
            return xmlReader.ReadInnerXml();
        }

        public override string ReadOuterXml()
        {
            return xmlReader.ReadOuterXml();
        }

        public override ReadState ReadState
        {
            get
            {
                if (xmlReader.ReadState != ReadState.Interactive && infosetReader != null)
                    return infosetReader.ReadState;

                return xmlReader.ReadState;
            }
        }

        public override int ReadValueChunk(char[] buffer, int index, int count)
        {
            return xmlReader.ReadValueChunk(buffer, index, count);
        }

        public override void ResolveEntity()
        {
            xmlReader.ResolveEntity();
        }

        public override XmlReaderSettings Settings
        {
            get
            {
                return xmlReader.Settings;
            }
        }

        public override void Skip()
        {
            xmlReader.Skip();
        }

        public override string this[int index]
        {
            get
            {
                return xmlReader[index];
            }
        }

        public override string this[string name]
        {
            get
            {
                return xmlReader[name];
            }
        }

        public override string this[string name, string ns]
        {
            get
            {
                return xmlReader[name, ns];
            }
        }

        public override string Value
        {
            get
            {
                return xmlReader.Value;
            }
        }

        public override Type ValueType
        {
            get
            {
                return xmlReader.ValueType;
            }
        }

        public override string XmlLang
        {
            get
            {
                return xmlReader.XmlLang;
            }
        }

        public override XmlSpace XmlSpace
        {
            get
            {
                return xmlReader.XmlSpace;
            }
        }

        public bool HasLineInfo()
        {
            if (xmlReader.ReadState == ReadState.Closed)
                return false;

            IXmlLineInfo lineInfo = xmlReader as IXmlLineInfo;
            if (lineInfo == null)
                return false;
            return lineInfo.HasLineInfo();
        }

        public int LineNumber
        {
            get
            {
                if (xmlReader.ReadState == ReadState.Closed)
                    return 0;

                IXmlLineInfo lineInfo = xmlReader as IXmlLineInfo;
                if (lineInfo == null)
                    return 0;
                return lineInfo.LineNumber;
            }
        }

        public int LinePosition
        {
            get
            {
                if (xmlReader.ReadState == ReadState.Closed)
                    return 0;

                IXmlLineInfo lineInfo = xmlReader as IXmlLineInfo;
                if (lineInfo == null)
                    return 0;
                return lineInfo.LinePosition;
            }
        }

        internal class MimePart
        {
            Stream stream;
            MimeHeaders headers;
            byte[] buffer;
            bool isReferencedFromInfoset;

            internal MimePart(Stream stream, MimeHeaders headers)
            {
                this.stream = stream;
                this.headers = headers;
            }

            internal Stream Stream
            {
                get { return stream; }
            }

            internal MimeHeaders Headers
            {
                get { return headers; }
            }

            internal bool ReferencedFromInfoset
            {
                get { return isReferencedFromInfoset; }
                set { isReferencedFromInfoset = value; }
            }

            internal long Length
            {
                get { return stream.CanSeek ? stream.Length : 0; }
            }

            internal byte[] GetBuffer(int maxBuffer, ref int remaining)
            {
                if (buffer == null)
                {
                    MemoryStream bufferedStream = stream.CanSeek ? new MemoryStream((int)stream.Length) : new MemoryStream();
                    int size = 256;
                    byte[] bytes = new byte[size];

                    int read = 0;

                    do
                    {
                        read = stream.Read(bytes, 0, size);
                        XmlSwaReader.DecrementBufferQuota(maxBuffer, ref remaining, read);
                        if (read > 0)
                            bufferedStream.Write(bytes, 0, read);
                    }
                    while (read > 0);

                    bufferedStream.Seek(0, SeekOrigin.Begin);
                    buffer = bufferedStream.GetBuffer();
                    stream = bufferedStream;
                }
                return buffer;
            }

            internal void Release(int maxBuffer, ref int remaining)
            {
                remaining += (int)this.Length;
                this.headers.Release(ref remaining);
            }
        }

        internal class XopIncludeReader : XmlDictionaryReader, IXmlLineInfo
        {
            int chunkSize = 4096;  // Just a default.  Serves as a max chunk size.
            int bytesRemaining;

            MimePart part;
            ReadState readState;
            XmlDictionaryReader parentReader;
            string stringValue;
            int stringOffset;
            XmlNodeType nodeType;
            MemoryStream binHexStream;
            byte[] valueBuffer;
            int valueOffset;
            int valueCount;
            bool finishedStream;

            public XopIncludeReader(MimePart part, XmlDictionaryReader reader)
            {
                if (part == null)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("part");
                if (reader == null)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("reader");

                this.part = part;
                this.parentReader = reader;
                this.readState = ReadState.Initial;
                this.nodeType = XmlNodeType.None;
                this.chunkSize = Math.Min(reader.Quotas.MaxBytesPerRead, chunkSize);
                this.bytesRemaining = this.chunkSize;
                this.finishedStream = false;
            }

            public override XmlDictionaryReaderQuotas Quotas
            {
                get
                {
                    return this.parentReader.Quotas;
                }
            }

            public override XmlNodeType NodeType
            {
                get
                {
                    return (readState == ReadState.Interactive) ? nodeType : parentReader.NodeType;
                }
            }

            public override bool Read()
            {
                bool retVal = true;
                switch (readState)
                {
                    case ReadState.Initial:
                        readState = ReadState.Interactive;
                        nodeType = XmlNodeType.Text;
                        break;
                    case ReadState.Interactive:
                        if (this.finishedStream || (this.bytesRemaining == this.chunkSize && this.stringValue == null))
                        {
                            readState = ReadState.EndOfFile;
                            nodeType = XmlNodeType.EndElement;
                        }
                        else
                        {
                            this.bytesRemaining = this.chunkSize;
                        }
                        break;
                    case ReadState.EndOfFile:
                        nodeType = XmlNodeType.None;
                        retVal = false;
                        break;
                }
                this.stringValue = null;
                this.binHexStream = null;
                this.valueOffset = 0;
                this.valueCount = 0;
                this.stringOffset = 0;
                CloseStreams();
                return retVal;
            }

            public override int ReadValueAsBase64(byte[] buffer, int offset, int count)
            {
                if (buffer == null)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("buffer");

                if (offset < 0)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException("offset", "SR.ValueMustBeNonNegative"));
                if (offset > buffer.Length)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException("offset", string.Format("SR.OffsetExceedsBufferSize, {0}", buffer.Length)));
                if (count < 0)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException("count", "SR.ValueMustBeNonNegative"));
                if (count > buffer.Length - offset)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException("count", string.Format("SR.SizeExceedsRemainingBufferSpace, {0}", buffer.Length - offset)));

                if (this.stringValue != null)
                {
                    count = Math.Min(count, this.valueCount);
                    if (count > 0)
                    {
                        Buffer.BlockCopy(this.valueBuffer, this.valueOffset, buffer, offset, count);
                        this.valueOffset += count;
                        this.valueCount -= count;
                    }
                    return count;
                }

                if (this.bytesRemaining < count)
                    count = this.bytesRemaining;

                int read = 0;
                if (readState == ReadState.Interactive)
                {
                    while (read < count)
                    {
                        int actual = part.Stream.Read(buffer, offset + read, count - read);
                        if (actual == 0)
                        {
                            this.finishedStream = true;
                            break;
                        }
                        read += actual;
                    }
                }
                this.bytesRemaining -= read;
                return read;
            }

            public override int ReadContentAsBase64(byte[] buffer, int offset, int count)
            {
                if (buffer == null)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("buffer");

                if (offset < 0)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException("offset", "SR.ValueMustBeNonNegative"));
                if (offset > buffer.Length)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException("offset", string.Format("SR.OffsetExceedsBufferSize, {0}", buffer.Length)));
                if (count < 0)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException("count", "SR.ValueMustBeNonNegative"));
                if (count > buffer.Length - offset)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException("count", string.Format("SR.SizeExceedsRemainingBufferSpace, {0}", buffer.Length - offset)));

                if (this.valueCount > 0)
                {
                    count = Math.Min(count, this.valueCount);
                    Buffer.BlockCopy(this.valueBuffer, this.valueOffset, buffer, offset, count);
                    this.valueOffset += count;
                    this.valueCount -= count;
                    return count;
                }

                if (this.chunkSize < count)
                    count = this.chunkSize;

                int read = 0;
                if (readState == ReadState.Interactive)
                {
                    while (read < count)
                    {
                        int actual = part.Stream.Read(buffer, offset + read, count - read);
                        if (actual == 0)
                        {
                            this.finishedStream = true;
                            if (!Read())
                                break;
                        }
                        read += actual;
                    }
                }
                this.bytesRemaining = this.chunkSize;
                return read;
            }

            public override int ReadContentAsBinHex(byte[] buffer, int offset, int count)
            {
                if (buffer == null)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("buffer");

                if (offset < 0)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException("offset", "SR.ValueMustBeNonNegative"));
                if (offset > buffer.Length)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException("offset", string.Format("SR.OffsetExceedsBufferSize, {0}", buffer.Length)));
                if (count < 0)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException("count", "SR.ValueMustBeNonNegative"));
                if (count > buffer.Length - offset)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException("count", string.Format("SR.SizeExceedsRemainingBufferSpace, {0}", buffer.Length - offset)));

                if (this.chunkSize < count)
                    count = this.chunkSize;

                int read = 0;
                int consumed = 0;
                while (read < count)
                {
                    if (binHexStream == null)
                    {
                        try
                        {
                            binHexStream = new MemoryStream(new BinHexEncoding().GetBytes(this.Value));
                        }
                        catch (FormatException e) // Wrap format exceptions from decoding document contents
                        {
                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(e.Message, e));
                        }
                    }

                    int actual = binHexStream.Read(buffer, offset + read, count - read);
                    if (actual == 0)
                    {
                        this.finishedStream = true;
                        if (!Read())
                            break;

                        consumed = 0;
                    }

                    read += actual;
                    consumed += actual;
                }

                // Trim off the consumed chars
                if (this.stringValue != null && consumed > 0)
                {
                    this.stringValue = this.stringValue.Substring(consumed * 2);
                    this.stringOffset = Math.Max(0, this.stringOffset - consumed * 2);

                    this.bytesRemaining = this.chunkSize;
                }
                return read;
            }

            public override int ReadValueChunk(char[] chars, int offset, int count)
            {
                if (chars == null)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("chars");

                if (offset < 0)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException("offset", "SR.ValueMustBeNonNegative"));
                if (offset > chars.Length)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException("offset", string.Format("SR.OffsetExceedsBufferSize, {0}", chars.Length)));
                if (count < 0)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException("count", "SR.ValueMustBeNonNegative"));
                if (count > chars.Length - offset)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException("count", string.Format("SR.SizeExceedsRemainingBufferSpace, {0}", chars.Length - offset)));

                if (readState != ReadState.Interactive)
                    return 0;

                // Copy characters from the Value property
                string str = this.Value;
                count = Math.Min(stringValue.Length - stringOffset, count);
                if (count > 0)
                {
                    stringValue.CopyTo(stringOffset, chars, offset, count);
                    stringOffset += count;
                }
                return count;
            }

            public override string Value
            {
                get
                {
                    if (readState != ReadState.Interactive)
                        return String.Empty;

                    if (stringValue == null)
                    {
                        // Compute the bytes to read
                        int byteCount = this.bytesRemaining;
                        byteCount -= byteCount % 3;

                        // Handle trailing bytes
                        if (this.valueCount > 0 && this.valueOffset > 0)
                        {
                            Buffer.BlockCopy(this.valueBuffer, this.valueOffset, this.valueBuffer, 0, this.valueCount);
                            this.valueOffset = 0;
                        }
                        byteCount -= this.valueCount;

                        // Resize buffer if needed
                        if (this.valueBuffer == null)
                        {
                            this.valueBuffer = new byte[byteCount];
                        }
                        else if (this.valueBuffer.Length < byteCount)
                        {
                            Array.Resize(ref this.valueBuffer, byteCount);
                        }
                        byte[] buffer = this.valueBuffer;

                        // Fill up the buffer
                        int offset = 0;
                        int read = 0;
                        while (byteCount > 0)
                        {
                            read = part.Stream.Read(buffer, offset, byteCount);
                            if (read == 0)
                            {
                                this.finishedStream = true;
                                break;
                            }

                            this.bytesRemaining -= read;
                            this.valueCount += read;
                            byteCount -= read;
                            offset += read;
                        }

                        // Convert the bytes
                        this.stringValue = Convert.ToBase64String(buffer, 0, this.valueCount);
                    }
                    return this.stringValue;
                }
            }

            public override string ReadContentAsString()
            {
                int stringContentQuota = this.Quotas.MaxStringContentLength;
                StringBuilder sb = new StringBuilder();
                do
                {
                    string val = this.Value;
                    if (val.Length > stringContentQuota)
                        XmlExceptionHelper.ThrowMaxStringContentLengthExceeded(this, Quotas.MaxStringContentLength);
                    stringContentQuota -= val.Length;
                    sb.Append(val);
                } while (Read());
                return sb.ToString();
            }

            public override int AttributeCount
            {
                get { return 0; }
            }

            public override string BaseURI
            {
                get { return parentReader.BaseURI; }
            }

            public override bool CanReadBinaryContent
            {
                get { return true; }
            }

            public override bool CanReadValueChunk
            {
                get { return true; }
            }

            public override bool CanResolveEntity
            {
                get { return parentReader.CanResolveEntity; }
            }

            public override void Close()
            {
                CloseStreams();
                readState = ReadState.Closed;
            }

            void CloseStreams()
            {
                if (binHexStream != null)
                {
                    binHexStream.Close();
                    binHexStream = null;
                }
            }

            public override int Depth
            {
                get
                {
                    return (readState == ReadState.Interactive) ? parentReader.Depth + 1 : parentReader.Depth;
                }
            }

            public override bool EOF
            {
                get { return readState == ReadState.EndOfFile; }
            }

            public override string GetAttribute(int index)
            {
                return null;
            }

            public override string GetAttribute(string name)
            {
                return null;
            }

            public override string GetAttribute(string name, string ns)
            {
                return null;
            }

            public override string GetAttribute(XmlDictionaryString localName, XmlDictionaryString ns)
            {
                return null;
            }

            public override bool HasAttributes
            {
                get { return false; }
            }

            public override bool HasValue
            {
                get { return readState == ReadState.Interactive; }
            }

            public override bool IsDefault
            {
                get { return false; }
            }

            public override bool IsEmptyElement
            {
                get { return false; }
            }

            public override bool IsLocalName(string localName)
            {
                return false;
            }

            public override bool IsLocalName(XmlDictionaryString localName)
            {
                return false;
            }

            public override bool IsNamespaceUri(string ns)
            {
                return false;
            }

            public override bool IsNamespaceUri(XmlDictionaryString ns)
            {
                return false;
            }

            public override bool IsStartElement()
            {
                return false;
            }

            public override bool IsStartElement(string localName)
            {
                return false;
            }

            public override bool IsStartElement(string localName, string ns)
            {
                return false;
            }

            public override bool IsStartElement(XmlDictionaryString localName, XmlDictionaryString ns)
            {
                return false;
            }
#if NO
            public override bool IsStartSubsetElement()
            {
                return false;
            }
#endif
            public override string LocalName
            {
                get
                {
                    return (readState == ReadState.Interactive) ? String.Empty : parentReader.LocalName;
                }
            }

            public override string LookupNamespace(string ns)
            {
                return parentReader.LookupNamespace(ns);
            }

            public override void MoveToAttribute(int index)
            {
            }

            public override bool MoveToAttribute(string name)
            {
                return false;
            }

            public override bool MoveToAttribute(string name, string ns)
            {
                return false;
            }

            public override bool MoveToElement()
            {
                return false;
            }

            public override bool MoveToFirstAttribute()
            {
                return false;
            }

            public override bool MoveToNextAttribute()
            {
                return false;
            }

            public override string Name
            {
                get
                {
                    return (readState == ReadState.Interactive) ? String.Empty : parentReader.Name;
                }
            }

            public override string NamespaceURI
            {
                get
                {
                    return (readState == ReadState.Interactive) ? String.Empty : parentReader.NamespaceURI;
                }
            }

            public override XmlNameTable NameTable
            {
                get { return parentReader.NameTable; }
            }

            public override string Prefix
            {
                get
                {
                    return (readState == ReadState.Interactive) ? String.Empty : parentReader.Prefix;
                }
            }

            public override char QuoteChar
            {
                get { return parentReader.QuoteChar; }
            }

            public override bool ReadAttributeValue()
            {
                return false;
            }

            public override string ReadInnerXml()
            {
                return ReadContentAsString();
            }

            public override string ReadOuterXml()
            {
                return ReadContentAsString();
            }

            public override ReadState ReadState
            {
                get { return readState; }
            }

            public override void ResolveEntity()
            {
            }

            public override XmlReaderSettings Settings
            {
                get { return parentReader.Settings; }
            }

            public override void Skip()
            {
                Read();
            }

            public override string this[int index]
            {
                get { return null; }
            }

            public override string this[string name]
            {
                get { return null; }
            }

            public override string this[string name, string ns]
            {
                get { return null; }
            }

            public override string XmlLang
            {
                get { return parentReader.XmlLang; }
            }

            public override XmlSpace XmlSpace
            {
                get { return parentReader.XmlSpace; }
            }

            public override Type ValueType
            {
                get
                {
                    return (readState == ReadState.Interactive) ? typeof(byte[]) : parentReader.ValueType;
                }
            }

            bool IXmlLineInfo.HasLineInfo()
            {
                return ((IXmlLineInfo)parentReader).HasLineInfo();
            }

            int IXmlLineInfo.LineNumber
            {
                get
                {
                    return ((IXmlLineInfo)parentReader).LineNumber;
                }
            }

            int IXmlLineInfo.LinePosition
            {
                get
                {
                    return ((IXmlLineInfo)parentReader).LinePosition;
                }
            }
        }
    }

}

