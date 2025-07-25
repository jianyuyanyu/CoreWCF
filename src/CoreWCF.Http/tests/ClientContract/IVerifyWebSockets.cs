﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ServiceModel;

namespace ClientContract
{
    [ServiceContract(SessionMode = SessionMode.Required)]
    public interface IVerifyWebSockets
    {
        [OperationContract()]
        bool ValidateWebSocketsUsed();
    }
}
