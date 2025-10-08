using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Concurrent;
using UnityEngine;

public class ClientLocal : MonoBehaviour
{
    public Rigidbody2D localCube;
    public Rigidbody2D remoteCube;
    public Rigidbody2D ball;
    public string serverIP = "127.0.0.1";
    public int serverPort = 7777;
    public int id = 1;

    private UdpClient udpClient;
    private Thread receiveThread;
    private IPEndPoint remoteEndPoint;
    private ConcurrentQueue<Action> mainThreadActions = new ConcurrentQueue<Action>();

    void Start()
    {
        udpClient = new UdpClient();
        remoteEndPoint = new IPEndPoint(IPAddress.Parse(serverIP), serverPort);
        receiveThread = new Thread(new ThreadStart(ReceiveData));
        receiveThread.IsBackground = true;
        receiveThread.Start();

        SendMessageToServer("ID:" + id);
    }

    void Update()
    {
        float moveY = Input.GetAxisRaw("Vertical");
        Vector2 newPos = localCube.position + new Vector2(0, moveY) * 8f * Time.deltaTime;
        localCube.MovePosition(newPos);

        string message = $"POS:{id}:{localCube.position.x}:{localCube.position.y}";
        SendMessageToServer(message);

        while (mainThreadActions.TryDequeue(out var action))
            action.Invoke();
    }

    void ReceiveData()
    {
        IPEndPoint anyIP = new IPEndPoint(IPAddress.Any, 0);
        while (true)
        {
            try
            {
                byte[] data = udpClient.Receive(ref anyIP);
                string text = Encoding.UTF8.GetString(data);
                HandleServerMessage(text);
            }
            catch (Exception e)
            {
                Debug.LogError(e.Message);
            }
        }
    }

    void HandleServerMessage(string msg)
    {
        if (msg.StartsWith("POS:BALL:"))
        {
            string[] parts = msg.Split(':');
            float x = float.Parse(parts[2]);
            float y = float.Parse(parts[3]);

            mainThreadActions.Enqueue(() => ball.MovePosition(new Vector2(x, y)));
        }
        else if (msg.StartsWith("POS:"))
        {
            string[] parts = msg.Split(':');
            int otherId = int.Parse(parts[1]);
            float x = float.Parse(parts[2]);
            float y = float.Parse(parts[3]);

            if (otherId != id)
                mainThreadActions.Enqueue(() => remoteCube.MovePosition(new Vector2(x, y)));
        }
        else if (msg.StartsWith("SCORE:"))
        {
            Debug.Log(msg);
        }
    }

    void SendMessageToServer(string message)
    {
        byte[] data = Encoding.UTF8.GetBytes(message);
        udpClient.Send(data, data.Length, remoteEndPoint);
    }

    private void OnApplicationQuit()
    {
        udpClient?.Close();
        receiveThread?.Abort();
    }
}
