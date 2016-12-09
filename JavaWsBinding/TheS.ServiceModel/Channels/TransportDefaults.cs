using System;
using System.Text;
using System.ServiceModel.Channels;
using System.Xml;

namespace TheS.ServiceModel.Channels
{

    static class EncoderDefaults
    {
        internal const int MaxReadPoolSize = 64;
        internal const int MaxWritePoolSize = 16;

        internal const int MaxDepth = 32;
        internal const int MaxStringContentLength = 8192;
        internal const int MaxArrayLength = 16384;
        internal const int MaxBytesPerRead = 4096;
        internal const int MaxNameTableCharCount = 16384;

        internal const int BufferedReadDefaultMaxDepth = 128;
        internal const int BufferedReadDefaultMaxStringContentLength = Int32.MaxValue;
        internal const int BufferedReadDefaultMaxArrayLength = Int32.MaxValue;
        internal const int BufferedReadDefaultMaxBytesPerRead = Int32.MaxValue;
        internal const int BufferedReadDefaultMaxNameTableCharCount = Int32.MaxValue;

        internal const CompressionFormat DefaultCompressionFormat = CompressionFormat.None;

        internal static readonly XmlDictionaryReaderQuotas ReaderQuotas = new XmlDictionaryReaderQuotas();

        internal static bool IsDefaultReaderQuotas(XmlDictionaryReaderQuotas quotas)
        {
            return quotas.ModifiedQuotas == 0x00;
        }
    }

    static class TextEncoderDefaults
    {
        internal static readonly Encoding Encoding = Encoding.GetEncoding(TextEncoderDefaults.EncodingString, new EncoderExceptionFallback(), new DecoderExceptionFallback());
        internal const string EncodingString = "utf-8";
        internal static readonly Encoding[] SupportedEncodings = new Encoding[] { Encoding.UTF8, Encoding.Unicode, Encoding.BigEndianUnicode };
        internal const string MessageVersionString = TheS.ServiceModel.Configuration.ConfigurationStrings.Soap11;
        // Somkiet's feedback, which tell us this default is not work.
        //internal const string MessageVersionString = TheS.ServiceModel.Configuration.ConfigurationStrings.Soap12WSAddressing10;
        internal static readonly CharSetEncoding[] CharSetEncodings = new CharSetEncoding[]
        {
            new CharSetEncoding("utf-8", Encoding.UTF8),
            new CharSetEncoding("utf-16LE", Encoding.Unicode),
            new CharSetEncoding("utf-16BE", Encoding.BigEndianUnicode),
            new CharSetEncoding("utf-16", null),   // Ignore.  Ambiguous charSet, so autodetect.
            new CharSetEncoding(null, null),       // CharSet omitted, so autodetect.
        };

        internal static void ValidateEncoding(Encoding encoding)
        {
            string charSet = encoding.WebName;
            Encoding[] supportedEncodings = SupportedEncodings;
            for (int i = 0; i < supportedEncodings.Length; i++)
            {
                if (charSet == supportedEncodings[i].WebName)
                    return;
            }
            throw new ArgumentException(string.Format("SR.MessageTextEncodingNotSupported, {0}", charSet), "encoding");
        }

        internal static string EncodingToCharSet(Encoding encoding)
        {
            string webName = encoding.WebName;
            CharSetEncoding[] charSetEncodings = CharSetEncodings;
            for (int i = 0; i < charSetEncodings.Length; i++)
            {
                Encoding enc = charSetEncodings[i].Encoding;
                if (enc == null)
                    continue;

                if (enc.WebName == webName)
                    return charSetEncodings[i].CharSet;
            }
            return null;
        }

        internal static bool TryGetEncoding(string charSet, out Encoding encoding)
        {
            CharSetEncoding[] charSetEncodings = CharSetEncodings;

            // Quick check for exact equality
            for (int i = 0; i < charSetEncodings.Length; i++)
            {
                if (charSetEncodings[i].CharSet == charSet)
                {
                    encoding = charSetEncodings[i].Encoding;
                    return true;
                }
            }

            // Check for case insensative match
            for (int i = 0; i < charSetEncodings.Length; i++)
            {
                string compare = charSetEncodings[i].CharSet;
                if (compare == null)
                    continue;

                if (compare.Equals(charSet, StringComparison.OrdinalIgnoreCase))
                {
                    encoding = charSetEncodings[i].Encoding;
                    return true;
                }
            }

            encoding = null;
            return false;
        }

        internal class CharSetEncoding
        {
            internal string CharSet;
            internal Encoding Encoding;

            internal CharSetEncoding(string charSet, Encoding enc)
            {
                CharSet = charSet;
                Encoding = enc;
            }
        }
    }

    static class MtomEncoderDefaults
    {
        internal const int MaxBufferSize = 65536;
    }
}
