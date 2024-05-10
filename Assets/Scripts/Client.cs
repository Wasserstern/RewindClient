using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;
public class Client : MonoBehaviour
{
    [Header("Client Settings")]
    public float moveSpeed;
    public float jumpForce;
    public float dashSpeed;
    public float dashCooldown;
    public float dashTimeInSeconds;

    [Header("NetworkSettings")]
    public string username;
    public int port;
    public int bufferSize;
    public int clientID;

    // Components
    Rigidbody rgbd;

    // Runtime variables
    TcpClient tcpClient;
    NetworkStream stream;
    Thread listenThread;
    public bool gameStarted;
    float xInput;
    float zInput;
    bool jumpButtonPressed;
    bool dashButtonPressed;
    bool isGrounded;
    Vector3 moveDirection;
    Vector3 dashDirection;
    Vector3 currentPosition;
    Vector3 nextPosition;
    [SerializeField]
    float currentDashCooldown;
    // Start is called before the first frame update
    async void Start()
    {
        currentDashCooldown = dashCooldown;
        rgbd = GetComponent<Rigidbody>();

        // Establish server connection
        var hostName = Dns.GetHostName();
        IPHostEntry localHost = await Dns.GetHostEntryAsync(hostName);
        IPAddress localIpAddress = localHost.AddressList[0];
        tcpClient = new TcpClient(localIpAddress.AddressFamily);
        await tcpClient.ConnectAsync(localIpAddress, port);
        stream = tcpClient.GetStream();
        Debug.Log($"Connected succesfully to port: {port}");
        currentPosition = nextPosition = transform.position;
    }

    // Update is called once per frame
    void Update()
    {
        // Test stuff
        /*
        if(stream != null){
            if(Input.GetKeyDown(KeyCode.M)){
                TestMessage messageToServer = new TestMessage();
                string json = JsonUtility.ToJson(messageToServer);
                Debug.Log(json);
                string messageJSON = "TEST%" + json;
                Debug.Log(messageJSON);
                byte[] bytes = Encoding.ASCII.GetBytes(messageJSON);
                stream.Write(bytes, 0, bytes.Length);
                Debug.Log("Should have sent a message.");
            }
            if(Input.GetKeyDown(KeyCode.P)){
                ClientMovedMessage message = new ClientMovedMessage(transform.position.x, transform.position.y, transform.position.z);
                string messageJSON = "PLAYERMOVED%" + JsonUtility.ToJson(message);
                Debug.Log(messageJSON);
                byte[] bytes = Encoding.ASCII.GetBytes(messageJSON);
                stream.Write(bytes, 0, bytes.Length);
                Debug.Log("Should have sent a player moved message");
            }
        }
        */
        if(gameStarted){
            if(currentDashCooldown > dashTimeInSeconds){
                GetInput();
            }
            moveDirection = new Vector3(xInput, 0f, zInput);
            currentDashCooldown += Time.deltaTime;
        }
        if(currentPosition != nextPosition){
            // Set initial start position. Freeze player
            transform.position = nextPosition;
            currentPosition = nextPosition;
            rgbd.isKinematic = true;
        }
    }
    private void FixedUpdate()
    {
        if(moveDirection != Vector3.zero){
            rgbd.AddForce(moveDirection * moveSpeed, ForceMode.Force);
        }

        if(jumpButtonPressed){
            rgbd.AddForce(transform.up * jumpForce, ForceMode.Impulse);
        }
        if(currentDashCooldown >= dashCooldown && dashButtonPressed){
            currentDashCooldown = 0;
            rgbd.velocity = Vector3.zero;
            dashDirection = transform.forward.normalized;
        }
        else if (currentDashCooldown < dashTimeInSeconds)
        {
            rgbd.AddForce(dashDirection * dashSpeed);
        }
        
    }

    IEnumerator Dash(){
        float startTime = Time.time;
        float elapsedTime = 0f;
        while(Time.time - startTime < dashTimeInSeconds){
            elapsedTime += Time.deltaTime;
            yield return null;
        }
    }

    void GetInput(){
        xInput = Input.GetAxis("Horizontal");
        zInput = Input.GetAxis("Vertical");
        jumpButtonPressed = Input.GetKeyDown(KeyCode.Space);
        dashButtonPressed = Input.GetMouseButtonDown(1);
    }

    private void OnTriggerEnter(Collider other)
    {
        if(other.gameObject.layer == LayerMask.NameToLayer("Portal")){
            listenThread = new Thread(ListenForServerMesssage);
            listenThread.Start();
            ClientConnectedMessage clientConnectedMessage = new ClientConnectedMessage(username);
            string messageString = "CLIENTCONNECTEDMESSAGE%" + JsonUtility.ToJson(clientConnectedMessage);
            byte[] bytes = Encoding.ASCII.GetBytes(messageString);
            stream.Write(bytes, 0, bytes.Length);
        }
    }
    public void ListenForServerMesssage(){
        try{
            while(true){
                byte[] buffer;
                while(tcpClient.ReceiveBufferSize > 0){
                    buffer = new byte[tcpClient.ReceiveBufferSize];
                    stream.Read(buffer, 0, tcpClient.ReceiveBufferSize);
                    string msg = Encoding.ASCII.GetString(buffer);
                    string[] splitMessage = msg.Split("%");
                    switch(splitMessage[0]){
                        case "SERVERACCEPTMESSAGE":{
                            Debug.Log("Server set initial position.");
                            ServerAcceptMessage serverAcceptMessage = JsonUtility.FromJson<ServerAcceptMessage>(splitMessage[1]);
                            Debug.Log(splitMessage[1]);
                            nextPosition = new Vector3(serverAcceptMessage.initialX, serverAcceptMessage.initialY, serverAcceptMessage.initialZ);
                            break;
                        }
                        case "SERVERGAMESTARTEDMESSAGE":{
                            ServerGameStartedMessage serverGameStartedMessage = JsonUtility.FromJson<ServerGameStartedMessage>(splitMessage[1]);
                            
                            break;
                        }
                    }
                }
            }
        }
        catch(SocketException e){
            Debug.Log("Exception while listening to the server stream: "+  e.Message);
        }
    }
}
