using UnityEngine;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading;
using System.Globalization;

public class UdpClientWithId : MonoBehaviour
{
    UdpClient client;
    Thread receiveThread;
    IPEndPoint serverEP;
    int myId = -1;

    public GameObject localCube;
    public GameObject remoteCube;
    public float moveSpeed = 5f;

    // Bola
    public GameObject ballPrefab; // arraste um prefab aqui no Inspector
    GameObject ballObject;
    Vector2 ballPos = Vector2.zero;

    Vector2 localPos = Vector2.zero;

    void Start()
    {
        client = new UdpClient();
        serverEP = new IPEndPoint(IPAddress.Parse("10.57.1.137"), 5001); // IP do servidor
        client.Connect(serverEP);

        // Thread para receber dados do servidor
        receiveThread = new Thread(ReceiveData);
        receiveThread.IsBackground = true;
        receiveThread.Start();

        // Mensagem inicial
        byte[] hello = Encoding.UTF8.GetBytes("HELLO");
        client.Send(hello, hello.Length);

        // Cria a bola local
        if (ballPrefab != null)
        {
            ballObject = Instantiate(ballPrefab, Vector3.zero, Quaternion.identity);
        }
        else
        {
            // Cria esfera padrão se não tiver prefab
            ballObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ballObject.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
            ballObject.transform.position = Vector3.zero;
            ballObject.name = "Bola";
        }
    }

    void Update()
    {
        // --- MOVIMENTAÇÃO (jogador local) ---
        bool isPressingUp = Input.GetKey(KeyCode.W);
        bool isPressingDown = Input.GetKey(KeyCode.S);

        if (isPressingUp)
        {
            localCube.transform.Translate(Vector2.up * Time.deltaTime * moveSpeed);
        }
        if (isPressingDown)
        {
            localCube.transform.Translate(Vector2.down * Time.deltaTime * moveSpeed);
        }

        // --- ENVIO DA POSIÇÃO ---
        string msg = "POS:" +
                     localCube.transform.position.x.ToString("F2", CultureInfo.InvariantCulture) + ";" +
                     localCube.transform.position.y.ToString("F2", CultureInfo.InvariantCulture);

        byte[] data = Encoding.UTF8.GetBytes(msg);
        client.Send(data, data.Length);

        // Atualiza posição do outro jogador (recebida do servidor)
        remoteCube.transform.position = Vector3.Lerp(remoteCube.transform.position, localPos, Time.deltaTime * 10f);

        // Atualiza posição da bola
        if (ballObject != null)
        {
            ballObject.transform.position = Vector3.Lerp(ballObject.transform.position, ballPos, Time.deltaTime * 10f);
        }
    }

    void ReceiveData()
    {
        IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);

        while (true)
        {
            try
            {
                byte[] data = client.Receive(ref remoteEP);
                string msg = Encoding.UTF8.GetString(data);

                if (msg.StartsWith("ASSIGN:"))
                {
                    myId = int.Parse(msg.Substring(7));
                    Debug.Log("[Cliente] Recebi ID = " + myId);
                }
                else if (msg.StartsWith("POS:"))
                {
                    string[] parts = msg.Substring(4).Split(';');

                    if (parts.Length == 3)
                    {
                        int id = int.Parse(parts[0]);

                        if (id != myId)
                        {
                            float x = float.Parse(parts[1], CultureInfo.InvariantCulture);
                            float y = float.Parse(parts[2], CultureInfo.InvariantCulture);
                            localPos = new Vector2(x, y);
                        }
                    }
                }
                else if (msg.StartsWith("BALL:"))
                {
                    string[] parts = msg.Substring(5).Split(';');

                    if (parts.Length == 2)
                    {
                        float x = float.Parse(parts[0], CultureInfo.InvariantCulture);
                        float y = float.Parse(parts[1], CultureInfo.InvariantCulture);
                        ballPos = new Vector2(x, y);
                    }
                }
            }
            catch (SocketException ex)
            {
                Debug.LogError("SocketException: " + ex.Message);
            }
        }
    }

    void OnApplicationQuit()
    {
        receiveThread?.Abort();
        client?.Close();
    }
}


