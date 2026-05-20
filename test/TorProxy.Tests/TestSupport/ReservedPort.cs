using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace Nefarius.Utilities.TorProxy.Tests.TestSupport
{
    public class ReservedPort : IDisposable
    {
        private static readonly object Lock = new object();
        private static readonly HashSet<int> ReservedPorts = new HashSet<int>();

        private readonly int _port;
        private bool _disposed;

        private ReservedPort(int port)
        {
            _port = port;
            _disposed = false;
        }

        public int Port
        {
            get
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException("The port is not longer reserved.");
                }

                return _port;
            }
        }

        public static ReservedPort Reserve()
        {
            // Randomise the start offset so that parallel dotnet-test processes (e.g. one
            // per target framework) are extremely unlikely to converge on the same candidate
            // port simultaneously.  The in-process HashSet only prevents intra-process
            // collisions; random starts handle the inter-process case.
            var rng = new Random();
            var start = rng.Next(50001, 60001);
            var port = start;
            var attempts = 0;
            const int MaxAttempts = 10000;

            while (attempts < MaxAttempts)
            {
                attempts++;

                lock (Lock)
                {
                    if (!ReservedPorts.Contains(port) && IsPortFree(port))
                    {
                        ReservedPorts.Add(port);
                        return new ReservedPort(port);
                    }
                }

                port++;
                if (port > 65534)
                {
                    port = 50001;
                }
            }

            throw new InvalidOperationException("Could not reserve a free TCP port after exhausting the search range.");
        }

        private static bool IsPortFree(int port)
        {
            var tcpListener = new TcpListener(IPAddress.Loopback, port);
            try
            {
                tcpListener.Start();
                return true;
            }
            catch (SocketException)
            {
                return false;
            }
            finally
            {
                tcpListener.Stop();
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            lock (Lock)
            {
                ReservedPorts.Remove(_port);
            }
        }
    }
}