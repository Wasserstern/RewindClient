public class ServerWorldStateMessage{
    public WorldState newWorldState;
    public int currentTick;
    public ServerWorldStateMessage(WorldState newWorldState, int currentTick){
        this.newWorldState = newWorldState;
        this.currentTick = currentTick;
    }
}