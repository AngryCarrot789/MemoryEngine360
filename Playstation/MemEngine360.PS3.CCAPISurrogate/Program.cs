// 
// Copyright (c) 2024-2025 REghZy
// 
// This file is part of MemoryEngine360.
// 
// MemoryEngine360 is free software; you can redistribute it and/or
// modify it under the terms of the GNU General Public License
// as published by the Free Software Foundation; either
// version 3.0 of the License, or (at your option) any later version.
// 
// MemoryEngine360 is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
// Lesser General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with MemoryEngine360. If not, see <https://www.gnu.org/licenses/>.
// 

using System.Net;
using System.Net.Sockets;

namespace MemEngine360.PS3.CCAPISurrogate;

public class Program {
    private Program() {
    }

    public static void Main(string[] args) {
        if (args.Length < 1)
            throw new Exception("No args. Need a port number");

        if (!int.TryParse(args[0], out int port) || port < 0 || port > 65535)
            throw new Exception("Invalid port number: " + args[0]);

        RunSingleClient(port);
    }

    private static void RunSharedServer(int port) {
        TcpListener server = new TcpListener(IPAddress.Loopback, port);
        server.Start(4);

        try {
            Console.WriteLine($"Running CCAPI surrogate at {IPAddress.Loopback} on port {port}");

            while (true) {
                TcpClient client = server.AcceptTcpClient();
                Console.WriteLine($"Accepted client {client.Client}");

                RpcClient.RunInThread(client);
            }
        }
        finally {
            try {
                server.Stop();
            }
            catch {
                // ignored
            }
        }
    }

    private static void RunSingleClient(int port) {
        TcpListener server = new TcpListener(IPAddress.Loopback, port);
        server.Start(4);

        TcpClient client;
        try {
            Console.WriteLine($"Running CCAPI surrogate at {IPAddress.Loopback} on port {port}");

            client = server.AcceptTcpClient();
        }
        finally {
            try {
                server.Stop();
            }
            catch {
                // ignored
            }
        }

        Console.WriteLine($"Accepted client {client.Client}");

        UnmanagedCCAPI lib = UnmanagedCCAPI.LoadLibrary("CCAPI.dll");
        new RpcClient(client, new ApiHelper(lib)).Run();
    }
}