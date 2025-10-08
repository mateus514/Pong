using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public class ServidorUDP : MonoBehaviour
{
    UdpClient server;
    Thread receiveThread;

    IPEndPoint client1EP;
    IPEndPoint client2EP;

    Vector2 paddle1 = new Vector2(-8f, 0f);
    Vector2 paddle2 = new Vector2(8f, 0f);

    Vector2 ballPos = Vector2.zero;
    Vector2 ballVel = new Vector2(6f, 3f);

    float fieldWidth = 9f;
    float fieldHeight = 5f;

    int score1 = 0;
    int score2 = 0;

    float tickRate = 1f / 60f;
    float accumulator = 0f;

    void Start()
    {
        server = new UdpClient(5001);
        receiveThread = new Thread(ReceiveData);
        receiveThread.IsBackground = true;
        receiveThread.Start();

        Debug.Log("[Servidor] Iniciado na porta 5001");
    }

    void Update()
    {
        accumulator += Time.deltaTime;
        while (accumulator >= tickRate)
        {
            UpdateGame(tickRate);
            accumulator -= tickRate;
        }
    }

    void UpdateGame(float dt)
    {
        ballPos += ballVel * dt;

        // Colisão com bordas
        if (ballPos.y > fieldHeight)
        {
            ballPos.y = fieldHeight;
            ballVel.y *= -1;
        }
        else if (ballPos.y < -fieldHeight)
        {
            ballPos.y = -fieldHeight;
            ballVel.y *= -1;
        }

        // Colisão com paddles
        if (Mathf.Abs(ballPos.x - paddle1.x) < 0.6f && Mathf.Abs(ballPos.y - paddle1.y) < 1.2f && ballVel.x < 0)
            ballVel.x *= -1;
        if (Mathf.Abs(ballPos.x - paddle2.x) < 0.6f && Mathf.Abs(ballPos.y - paddle2.y) < 1.2f && ballVel.x > 0)
            ballVel.x *= -1;

        // Pontuação
        if (ballPos.x > fieldWidth)
        {
            score1++;
            ResetBall(-1);
        }
        else if (ballPos.x < -fieldWidth)
        {
            score2++;
            ResetBall(1);
        }

        // Envia atualizações
        Broadcast($"POS:1;{paddle1.x:F2};{paddle1.y:F2}");
        Broadcast($"POS:2;{paddle2.x:F2};{paddle2.y:F2}");
        Broadcast($"BALL:{ballPos.x:F2};{ballPos.y:F2}");
        Broadcast($"SCORE:{score1};{score2}");
    }

    void ResetBall(int direction)
    {
        ballPos = Vector2.zero;
        ballVel = new Vector2(direction * 6f, UnityEngine.Random.Range(-3f, 3f));
    }

    void ReceiveData()
    {
        IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);

        while (true)
        {
            try
            {
                byte[] data = server.Receive(ref remoteEP);
                string msg = Encoding.UTF8.GetString(data);

                if (msg == "HELLO")
                {
                    if (client1EP == null)
                    {
                        client1EP = remoteEP;
                        Send(client1EP, "ASSIGN:1");
                        Debug.Log("[Servidor] Cliente 1 conectado");
                    }
                    else if (client2EP == null)
                    {
                        client2EP = remoteEP;
                        Send(client2EP, "ASSIGN:2");
                        Debug.Log("[Servidor] Cliente 2 conectado");
                    }
                }
                else if (msg.StartsWith("POS:"))
                {
                    string[] parts = msg.Substring(4).Split(';');
                    float x = float.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture);
                    float y = float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);

                    if (remoteEP.Equals(client1EP))
                        paddle1.y = y;
                    else if (remoteEP.Equals(client2EP))
                        paddle2.y = y;
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[Servidor] Erro: " + e.Message);
            }
        }
    }

    void Broadcast(string msg)
    {
        byte[] data = Encoding.UTF8.GetBytes(msg);
        if (client1EP != null) server.Send(data, data.Length, client1EP);
        if (client2EP != null) server.Send(data, data.Length, client2EP);
    }

    void Send(IPEndPoint ep, string msg)
    {
        byte[] data = Encoding.UTF8.GetBytes(msg);
        server.Send(data, data.Length, ep);
    }

    void OnApplicationQuit()
    {
        receiveThread?.Abort();
        server?.Close();
    }
}
