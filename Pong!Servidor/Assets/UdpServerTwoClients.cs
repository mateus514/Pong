using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using System.Globalization;

public class UdpServerTwoClients : MonoBehaviour
{
    UdpClient server;
    IPEndPoint anyEP;
    Thread receiveThread;
    Dictionary<string, int> clientIds = new Dictionary<string, int>();
    int nextId = 1;

    // --- Bola ---
    Vector2 ballPos = Vector2.zero;
    Vector2 ballDir;
    float ballSpeed = 5f;
    GameObject ballObject;

    void Start()
    {
        server = new UdpClient(5001);
        anyEP = new IPEndPoint(IPAddress.Any, 0);

        receiveThread = new Thread(ReceiveData);
        receiveThread.IsBackground = true;
        receiveThread.Start();

        Debug.Log("Servidor iniciado na porta 5001");

        // Direção aleatória da bola
        bool isRight = Random.value >= 0.5f;
        float xVelocity = isRight ? 1f : -1f;
        float yVelocity = Random.Range(-1f, 1f);
        ballDir = new Vector2(xVelocity, yVelocity).normalized;

        // Cria a bola visualmente (opcional)
        CreateBallObject();
    }

    void Update()
    {
        // Movimento da bola
        ballPos += ballDir * ballSpeed * Time.deltaTime;

        // Rebater nas bordas (campo de jogo entre x = ±8 e y = ±4)
        if (ballPos.y > 4f || ballPos.y < -4f)
        {
            ballDir.y *= -1;
        }

        if (ballPos.x > 8f || ballPos.x < -8f)
        {
            ballDir.x *= -1;
        }

        // Atualiza posição da bola visualmente (no editor do Unity)
        if (ballObject != null)
        {
            ballObject.transform.position = new Vector3(ballPos.x, ballPos.y, 0f);
        }

        // Envia posição da bola para todos os clientes
        BroadcastBallPosition();
    }

    void ReceiveData()
    {
        while (true)
        {
            try
            {
                byte[] data = server.Receive(ref anyEP);
                string msg = Encoding.UTF8.GetString(data);
                string key = anyEP.Address + ":" + anyEP.Port;

                if (!clientIds.ContainsKey(key))
                {
                    clientIds[key] = nextId++;
                    string assignMsg = "ASSIGN:" + clientIds[key];
                    server.Send(Encoding.UTF8.GetBytes(assignMsg), assignMsg.Length, anyEP);
                    Debug.Log($"Cliente novo conectado: {key} => ID {clientIds[key]}");
                }

                int id = clientIds[key];

                if (msg.StartsWith("POS:"))
                {
                    string coords = msg.Substring(4);
                    string broadcast = $"POS:{id};{coords}";
                    byte[] bdata = Encoding.UTF8.GetBytes(broadcast);

                    foreach (var kvp in clientIds)
                    {
                        var parts = kvp.Key.Split(':');
                        IPEndPoint ep = new IPEndPoint(IPAddress.Parse(parts[0]), int.Parse(parts[1]));
                        server.Send(bdata, bdata.Length, ep);
                    }
                }
            }
            catch (SocketException ex)
            {
                Debug.LogError("Erro ao receber dados: " + ex.Message);
            }
        }
    }

    void BroadcastBallPosition()
    {
        string msg = $"BALL:{ballPos.x.ToString("F2", CultureInfo.InvariantCulture)};" +
                     $"{ballPos.y.ToString("F2", CultureInfo.InvariantCulture)}";
        byte[] data = Encoding.UTF8.GetBytes(msg);

        foreach (var kvp in clientIds)
        {
            var parts = kvp.Key.Split(':');
            IPEndPoint ep = new IPEndPoint(IPAddress.Parse(parts[0]), int.Parse(parts[1]));
            server.Send(data, data.Length, ep);
        }
    }

    void CreateBallObject()
    {
        ballObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        ballObject.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
        ballObject.transform.position = new Vector3(ballPos.x, ballPos.y, 0f);
        ballObject.name = "Bola";
        Renderer renderer = ballObject.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = Color.yellow;
        }
    }

    void OnApplicationQuit()
    {
        receiveThread?.Abort();
        server?.Close();
    }
}
