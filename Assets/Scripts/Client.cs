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
    public float positionCorrectionThreshold;
    public float updatesPerSecond;
    public GameObject playerPrefab;

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
    int clientTick;
    bool hasBeenCorrected;
    Dictionary<int , Vector3> previousPositions;
    [SerializeField]
    float currentDashCooldown;
    float updateTimer;
    bool readyToUpdate;
    // Start is called before the first frame update
    async void Start()
    {
        currentDashCooldown = dashCooldown;
        rgbd = GetComponent<Rigidbody>();
        previousPositions = new Dictionary<int, Vector3>();

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

        updateTimer += Time.deltaTime;
        if(updateTimer > 1f / updatesPerSecond){
            readyToUpdate = true;
            updateTimer = 0f;
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
    public void UpdateServer(){
        if(readyToUpdate){
            if(previousPositions[clientTick] != transform.position){
                // Client moved since last tick. Send Move Message
                ClientMovedMessage clientMovedMessage = new ClientMovedMessage(transform.position.x, transform.position.y, transform.position.z);
                string msg = "CLIENTMOVEDMESSAGE%" + JsonUtility.ToJson(clientMovedMessage);
                byte[] bytes = Encoding.ASCII.GetBytes(msg);
                stream.Write(bytes, 0, bytes.Length);
            }
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
                            WorldState initialState = serverGameStartedMessage.initialState;
                            List<ClientInformation> clientInformationList = initialState.clientInformationList;
                            foreach(ClientInformation info in clientInformationList)
                            {
                                if(info.username != username){
                                    // Instantiate other players
                                    GameObject player = Instantiate(playerPrefab);
                                    player.transform.position = info.currentPosition;
                                    player.name = info.username;
                                }
                                else{
                                    transform.position = info.currentPosition;
                                }

                            }
                            gameStarted = true;
                            break;
                        }
                        case "SERVERWORLDSTATEMESSAGE":{
                            ServerWorldStateMessage serverWorldStateMessage = JsonUtility.FromJson<ServerWorldStateMessage>(splitMessage[1]);
                            WorldState newWorldState =  serverWorldStateMessage.newWorldState;
                            List<ClientInformation> clientInformationList = newWorldState.clientInformationList;

                            foreach(ClientInformation info in clientInformationList)
                            {
                                if(info.username != username){
                                    // Update other players position
                                    // TODO: Implement interpolation between two positions instead of directly updating the position
                                }
                                else{
                                    // Check if previousPositions were corrected by the server
                                    Vector3 correctedPosition = info.currentPosition;
                                    Vector3 previousPosition = previousPositions[serverWorldStateMessage.currentTick];
                                    if(Vector3.Distance(correctedPosition, previousPosition) > positionCorrectionThreshold){
                                        // Correct players position. This possibly warps him.
                                        // TODO: Instead of correcting the position directly. Do a step by step correction. Taking previous client frames.
                                        transform.position = correctedPosition;
                                        hasBeenCorrected = true;
                                    }
                                }
                            }
                            clientTick = serverWorldStateMessage.currentTick;
                            Thread updateServerThread = new Thread(UpdateServer);
                            updateServerThread.Start();
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
