using System;
using kurome;
using Kurome;
using Action = kurome.Action;

namespace DefaultNamespace;

public class PairingHandler
{
    public enum PairStatus
    {
        NotPaired,
        Requested,
        RequestedByPeer,
        Paired
    }

    private Device device;
    private PairStatus status;
    public delegate void PairStatusDelegate(PairStatus status, Device device);
    public PairingHandler(Device device)
    {
        this.device = device;
    }

    public void PairPacketReceived(Packet packet)
    {
        var wantsPair = packet.Pair == PairEvent.Pair;
        var isPaired = status == PairStatus.Paired;
        if (wantsPair == isPaired)
        {
            Console.WriteLine("Pairing cancelled");
        }

        if (wantsPair)
        {
            status = PairStatus.Requested;
            Console.WriteLine("Pairing requested from " + device.Name + "\n Accept? (y/n)");
            var key = Console.ReadKey().KeyChar;
            Console.WriteLine();
            if (key is 'y' or 'Y')
            {
                AcceptPairing();
            }
            else
            {
                device.SendPacket(action: Action.ActionPair, pair: PairEvent.Unpair);
                Console.WriteLine("Request Packet sent");
            }

        }
    }

    private void AcceptPairing()
    {
        device.AcceptPairing();
        Console.WriteLine("PairingHandler: Pairing accepted");
    }
}