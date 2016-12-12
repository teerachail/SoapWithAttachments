// <copyright>
//   Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>

namespace TheS.Xml
{
    class XmlNodeWriterWriteBase64TextArgs
    {
        internal byte[] TrailBuffer { get; set; }

        internal int TrailCount { get; set; }

        internal byte[] Buffer { get; set; }

        internal int Offset { get; set; }

        internal int Count { get; set; }
    }
}
