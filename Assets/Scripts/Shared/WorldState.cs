using System.Collections.Generic;
using UnityEngine;
public class WorldState{
    public List<ClientInformation> clientInformationList;
    public float timeSinceGameStarted;


    public WorldState(List<ClientInformation> newClientInfoList, float timeSinceGamestarted){
        this.clientInformationList = new List<ClientInformation>();
        foreach(ClientInformation info in newClientInfoList){
            ClientInformation copy = new ClientInformation(info.client, info.stream, info.thread, info.username);
            this.clientInformationList.Add(copy);
        }
        this.timeSinceGameStarted = timeSinceGamestarted;
    }
    
}