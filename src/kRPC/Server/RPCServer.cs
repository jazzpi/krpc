using System;
using System.IO;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Linq;
using KRPC.Schema.RPC;
using KRPC.Utils;
using Google.ProtocolBuffers;

namespace KRPC.Server
{
    public class RPCServer
    {
        private IServer server;
        double timeout;
        private Dictionary<int,MemoryStream> clientBuffers = new Dictionary<int, MemoryStream>();

        public RPCServer (IServer server, double timeout = 15)
        {
            this.server = server;
            this.timeout = timeout;
            server.OnClientRequestingConnection += CheckHelloMessage;
        }

        public void CheckHelloMessage(object sender, ClientRequestingConnectionArgs args) {
            // Read 8 byte hello message
            //TODO: refactor this
            Logger.WriteLine("Waiting for hello message from client...");
            byte[] buffer = new byte[8];
            int offset = 0;
            for (int i = 0; i < (int)(timeout * 1000) / 50; i++) {
                if (args.Stream.DataAvailable) {
                    offset += args.Stream.Read (buffer, offset, 8-offset);
                    if (offset == 8)
                        break;
                }
                System.Threading.Thread.Sleep(50);
            }

            // Failed to read message in sufficient time - kill connection
            if (offset != 8) {
                Logger.WriteLine("Client connection abandoned. Timed out waiting for hello message.");
                args.Deny ();
                return;
            }

            // Check the hello message is correct
            byte[] expectedBuffer = {0x48,0x45,0x4C,0x4C,0x4F,0xBA,0xDA,0x55};
            if (!buffer.SequenceEqual(expectedBuffer)) {
                string hex = ("0x" + BitConverter.ToString(buffer)).Replace("-", " 0x");
                Logger.WriteLine("Client connection abandoned. Invalid hello message received (" + hex + ")");
                args.Deny ();
            } else {
                Logger.WriteLine("Correct hello message received.");
                args.Allow ();
            }
        }

        public void Start()
        {
            server.Start();
        }

        public void Stop()
        {
            server.Stop();
        }

        public IServer Server {
            get { return server; }
        }

        public bool Running {
            get { return server.Running; }
        }

        /// <summary>
        /// Get an RPC request from the server. Blocks until a request is received.
        /// </summary>
        public Tuple<int,Request> GetRequest ()
        {
            // For each client:
            //   If there is not data to receive from the client, try the next client
            //   Receive as many bytes from client as possible and store them in a memory buffer
            //   Run Request.MergeFrom on the memory buffer
            //   If it reads an uninitialized message => malformed message, try next client
            //   If it fails => partial message received so far, try next client
            //   If it succeeds => return message
            // If no clients return a message, throw NoRequestException
            foreach (int clientId in server.GetConnectedClientIds()) {
                var stream = server.GetClientStream (clientId);

                // If there's no data, continue to next client
                // Note: There may be data in the clients buffer, but that doesn't matter.
                //       That data does not constitute a complete message,
                //       otherwise it would have been parsed on the last iteration.
                if (!stream.DataAvailable)
                    continue;

                // Read as much data as we can from the client into its buffer
                // TODO: Do we need a maximum buffer size?
                if (!clientBuffers.ContainsKey(clientId))
                    clientBuffers[clientId] = new MemoryStream();
                MemoryStream buffer = clientBuffers[clientId];
                buffer.Seek (0,SeekOrigin.End);
                while (stream.DataAvailable) {
                    byte[] tmp = new byte[4096];
                    System.Int32 size = stream.Read(tmp, 0, 4096);
                    buffer.Write(tmp, 0, size);
                }

                var builder = Request.CreateBuilder();
                buffer.Seek (0,SeekOrigin.Begin);
                try
                {
                    Request req = builder.MergeFrom(buffer).BuildPartial();
                    if (req.IsInitialized)
                        return new Tuple<int,Request>(clientId, req);
                    else
                        throw new MalformedRequestException();
                }
                catch (InvalidProtocolBufferException)
                {
                    // Haven't received enough bytes, so continue on
                    // TODO: What about actual malformed messages?
                    // TODO: Should forcibly disconnect the client. Set a maximum buffered data size?
                }
            }
            throw new NoRequestException();
        }

        public void SendResponse(int clientId, Response response)
        {
            var stream = server.GetClientStream(clientId);
            response.WriteTo(stream.GetUnderlyingNetworkStream());
        }
    }
}