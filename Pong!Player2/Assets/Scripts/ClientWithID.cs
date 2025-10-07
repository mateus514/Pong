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
    Vector2 remotePos = Vector2.zero;

    public GameObject remoteCube;
    public GameObject localCube;
    
   

    public float moveSpeed = 5f; // mesma ideia do P1

    void Start()
    {
        client = new UdpClient();
        serverEP = new IPEndPoint(IPAddress.Parse("10.57.1.81"), 5001);
        client.Connect(serverEP);

        // Thread para ouvir respostas do servidor
        receiveThread = new Thread(ReceiveData);
        receiveThread.Start();

        // Envia mensagem inicial para o servidor
        byte[] hello = Encoding.UTF8.GetBytes("HELLO");
        client.Send(hello, hello.Length);
    }

    void Update()
    {
        // --- MOVIMENTAÇÃO IGUAL AO SCRIPT P1 ---
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

        // --- ENVIO DA POSIÇÃO PARA O SERVIDOR ---
        string msg = "POS:" +
                     localCube.transform.position.x.ToString("F2", CultureInfo.InvariantCulture) + ";" +
                     localCube.transform.position.y.ToString("F2", CultureInfo.InvariantCulture);

        byte[] data = Encoding.UTF8.GetBytes(msg);
        client.Send(data, data.Length);
        
        // Atualiza posição do outro jogador

        remoteCube.transform.position = Vector3.Lerp(

            remoteCube.transform.position, 
            
            remotePos,

            Time.deltaTime* 10f

        );
    }

    void ReceiveData()
    {
        IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);

        while (true)
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

                        float x = float.Parse(parts[1],
                            CultureInfo.InvariantCulture);

                        float y = float.Parse(parts[2],
                            CultureInfo.InvariantCulture);

                        remotePos = new Vector3(x, y, 0);
                    }
                }
            }
        }
    }



    void OnApplicationQuit()
    {
        receiveThread.Abort();
        client.Close();
    }
}
