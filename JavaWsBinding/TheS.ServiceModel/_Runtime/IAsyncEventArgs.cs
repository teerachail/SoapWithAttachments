// <copyright>
//   Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>

using System;

namespace TheS.Runtime
{
    interface IAsyncEventArgs
    {
        object AsyncState { get; }

        Exception Exception { get; }
    }
}
