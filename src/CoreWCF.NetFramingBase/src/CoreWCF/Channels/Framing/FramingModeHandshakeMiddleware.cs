﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Configuration;
using Microsoft.Extensions.Hosting;

namespace CoreWCF.Channels.Framing
{
    internal class FramingModeHandshakeMiddleware
    {
        private readonly HandshakeDelegate _next;
        private readonly IApplicationLifetime _appLifetime;

        public FramingModeHandshakeMiddleware(HandshakeDelegate next, IApplicationLifetime appLifetime)
        {
            _next = next;
            _appLifetime = appLifetime;
        }

        public async Task OnConnectedAsync(FramingConnection connection)
        {
            // If the remote client aborts or the connection, AspNetCore throws an exceptions which
            // bubbles all the way up to here and causes a critical level event to be logged. This is a normal thing
            // to happen so just swallowing the exception to suppress the critical log output.
            try
            {
                await OnConnectedCoreAsync(connection);
            }
            catch (Microsoft.AspNetCore.Connections.ConnectionAbortedException) { }
            catch (Microsoft.AspNetCore.Connections.ConnectionResetException) { }
            catch (CoreWCF.Security.SecurityNegotiationException) { } // TODO: Work out where this needs to be caught
            catch (System.IO.IOException) { } // TODO: Work out where this needs to be caught
            catch (CommunicationException) { } // TODO: Work out how this is logged in WCF and replicate
        }

        public async Task OnConnectedCoreAsync(FramingConnection connection)
        {
            using (_appLifetime.ApplicationStopped.Register(() =>
             {
                 connection.RawTransport.Input.Complete();
                 connection.RawTransport.Output.Complete();
             }))
            {
                IConnectionReuseHandler reuseHandler = null;
                do
                {
                    System.IO.Pipelines.PipeReader inputPipe = connection.Input;
                    var modeDecoder = new ServerModeDecoder(connection.Logger);
                    try
                    {
                        if (!await modeDecoder.ReadModeAsync(inputPipe, connection.ChannelInitializationCancellationToken))
                        {
                            break; // Input pipe closed
                        }
                    }
                    catch (CommunicationException e)
                    {
                        // see if we need to send back a framing fault
                        if (FramingEncodingString.TryGetFaultString(e, out string framingFault))
                        {
                            await connection.SendFaultAsync(
                                framingFault,
                                ConnectionOrientedTransportDefaults.MaxViaSize + ConnectionOrientedTransportDefaults.MaxContentTypeSize,
                                connection.ChannelInitializationCancellationToken);
                        }

                        return; // Completing the returned Task causes the connection to be closed if needed and cleans everything up.
                    }
                    catch (OperationCanceledException oce)
                    {
                        // Need to Abort (RST) the connection as aspnetcore on .NET Framework doesn't correctly close the socket
                        // In the case of connection establishment timeout, this is a change in behavior as WCF will close (FIN) the socket.
                        connection.Abort(oce);
                        throw;
                    }

                    connection.FramingMode = modeDecoder.Mode;
                    await _next(connection);

                    // Unwrap the connection.
                    // TODO: Investigate calling Dispose on the wrapping stream to improve cleanup. nb: .NET Framework does not call Dispose.
                    connection.Transport = connection.RawTransport;

                    // connection.ServiceDispatcher is null until later middleware layers are executed.
                    if (connection.ServiceDispatcher == null)
                    {
                        await connection.SendFaultAsync(
                            FramingEncodingString.EndpointNotFoundFault,
                            ConnectionOrientedTransportDefaults.MaxViaSize + ConnectionOrientedTransportDefaults.MaxContentTypeSize,
                            connection.ChannelInitializationCancellationToken);
                        return;
                    }

                    if (reuseHandler == null)
                    {
                        var listenOptions = connection.ConnectionFeatures.Get<NetFramingListenOptions>();
                        reuseHandler = listenOptions.ConnectionReuseHandler;
                    }
                } while (await reuseHandler.ReuseConnectionAsync(connection, _appLifetime.ApplicationStopping));

                // On .NET Framework, with the way that AspNetCore closes a connection, it sometimes doesn't send the
                // final bytes if those bytes haven't been sent yet. Delaying completeing the connection to compensate.
                await Task.Delay(5);
            }

            // AspNetCore 2.1 doesn't close the connection. 2.2+ does so these lines can eventually be removed.
            connection.RawTransport.Input.Complete();
            connection.RawTransport.Output.Complete();
        }
    }
}
