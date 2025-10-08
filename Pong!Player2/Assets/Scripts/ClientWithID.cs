using UnityEngine;
using UnityEngine.UI;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading;
using System.Globalization;
using System.Collections.Generic;

public class ClientLocal : MonoBehaviour
{
    UdpClient client;
    Thread receiveThread;
    IPEndPoint serverEP;
    int myId = -1;

    public GameObject localCube;
    public GameObject remoteCube;
    public float moveSpeed = 5f;

    // Bola
    public GameObject ballPrefab;
    GameObject ballObject;
    Vector2 ballPos = Vector2.zero;

    Vector2 localPos = Vector2.zero;

    // Placar
    Text scoreText;
    int scoreLocal = 0;
    int scoreRemote = 0;

    // Ações a executar na thread principal
    Queue<System.Action> mainThreadActions = new Queue<System.Action>();

    void Start()
    {
        client = new UdpClient();
        serverEP = new IPEndPoint(IPAddress.Parse("10.57.1.137"), 5001); // IP do servidor
        client.Connect(serverEP);

        receiveThread = new Thread(ReceiveData);
        receiveThread.IsBackground = true;
        receiveThread.Start();

        // Mensagem inicial
        byte[] hello = Encoding.UTF8.GetBytes("HELLO");
        client.Send(hello, hello.Length);

        // Cria a bola local
        if (ballPrefab != null)
            ballObject = Instantiate(ballPrefab, Vector3.zero, Quaternion.identity);
        else
        {
            ballObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ballObject.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
            ballObject.transform.position = Vector3.zero;
            ballObject.name = "Bola";
        }

        // --- Cria placar automaticamente ---
        Canvas canvas = new GameObject("Canvas").AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvas.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvas.gameObject.AddComponent<GraphicRaycaster>();

        GameObject textGO = new GameObject("ScoreText");
        textGO.transform.SetParent(canvas.transform);
        scoreText = textGO.AddComponent<Text>();
        scoreText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        scoreText.fontSize = 48;
        scoreText.alignment = TextAnchor.UpperCenter;
        scoreText.color = Color.white;
        RectTransform rt = scoreText.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -20f);
        rt.sizeDelta = new Vector2(400f, 100f);
        scoreText.text = "0  -  0";
    }

    void Update()
    {
        // Executa ações da thread principal
        while (mainThreadActions.Count > 0)
            mainThreadActions.Dequeue().Invoke();

        // Movimento do jogador local
        if (Input.GetKey(KeyCode.W))
            localCube.transform.Translate(Vector2.up * Time.deltaTime * moveSpeed);
        if (Input.GetKey(KeyCode.S))
            localCube.transform.Translate(Vector2.down * Time.deltaTime * moveSpeed);

        // Envia posição
        string msg = "POS:" +
                     localCube.transform.position.x.ToString("F2", CultureInfo.InvariantCulture) + ";" +
                     localCube.transform.position.y.ToString("F2", CultureInfo.InvariantCulture);
        byte[] data = Encoding.UTF8.GetBytes(msg);
        client.Send(data, data.Length);

        // Atualiza posição do outro jogador
        remoteCube.transform.position = Vector3.Lerp(remoteCube.transform.position, localPos, Time.deltaTime * 10f);

        // Atualiza posição da bola
        if (ballObject != null)
            ballObject.transform.position = Vector3.Lerp(ballObject.transform.position, ballPos, Time.deltaTime * 10f);
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
                        mainThreadActions.Enqueue(() => ballPos = new Vector2(x, y));
                    }
                }
                else if (msg.StartsWith("SCORE:"))
                {
                    string[] parts = msg.Substring(6).Split(';');
                    if (parts.Length == 2)
                    {
                        int s1 = int.Parse(parts[0]);
                        int s2 = int.Parse(parts[1]);
                        mainThreadActions.Enqueue(() =>
                        {
                            scoreText.text = $"{s1}  -  {s2}";
                        });
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
