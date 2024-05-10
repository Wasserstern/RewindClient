using System.Net.Sockets;
using System.Threading;
using UnityEngine;

public class ClientInformation{
    public string username;
    public TcpClient client;
    public NetworkStream stream;
    public Thread thread;

    public Vector3 currentPosition;
    public ClientInformation(TcpClient client, NetworkStream stream, Thread thread, string username){
        this.client = client;
        this.stream = stream;
        this.thread = thread;
        this.username = username;
        
    }
}