public class ServerGameStartedMessage{
    public WorldState initialState;
    public ServerGameStartedMessage(WorldState initialState){
        this.initialState = initialState;
    }
}