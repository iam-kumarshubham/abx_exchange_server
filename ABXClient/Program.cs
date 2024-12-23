using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Collections.Generic;
using System.Linq;

class ABXClient
{
    const string ServerHost = "localhost";
    const int ServerPort = 3000;

    static void Main(string[] args)
    {
        try
        {
            // Connect to server
            using TcpClient client = new TcpClient(ServerHost, ServerPort);
            using NetworkStream stream = client.GetStream();

            // Request to stream all packets (callType = 1)
            byte[] request = new byte[] { 1, 0 }; // callType=1, resendSeq=0
            stream.Write(request, 0, request.Length);

            // Read response
            List<Packet> packets = new List<Packet>();
            byte[] buffer = new byte[17]; // Packet size: 17 bytes

            while (stream.Read(buffer, 0, buffer.Length) > 0)
            {
                packets.Add(ParsePacket(buffer));
            }

            // Identify missing sequences
            var allSequences = packets.Select(p => p.PacketSequence).OrderBy(seq => seq);
            var missingSequences = Enumerable.Range(allSequences.First(), allSequences.Last() - allSequences.First() + 1)
                                             .Except(allSequences)
                                             .ToList();

            // Request missing packets
            foreach (int seq in missingSequences)
            {
                byte[] resendRequest = new byte[] { 2, (byte)seq }; // callType=2, resendSeq=seq
                stream.Write(resendRequest, 0, resendRequest.Length);

                stream.Read(buffer, 0, buffer.Length);
                packets.Add(ParsePacket(buffer));
            }

            // Serialize to JSON
            string jsonOutput = JsonSerializer.Serialize(packets.OrderBy(p => p.PacketSequence));
            File.WriteAllText("output.json", jsonOutput);

            Console.WriteLine("Data saved to output.json");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }

    static Packet ParsePacket(byte[] buffer)
    {
        string symbol = Encoding.ASCII.GetString(buffer, 0, 4);
        char buySellIndicator = (char)buffer[4];
        int quantity = BitConverter.ToInt32(buffer.Skip(5).Take(4).Reverse().ToArray(), 0);
        int price = BitConverter.ToInt32(buffer.Skip(9).Take(4).Reverse().ToArray(), 0);
        int packetSequence = BitConverter.ToInt32(buffer.Skip(13).Take(4).Reverse().ToArray(), 0);

        return new Packet
        {
            Symbol = symbol,
            BuySellIndicator = buySellIndicator,
            Quantity = quantity,
            Price = price,
            PacketSequence = packetSequence
        };
    }

    class Packet
    {
        public string Symbol { get; set; }
        public char BuySellIndicator { get; set; }
        public int Quantity { get; set; }
        public int Price { get; set; }
        public int PacketSequence { get; set; }
    }
}
